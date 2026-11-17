using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using GameCombat.AgentContract;
namespace GameCombat.ML {
 [RequireComponent(typeof(BehaviorParameters))]
 public sealed class EnemyLearningAgent : Agent,ICombatPolicy,ICombatPolicyDiagnostics {
  [Tooltip("Explicit contract test mode. Never enabled by default.")] public bool testHeuristic;
  public TextAsset modelMetadata;
  public bool trainingMode;
  [Header("Gameplay evaluation binding (never training approval)")]
  [Tooltip("Enable only on a gameplay evaluation adapter. Training/heuristic modes are rejected here.")] public bool runtimeEvaluationOnly;
  [Tooltip("Imported model verified against the recorded ONNX SHA256 during installation.")] public Unity.InferenceEngine.ModelAsset compatibleModel;
  [Tooltip("SHA256 of the exact runtime metadata TextAsset. Prevents swapping metadata independently.")] public string compatibleMetadataSha256;
  [Tooltip("Recorded ONNX SHA256 for the bound model.")] public string modelSha256;
  [Tooltip("SHA256 of the imported inference payload, verified once per model instance at activation.")] public string compatiblePayloadSha256;
  Unity.InferenceEngine.ModelAsset hashedModel;string payloadHash;
  public static string PayloadHash(Unity.InferenceEngine.ModelAsset model){using var stream=new System.IO.MemoryStream();Unity.InferenceEngine.ModelWriter.Save(stream,model);using var sha=System.Security.Cryptography.SHA256.Create();return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
  public string PolicyLabel=>runtimeEvaluationOnly?$"EVALUATION ONLY — failed training gates\n{(compatibleModel?compatibleModel.name:"missing model")} SHA256 {modelSha256}":testHeuristic?"Diagnostic heuristic (not learned inference)":trainingMode?"Explicit training adapter":"Saved policy evaluation";
  public event Action EpisodeRequested;
  public event Action<DecisionFrame> DecisionRequested;
  public event Action<int,int> ActionReturned;
  public long DecisionCount {get;private set;}
  float[] lastObservation=new float[66];
  public void ResetEpisodeState(){pending=null;receive=null;heuristicStep=heuristicCombatStep=0;lastObservation=new float[66];}
  public void ResetArenaTransport(){ResetEpisodeState();EndEpisode();}
  public override void OnEpisodeBegin(){EpisodeRequested?.Invoke();}
  DecisionFrame pending; Action<uint,uint,int,int> receive; int heuristicStep,heuristicCombatStep;
  [Serializable] class Metadata {public int contractVersion,observationSize,stacks,decisionTicks,latencyTicks;public string tuningHash,editorTuningHash,movementVersion,exportSha256,inferenceMode;public float fixedDeltaTime,combatMoveSpeed;public bool evaluationOnly;}
  public override void Initialize(){var b=GetComponent<BehaviorParameters>();b.InferenceDevice=InferenceDevice.Burst;b.BehaviorType=trainingMode?BehaviorType.Default:testHeuristic || !b.Model?BehaviorType.HeuristicOnly:BehaviorType.InferenceOnly;}
  public bool CanActivate(string tuningHash,out string reason){
   var b=GetComponent<BehaviorParameters>();var spec=b.BrainParameters.ActionSpec;
   if(runtimeEvaluationOnly && (trainingMode || testHeuristic)){reason="Gameplay adapter requires saved-model inference, not training/heuristic";return false;}
   if(spec.NumContinuousActions!=0 || spec.BranchSizes.Length!=2 || spec.BranchSizes[0]!=9 || spec.BranchSizes[1]!=5 || b.BrainParameters.VectorObservationSize!=66 || b.BrainParameters.NumStackedVectorObservations!=3){reason="ML contract shape mismatch";return false;}
   if(trainingMode){reason="Explicit local trainer";return isActiveAndEnabled && b.BehaviorType==BehaviorType.Default && Academy.Instance.IsCommunicatorOn;}
   if(testHeuristic){reason="Explicit heuristic contract test";return isActiveAndEnabled && b.BehaviorType==BehaviorType.HeuristicOnly;}
   if(!b.Model || !modelMetadata){reason="ML model/metadata missing";return false;}
   if(b.BehaviorType!=BehaviorType.InferenceOnly || b.InferenceDevice!=InferenceDevice.Burst){reason="ML requires InferenceOnly with CPU Burst";return false;}
   var m=JsonUtility.FromJson<Metadata>(modelMetadata.text);
   if(runtimeEvaluationOnly){
    string mode=m?.inferenceMode??"deterministic";if(mode!="deterministic" && mode!="sampled" || b.DeterministicInference!=(mode=="deterministic")){reason="ML inference mode metadata mismatch";return false;}
    using var sha=System.Security.Cryptography.SHA256.Create();
    string metadataHash=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(modelMetadata.text))).Replace("-","").ToLowerInvariant();
    ICombatMovementMetadata motor=null;foreach(var component in GetComponentsInParent<MonoBehaviour>())if(component is ICombatMovementMetadata movement)motor=movement;
    if(b.Model!=compatibleModel || !compatibleModel || metadataHash!=compatibleMetadataSha256 || m==null || m.exportSha256!=modelSha256){reason="ML model/metadata identity mismatch";return false;}
    if(hashedModel!=b.Model){payloadHash=PayloadHash(b.Model);hashedModel=b.Model;}
    if(payloadHash!=compatiblePayloadSha256){reason="ML imported model payload mismatch";return false;}
    if(!m.evaluationOnly || m.movementVersion!="shared-enemy-motor-v1" || motor==null || Mathf.Abs(motor.CombatMoveSpeed-m.combatMoveSpeed)>.00001f || Mathf.Abs(m.combatMoveSpeed-2.5f)>.00001f || Mathf.Abs(Time.fixedDeltaTime-m.fixedDeltaTime)>.00001f || Mathf.Abs(m.fixedDeltaTime-.02f)>.00001f || m.decisionTicks!=3 || m.latencyTicks!=1){reason="ML movement/clock/scheduling metadata mismatch";return false;}
   }
   // Unity Editor and standalone JsonUtility produce distinct fingerprints for the
   // same source assets. The installed manifest records both; training provenance
   // retains its original standalone hash. Never substitute one in training.
   bool tuningMatches=m!=null && (m.tuningHash==tuningHash || runtimeEvaluationOnly && Application.isEditor && m.editorTuningHash==tuningHash);
   bool valid=m!=null && m.contractVersion==1 && m.observationSize==66 && m.stacks==3 && tuningMatches;
   reason=valid?"Local CPU inference":"ML contract/tuning metadata mismatch";return valid && isActiveAndEnabled;
  }
  public void Decide(DecisionFrame frame,Action<uint,uint,int,int> callback){if(pending!=null)throw new InvalidOperationException("Previous ML decision still pending");pending=frame;lastObservation=frame.observations;receive=callback;DecisionCount++;DecisionRequested?.Invoke(frame);RequestDecision();}
  public override void CollectObservations(Unity.MLAgents.Sensors.VectorSensor sensor){foreach(float value in pending!=null?pending.observations:lastObservation)sensor.AddObservation(value);}
  public override void WriteDiscreteActionMask(IDiscreteActionMask mask){if(pending==null)return;for(int i=1;i<9;i++)mask.SetActionEnabled(0,i,pending.movementAllowed[i]);for(int i=1;i<5;i++)mask.SetActionEnabled(1,i,pending.combatAllowed[i]);}
  public override void OnActionReceived(ActionBuffers actions){var frame=pending;var callback=receive;pending=null;receive=null;if(frame==null)return;ActionReturned?.Invoke(actions.DiscreteActions[0],actions.DiscreteActions[1]);callback?.Invoke(frame.generation,frame.sequence,actions.DiscreteActions[0],actions.DiscreteActions[1]);}
  public override void Heuristic(in ActionBuffers output){if(pending==null)return;int move=heuristicStep++%9,combat=heuristicCombatStep%5;var choices=output.DiscreteActions;choices[0]=pending.movementAllowed[move]?move:0;choices[1]=pending.combatAllowed[combat]?combat:0;if(pending.combatAllowed[combat])heuristicCombatStep++;}
  // Keep an outstanding frame until its callback drains. It cannot be relabelled
  // as a new owner's decision after disable/switch/re-enable.
  public void Deactivate(){receive=null;}
  protected override void OnDisable(){Deactivate();base.OnDisable();}
 }
}






