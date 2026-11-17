using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GameCombat;
using GameCombat.ML;
using GameCombat.AgentContract;

// Interface fixture only. Never saved or installed as a learned opponent.
public sealed class DemoControlledPolicy : MonoBehaviour,ICombatPolicy {
 public int mode;public DecisionFrame frame;public int move,combat,decisions;
 public bool CanActivate(string hash,out string reason){reason="Controlled interface test, NOT learned performance";return true;}
 public void Decide(DecisionFrame f,Action<uint,uint,int,int> receive){
  frame=f;decisions++;move=combat=0;
  if(mode==1)combat=f.combatAllowed[2]?2:0;
  if(mode==2 && f.observations[20]>0){float distance=f.observations[22]*10;if(distance>1.45f)move=f.movementAllowed[1]?1:0;else combat=f.combatAllowed[2]?2:0;}
  receive(f.generation,f.sequence,move,combat);
 }
 public void Deactivate(){}
}

[InitializeOnLoad]
public static class DemoExecutionDiagnosis {
 [Serializable] class InferenceMode {public string inferenceMode;}
 const string Folder="Logs/DemoPolicy20261117";
 static EnemyBrainHost host;static EnemyMotor motor;static Combatant actor,target;static enemy body;static Player player;
 static EnemyLearningAgent learned;static DemoControlledPolicy controlled;static DecisionFrame frame;
 static int index=-1,tick,returnedMove,returnedCombat,requests,accepted,rejected,contacts;static long startDecisions;static float damage,previousYaw,maxTurn;
 static double deadline;static bool done,attached;static string runName,lastReason="none";
 static Vector3 basePoint=new(1000,0,1000);static float actorY,targetY;
 static readonly List<CaseResult> results=new();
 static readonly string[] names={"controlled-ahead-neutral","controlled-right-neutral","controlled-left-neutral","controlled-behind-neutral","controlled-orbit-neutral","controlled-inside-light","controlled-outside-spam","controlled-approach","original-inside","original-passive-seed100001","original-orbit","normal-orbit"};
 static readonly int[] lengths={90,110,110,130,180,180,180,300,220,1500,220,220};
 static readonly float[] angles={0,90,-90,180,0,0,0,90,90,0,0,0};
 [Serializable] public class CaseResult {public string name,policy,constraints,facingPath;public int ticks,requests,accepted,rejected,contacts,decisions;public float damage,endDistance,endFacingError,rootYaw,modelYaw,maxTurnPerTick;public bool activeML,noFallback;}
 [Serializable] class Trace {public string kind="tick",scenario,selected,active,fallback,owner,movement,action,phase,reason;public int tick,move,combat,appliedMove,appliedCombat,bufferedMove;public long agentTick,applyTick;public uint generation,decisionSequence;public bool visible,delayedVisible,guard,sleeping,awaiting,received;public float elapsed,health,damage,distance;public Vector3 publicDirection,desiredFacing,rbEuler,modelEuler,animatorEuler,position,targetPosition;public bool[] moveMask,combatMask;public float[] observation;}
 [Serializable] class RequestTrace {public string kind="request",scenario,request,result,action,phase;public int tick;public bool grounded;public float distance,facingError;}
 static DemoExecutionDiagnosis(){if(SessionState.GetBool("DemoExecutionDiagnosis",false)){deadline=EditorApplication.timeSinceStartup+90;runName=SessionState.GetString("DemoDiagnosisName","diagnosis");EditorApplication.update+=Update;}}
 static object Field(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
 public static void Run()=>RunInternal(false);
 public static void RunStaged()=>RunInternal(true);
 static void RunInternal(bool staged){
  Directory.CreateDirectory(Folder);runName=Environment.GetEnvironmentVariable("DEMO_DIAGNOSIS_NAME")??"diagnosis";
  if(File.Exists(Folder+"/"+runName+".jsonl"))throw new Exception("Use fresh diagnostic name");
  SessionState.SetString("DemoDiagnosisName",runName);SessionState.SetBool("DemoExecutionDiagnosis",true);
  EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
  if(staged){
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var h=UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>();var a=h.agentPolicy as EnemyLearningAgent;var b=a.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
   b.Model=AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>("Assets/DemoPolicyEvaluation/CombatV1.onnx");a.compatibleModel=b.Model;a.modelMetadata=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DemoPolicyEvaluation/runtime-metadata.json");b.DeterministicInference=JsonUtility.FromJson<InferenceMode>(a.modelMetadata.text).inferenceMode!="sampled";a.compatibleMetadataSha256=RuntimeEvaluationSetup.Hash(System.Text.Encoding.UTF8.GetBytes(a.modelMetadata.text));a.modelSha256=RuntimeEvaluationSetup.Hash(File.ReadAllBytes("Assets/DemoPolicyEvaluation/CombatV1.onnx"));a.compatiblePayloadSha256=EnemyLearningAgent.PayloadHash(b.Model);
  }
  deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=Update;EditorApplication.EnterPlaymode();
 }
 static void Update(){if(done)return;try{
  if(EditorApplication.timeSinceStartup>deadline)throw new Exception("90 second diagnostic deadline");
  if(!EditorApplication.isPlaying || attached)return;
  Time.timeScale=1;Time.fixedDeltaTime=.02f;Time.captureDeltaTime=.02f;
  host=UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>();player=UnityEngine.Object.FindFirstObjectByType<Player>();if(!host || !player)return;
  actor=host.GetComponent<Combatant>();target=player.GetComponent<Combatant>();motor=host.GetComponent<EnemyMotor>();body=host.GetComponent<enemy>();learned=host.agentPolicy as EnemyLearningAgent;
  string why="adapter missing";if(!learned || !learned.CanActivate(host.AgentTuningHash,out why))throw new Exception("Installed model unavailable "+why);
  player.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;player.inputComponent.ResetEpisodeState();player.standaloneControl=true;
  actorY=actor.transform.position.y-actor.GetComponent<Collider>().bounds.min.y+.02f;targetY=target.transform.position.y-target.GetComponent<Collider>().bounds.min.y+.02f;
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Controlled matched flat diagnostic geometry";floor.layer=7;floor.transform.position=basePoint+Vector3.down*.25f;floor.transform.localScale=new Vector3(30,.5f,30);
  var surface=floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();surface.collectObjects=Unity.AI.Navigation.CollectObjects.Children;surface.useGeometry=UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
  controlled=host.gameObject.AddComponent<DemoControlledPolicy>();
  learned.DecisionRequested+=f=>frame=f;learned.ActionReturned+=(m,c)=>{returnedMove=m;returnedCombat=c;};
  actor.Runner.RequestEvaluated+=Request;actor.Result+=Result;
  CombatWorld.BeforeStep+=Before;CombatWorld.AfterStep+=After;attached=true;Next();
 }catch(Exception ex){Fail(ex);}}
 static void Reset(){
  actor.ContactsSuppressed=target.ContactsSuppressed=true;
  learned.ResetEpisodeState();learned.ResetArenaTransport();host.ResetEpisodeState();actor.ResetEpisodeState();target.ResetEpisodeState();body.ResetEpisodeState();player.ResetEpisodeState();
  actor.GetComponent<EnemyPerceptionNav>().ResetEpisodeState();actor.GetComponent<EnemyPerceptionNav>().target=target.transform;
  foreach(var a in new[]{actor,target}){a.GetComponentInChildren<Animator>().Rebind();a.GetComponentInChildren<Animator>().Update(0);a.GetComponent<CombatPresentation>().ResetEpisodeState();CombatWorld.ForgetMotion(a);}
  actor.ContactsSuppressed=target.ContactsSuppressed=false;
 }
 static void PlaceTarget(Vector3 position){position.y=targetY;var rb=target.GetComponent<Rigidbody>();rb.position=position;target.transform.position=position;rb.linearVelocity=rb.angularVelocity=Vector3.zero;player.input.movement=Vector2.zero;Physics.SyncTransforms();}
 static void Next(){
  if(index>=0){var d=target.transform.position-actor.transform.position;d.y=0;
   results.Add(new CaseResult{name=names[index],policy=index<8?"CONTROLLED INTERFACE, NOT LEARNED":index==11?"Normal":"SAVED LEARNED MODEL "+learned.modelSha256,constraints=body.rb.constraints.ToString(),facingPath=PathOf(actor.facing),ticks=tick,requests=requests,accepted=accepted,rejected=rejected,contacts=contacts,decisions=index<8?controlled.decisions:(int)(learned.DecisionCount-startDecisions),damage=damage,endDistance=d.magnitude,endFacingError=Vector3.Angle(actor.facing.forward,d),rootYaw=body.rb.rotation.eulerAngles.y,modelYaw=actor.facing.eulerAngles.y,maxTurnPerTick=maxTurn,activeML=host.ActiveMode==EnemyControlMode.ML,noFallback=string.IsNullOrEmpty(host.FallbackReason)});
  }
  if(++index>=names.Length){File.WriteAllText(Folder+"/"+runName+".summary.json",JsonUtility.ToJson(new Report{cases=results.ToArray()},true));Debug.Log("DEMO_EXECUTION_DIAGNOSIS_COMPLETE cases="+results.Count);Finish(0);return;}
  host.SetMode(EnemyControlMode.Normal);Reset();
  tick=requests=accepted=rejected=contacts=0;damage=maxTurn=0;frame=null;lastReason="none";controlled.frame=null;controlled.decisions=0;startDecisions=learned.DecisionCount;
  float distance=index==6?3.1f:index==7?3.5f:index==9?3.06017494f:index==5 || index==8?1.5f:2.4f;
  var direction=Quaternion.Euler(0,angles[index],0)*Vector3.forward;
  Quaternion initial=Quaternion.identity;
  if(index==9){var rng=new System.Random(100001);distance=1.5f+2*(float)rng.NextDouble();float angle=-Mathf.PI+2*Mathf.PI*(float)rng.NextDouble();direction=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));initial=Quaternion.LookRotation(direction);}
  var position=basePoint+Vector3.up*actorY;
  motor.Teleport(position,initial);if(body.agent.enabled)body.agent.Warp(position);
  PlaceTarget(basePoint+direction*distance);target.GetComponent<Rigidbody>().rotation=Quaternion.LookRotation(-direction);
  controlled.mode=index==5 || index==6?1:index==7?2:0;
  host.agentPolicy=index<8?controlled:learned;host.SetMode(index==11?EnemyControlMode.Normal:EnemyControlMode.ML);
  previousYaw=body.rb.rotation.eulerAngles.y;
 }
 [Serializable] class Report {public CaseResult[] cases;}
 static void Before(){if(done)return;try{
  if(index==4 || index==10 || index==11){float angle=tick*.02f*.8f;PlaceTarget(basePoint+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*2.4f);}
 }catch(Exception ex){Fail(ex);}}
 static void Request(CombatRequest request,RequestResult result){requests++;if(result==RequestResult.Accepted)accepted++;else rejected++;lastReason=request+":"+result;
  File.AppendAllText(Folder+"/"+runName+".jsonl",JsonUtility.ToJson(new RequestTrace{scenario=names[index],tick=tick+1,request=request.ToString(),result=result.ToString(),action=actor.Runner.Action.ToString(),phase=actor.Runner.Phase.ToString(),grounded=body.grounded,distance=Vector3.Distance(actor.transform.position,target.transform.position),facingError=Vector3.Angle(actor.facing.forward,target.transform.position-actor.transform.position)})+Environment.NewLine);
 }
 static void Result(CombatResult r){if(r.attackerId==actor.Id && r.actualHealthDamage>0){damage+=r.actualHealthDamage;contacts++;}}
 static void After(){if(done)return;try{
  tick++;if(index<8){frame=controlled.frame;returnedMove=controlled.move;returnedCombat=controlled.combat;}
  var delta=target.transform.position-actor.transform.position;delta.y=0;
  float yaw=body.rb.rotation.eulerAngles.y;if(tick>2)maxTurn=Mathf.Max(maxTurn,Mathf.Abs(Mathf.DeltaAngle(previousYaw,yaw)));previousYaw=yaw;
  var trace=new Trace{scenario=names[index],tick=tick,selected=host.mode.ToString(),active=host.ActiveMode.ToString(),fallback=host.FallbackReason,owner=motor.Owner.ToString(),movement=motor.MovementDiagnostic,action=actor.Runner.Action.ToString(),phase=actor.Runner.Phase.ToString(),elapsed=actor.Runner.Elapsed,reason=lastReason,move=returnedMove,combat=returnedCombat,appliedMove=(int)Field(host,"pendingMove"),appliedCombat=(int)Field(host,"previousCombat"),visible=actor.GetComponent<EnemyPerceptionNav>().CanSeePlayer(),delayedVisible=frame!=null && frame.observations[20]>0,publicDirection=host.ObservedTargetDirection,desiredFacing=(Vector3)Field(motor,"facing"),rbEuler=body.rb.rotation.eulerAngles,modelEuler=actor.facing.eulerAngles,animatorEuler=body.animator.transform.eulerAngles,position=body.rb.position,targetPosition=target.transform.position,health=target.Health.Health,damage=damage,distance=delta.magnitude,guard=actor.Runner.Guarding,moveMask=frame?.movementAllowed,combatMask=frame?.combatAllowed,observation=frame?.observations};
  trace.bufferedMove=(int)Field(host,"pendingMove");trace.appliedMove=Array.FindIndex(Enumerable.Range(0,9).ToArray(),i=>(EnemyBrainHost.AgentMovement(i)-host.Intent.localMove).sqrMagnitude<.0001f);
  trace.agentTick=(long)Field(host,"agentTick");trace.applyTick=(long)Field(host,"applyTick");trace.awaiting=(bool)Field(host,"awaiting");trace.received=(bool)Field(host,"received");trace.generation=host.Generation;trace.decisionSequence=frame==null?0:frame.sequence;
  trace.sleeping=body.rb.IsSleeping();File.AppendAllText(Folder+"/"+runName+".jsonl",JsonUtility.ToJson(trace)+Environment.NewLine);
  if(tick>=lengths[index] || actor.Dead || target.Dead)Next();
 }catch(Exception ex){Fail(ex);}}
 static void Fail(Exception ex){Debug.LogException(ex);Finish(1);}
 static void Finish(int code){done=true;SessionState.SetBool("DemoExecutionDiagnosis",false);CombatWorld.BeforeStep-=Before;CombatWorld.AfterStep-=After;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}
