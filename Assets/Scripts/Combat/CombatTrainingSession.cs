using System;
using System.IO;
using UnityEngine;
using Unity.MLAgents.Policies;
namespace GameCombat.Training {
[DefaultExecutionOrder(-200)]
public sealed class CombatTrainingSession : MonoBehaviour {
 public CombatTrainingArena[] arenas;
 public Unity.InferenceEngine.ModelAsset evaluationModel;
 public TextAsset evaluationMetadata;
 public int defaultArenaCount=1;
 [Header("Visual inspection (Editor only)")]
 [Tooltip("Use normal-speed, wall-time-paced heuristic inspection: scale 1, fixed 0.02, capture 0.02. Ignored for --phase5-train and --phase5-eval. Leave off for accelerated runs.")]
 public bool normalSpeedVisualInspection;
 public bool NormalSpeedVisualInspectionActive=>Application.isEditor && normalSpeedVisualInspection && !automatedRun;
 public int Completed {get;private set;}
 public int evaluationPerFamily=200;
 int evaluationFamily,evaluationEndFamily;bool evaluation,automatedRun,inspectionPacing;
 double nextInspectionFrame;
 static CombatTrainingSession clockOwner;
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
 static void ResetClockOwner(){clockOwner=null;}
 string Arg(string name,string fallback){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);return index>=0 && index+1<args.Length?args[index+1]:fallback;}
 bool Has(string name)=>Array.IndexOf(Environment.GetCommandLineArgs(),name)>=0;
 void Awake(){
  Application.logMessageReceived+=Fatal;
  Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
  evaluation=Has("--phase5-eval");automatedRun=evaluation || Has("--phase5-train");clockOwner=this;
  ApplyClock();int count=int.Parse(Arg("--arenas",defaultArenaCount.ToString()));if(count!=1 && count!=8)throw new ArgumentException("Use one proven arena or eight arenas");
  evaluationEndFamily=Enum.GetValues(typeof(OpponentFamily)).Length;
  string selectedFamily=Arg("--eval-family","all");if(selectedFamily!="all"){if(!Enum.TryParse<OpponentFamily>(selectedFamily,out var selected))throw new ArgumentException("Unknown opponent family");evaluationFamily=(int)selected;evaluationEndFamily=evaluationFamily+1;}
  UnityEngine.Random.InitState(int.Parse(Arg("--train-seed","23")));
  evaluationPerFamily=int.Parse(Arg("--eval-episodes",evaluationPerFamily.ToString()));int stage=int.Parse(Arg("--stage","1"));if(stage<1 || stage>4)throw new ArgumentException("Stage must be 1..4");
  string output=Arg("--metrics",evaluation?"Logs/Phase5Environment/evaluation.jsonl":"Logs/Phase5Environment/episodes.jsonl");
  for(int i=0;i<arenas.Length;i++){
   var arena=arenas[i];bool active=i<count && (!evaluation || i==0);if(!active){arena.gameObject.SetActive(false);continue;}
   arena.evaluation=evaluation;arena.evaluationSeedOffset=int.Parse(Arg("--eval-seed-offset","100000"));if(arena.evaluationSeedOffset<100000 || arena.evaluationSeedOffset>700000)throw new ArgumentException("Evaluation seed offset outside reserved held-out range");arena.stage=stage;arena.seed=int.Parse(Arg("--train-seed","23"));arena.outputPath=output;arena.hideRenderers=arena.hideRenderers && !Has("--rendered");arena.family=(OpponentFamily)evaluationFamily;
   var agent=arena.agent;agent.trainingMode=Has("--phase5-train") && !evaluation;agent.testHeuristic=!evaluation && !agent.trainingMode;
   var behavior=agent.GetComponent<BehaviorParameters>();behavior.BehaviorName="CombatV1";
   if(evaluation){if(!evaluationModel || !evaluationMetadata)throw new InvalidOperationException("Build evaluation with an exported combat model and metadata");behavior.Model=evaluationModel;behavior.DeterministicInference=!Has("--sample-policy");agent.modelMetadata=evaluationMetadata;}
   arena.EpisodeCompleted+=OnEpisode;
   arena.gameObject.SetActive(true);
  }
  // Physics contacts are explicitly excluded even if an actor were teleported.
  for(int i=0;i<arenas.Length;i++)for(int j=i+1;j<arenas.Length;j++)foreach(var a in new[]{arenas[i].learner,arenas[i].opponent})foreach(var b in new[]{arenas[j].learner,arenas[j].opponent})foreach(var ca in a.GetComponentsInChildren<Collider>(true))foreach(var cb in b.GetComponentsInChildren<Collider>(true))Physics.IgnoreCollision(ca,cb,true);
 }
 public static void ApplyClock(){bool inspect=clockOwner && clockOwner.NormalSpeedVisualInspectionActive;Time.fixedDeltaTime=.02f;Time.timeScale=inspect ? 1 : 4;Time.captureDeltaTime=inspect ? .02f : .005f;Time.maximumDeltaTime=.08f;}
 void Update(){ApplyClock();PaceVisualInspection();}
 void PaceVisualInspection(){
  if(!NormalSpeedVisualInspectionActive){inspectionPacing=false;return;}
  // Capture time advances once per rendered frame, independently of wall time.
  // Pace only this Editor diagnostic, including Scene view, to at most 50 Hz.
  double now=(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency;
  if(!inspectionPacing){nextInspectionFrame=now+.02;inspectionPacing=true;}
  double remaining=nextInspectionFrame-now;
  if(remaining>0)System.Threading.Thread.Sleep((int)Math.Ceiling(Math.Min(.02,remaining)*1000));
  nextInspectionFrame=(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency+.02;
 }
 void Start(){if(evaluation && Unity.MLAgents.Academy.Instance.IsCommunicatorOn)throw new InvalidOperationException("Evaluation must not connect to Python");Debug.Log("PHASE5_SESSION_CLOCK fixed="+Time.fixedDeltaTime+" capture="+Time.captureDeltaTime+" scale="+Time.timeScale+" visualInspection="+NormalSpeedVisualInspectionActive+" communicator="+Unity.MLAgents.Academy.Instance.IsCommunicatorOn);}
 void OnDestroy(){Application.logMessageReceived-=Fatal;if(clockOwner==this)clockOwner=null;}
 void Fatal(string message,string stack,LogType type){if(!Application.isEditor && (Has("--phase5-train") || evaluation) && (type==LogType.Error || type==LogType.Exception))Application.Quit(2);}
 void OnEpisode(EpisodeRecord record){Completed++;if(!evaluation)return;
  if(Completed% evaluationPerFamily==0){evaluationFamily++;arenas[0].family=(OpponentFamily)evaluationFamily;}
  if(evaluationFamily>=evaluationEndFamily){Debug.Log("PHASE5_HELD_OUT_COMPLETE episodes="+Completed);if(!Application.isEditor)Application.Quit(0);else enabled=false;}
 }
}
}
