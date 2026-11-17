using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using GameCombat;
using GameCombat.ML;
using GameCombat.AgentContract;

public class RuntimeFailurePolicy : MonoBehaviour,ICombatPolicy {
 public bool CanActivate(string hash,out string reason){reason="Injected inference failure fixture";return true;}
 public void Decide(DecisionFrame frame,Action<uint,uint,int,int> callback){throw new InvalidOperationException("fixture inference failure");}
 public void Deactivate(){}
}

// Actual GameScene/Academy loop followed by scoped fault and presentation checks.
// No trainer. All scene overrides are unsaved. Explicit tick and wall deadlines.
[InitializeOnLoad]
public static class RuntimeIntegrationValidation {
 [Serializable] class InferenceBinding {public string inferenceMode;}
 static EnemyBrainHost ml,normal;static Combatant player;static EnemyLearningAgent policy;
 static int ticks,checks,loads,normalResults,mlResults;static bool attached,done;static double deadline;
 static readonly HashSet<string> deliveries=new();
 static RuntimeIntegrationValidation(){if(SessionState.GetBool("RuntimeIntegrationValidation",false)){deadline=EditorApplication.timeSinceStartup+75;EditorApplication.update+=Update;}}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");SessionState.SetBool("RuntimeIntegrationValidation",true);deadline=EditorApplication.timeSinceStartup+75;EditorApplication.update+=Update;EditorApplication.EnterPlaymode();}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;Debug.Log("RUNTIME_CHECK "+message);}
 static object Invoke(object value,string method,params object[] args)=>value.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);
 static void Update(){if(done)return;try{
  if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Runtime integration 75-second deadline");
  if(!EditorApplication.isPlaying || attached)return;
  Time.timeScale=1;Time.fixedDeltaTime=.02f;Time.captureDeltaTime=.02f;
  ml=UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>();var p=UnityEngine.Object.FindFirstObjectByType<Player>();if(!ml || !p)return;
  Check(ml.mode==EnemyControlMode.Normal && ml.ActiveMode==EnemyControlMode.Normal,"saved GameScene starts Normal");
  if(loads==1){Check(!Academy.Instance.IsCommunicatorOn,"scene reload still offline");Debug.Log($"RUNTIME_INTEGRATION_VALIDATION_PASS checks={checks} liveTicks={ticks} NormalResults={normalResults} MLResults={mlResults}; numerical, no visual acceptance");Finish(0);return;}
  player=p.GetComponent<Combatant>();p.GetComponent<UnityEngine.InputSystem.PlayerInput>().enabled=false;
  policy=ml.agentPolicy as EnemyLearningAgent;Check(policy && policy.runtimeEvaluationOnly && !policy.testHeuristic && !policy.trainingMode,"gameplay adapter evaluation-only, no heuristic/training");
  var sourceActor=ml.GetComponent<Combatant>();System.IO.File.WriteAllText("Logs/RuntimeIntegration20261117/runtime-tuning.json",JsonUtility.ToJson(sourceActor.tuning)+"\n"+JsonUtility.ToJson(sourceActor.light)+"\n"+JsonUtility.ToJson(sourceActor.heavy));
  Check(policy.CanActivate(ml.AgentTuningHash,out var reason),"candidate contract/identity/movement metadata: "+reason+" runtime tuning="+ml.AgentTuningHash);
  var b=policy.GetComponent<BehaviorParameters>();bool expectedDeterministic=JsonUtility.FromJson<InferenceBinding>(policy.modelMetadata.text).inferenceMode!="sampled";Check(b.BehaviorType==BehaviorType.InferenceOnly && b.InferenceDevice==InferenceDevice.Burst && b.DeterministicInference==expectedDeterministic,"InferenceOnly CPU Burst matches bound inference-mode metadata");
  normal=UnityEngine.Object.Instantiate(ml.gameObject).GetComponent<EnemyBrainHost>();normal.name="Normal coexistence fixture";
  // Both existing actor instances share the same player, faction rules, arena and resolver.
  Place(ml,p.transform.position+Vector3.forward*2.1f,p);Place(normal,p.transform.position+Vector3.right*2.1f,p);
  Check(ml.GetComponent<Combatant>().arenaId==player.arenaId && normal.GetComponent<Combatant>().arenaId==player.arenaId,"both enemies share player arena");
  Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Applied,"explicit candidate selection");
  player.Result+=OnResult;CombatWorld.AfterStep+=Sample;attached=true;
 }catch(Exception e){Fail(e);}}
 static void Place(EnemyBrainHost host,Vector3 position,Player p){var body=host.GetComponent<enemy>();var col=host.GetComponent<Collider>();position.y=p.playerCollider.bounds.min.y+(host.transform.position.y-col.bounds.min.y);host.GetComponent<EnemyMotor>().Teleport(position,Quaternion.LookRotation(Vector3.ProjectOnPlane(p.transform.position-position,Vector3.up)));var perception=host.GetComponent<EnemyPerceptionNav>();perception.target=p.transform;Physics.SyncTransforms();}
 static void OnResult(CombatResult result){if(result.defenderId!=player.Id)return;Check(deliveries.Add(result.attackerId+":"+result.attackInstanceId),"shared player result delivered once per attack");if(result.attackerId==ml.GetComponent<Combatant>().Id)mlResults++;if(result.attackerId==normal.GetComponent<Combatant>().Id)normalResults++;}
 static void Sample(){if(done)return;try{
  ticks++;if(ticks<300 && !player.Dead)return;
  CombatWorld.AfterStep-=Sample;player.Result-=OnResult;
  Check(policy.DecisionCount>=5 && ml.ActiveMode==EnemyControlMode.ML && string.IsNullOrEmpty(ml.FallbackReason),"real Academy callbacks meet runtime deadline without fallback, decisions="+policy.DecisionCount);
  Check(normal.ActiveMode==EnemyControlMode.Normal && normal.InCombat,"Normal enemy coexists in combat against same player");
  Check(!Academy.Instance.IsCommunicatorOn && !CommunicatorFactory.Enabled,"no Python communicator");
  Check(!ml.DebugOwned && !normal.DebugOwned,"debug controls do not compete");
  Debug.Log($"RUNTIME_LIVE ticks={ticks} normalResults={normalResults} mlResults={mlResults} playerDead={player.Dead}; policy contact success is not required for loading acceptance");
  UnityEngine.Object.FindFirstObjectByType<CombatWorld>().enabled=false;
  FaultsAndSounds();loads=1;attached=false;EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/GameScene.unity",new LoadSceneParameters(LoadSceneMode.Single));
 }catch(Exception e){Fail(e);}}
 static void Free(Combatant a){int budget=500;while(a.Runner.Action!=CombatAction.Free && !a.Dead && budget-->0)a.Runner.Tick(.02f);Check(!a.Dead && budget>0,"bounded commitment drain");a.Runner.SetGuard(false);}
 static void FaultsAndSounds(){
  var actor=ml.GetComponent<Combatant>();ml.enabled=false;normal.enabled=false;Free(actor);ml.SetMode(EnemyControlMode.Normal);
  var obs=new CombatObservation{visible=true,grounded=true,relativePosition=Vector3.forward*2,opponentHealth=1,opponentForward=Vector3.back};
  var behavior=policy.GetComponent<BehaviorParameters>();var model=behavior.Model;
  // Simulate a missing serialized asset reference. The package Model setter reloads
  // its policy immediately and must not be used to remove a model during Play Mode.
  var modelField=typeof(BehaviorParameters).GetField("m_Model",BindingFlags.Instance|BindingFlags.NonPublic);
  modelField.SetValue(behavior,null);Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && ml.ActiveMode==EnemyControlMode.Normal && ml.mode==EnemyControlMode.ML && ml.FallbackReason.Contains("missing"),"missing serialized model fallback visibly preserves selected ML / active Normal");modelField.SetValue(behavior,model);
  var metadata=policy.modelMetadata;policy.modelMetadata=new TextAsset("{ invalid metadata");Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable,"invalid metadata safe fallback");UnityEngine.Object.DestroyImmediate(policy.modelMetadata);policy.modelMetadata=metadata;
  var motor=actor.GetComponent<EnemyMotor>();float speed=motor.combatMoveSpeed;motor.combatMoveSpeed+=1;Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && ml.FallbackReason.Contains("movement"),"movement mismatch rejected");motor.combatMoveSpeed=speed;
  var tuning=actor.tuning;actor.tuning=UnityEngine.Object.Instantiate(tuning);actor.tuning.guardAngle+=5;Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && ml.FallbackReason.Contains("tuning"),"changed defense tuning rejected");UnityEngine.Object.DestroyImmediate(actor.tuning);actor.tuning=tuning;
  string payload=policy.compatiblePayloadSha256;policy.compatiblePayloadSha256="invalid";Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && ml.FallbackReason.Contains("payload"),"invalid imported model payload rejected");policy.compatiblePayloadSha256=payload;
  var inferenceFlag=typeof(BehaviorParameters).GetField("m_DeterministicInference",BindingFlags.Instance|BindingFlags.NonPublic);bool inferenceValue=behavior.DeterministicInference;inferenceFlag.SetValue(behavior,!inferenceValue);Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable && ml.FallbackReason.Contains("inference mode"),"changed inference mode rejected by matching metadata");inferenceFlag.SetValue(behavior,inferenceValue);
  float dt=Time.fixedDeltaTime;Time.fixedDeltaTime=.04f;Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable,"fixed timestep mismatch rejected");Time.fixedDeltaTime=dt;
  policy.enabled=false;Check(ml.SetMode(EnemyControlMode.ML)==BrainSwitchResult.Unavailable,"disabled adapter safe fallback");policy.enabled=true;
  var failure=ml.gameObject.AddComponent<RuntimeFailurePolicy>();ml.agentPolicy=failure;ml.SetMode(EnemyControlMode.ML);Invoke(ml,"CollectAgent",obs,true,true);Check(ml.ActiveMode==EnemyControlMode.Normal && ml.FallbackReason.Contains("fixture inference failure"),"thrown inference failure reason retained after fallback");
  ml.SetMode(EnemyControlMode.ML);actor.Runner.TryStart(CombatRequest.Heavy,actor.facing.forward);Invoke(ml,"CollectAgent",obs,true,true);Check(ml.HasPendingSwitch && actor.Runner.Action==CombatAction.Attack,"inference failure during commitment defers fallback without cancellation");Free(actor);Invoke(ml,"Collect");Check(ml.ActiveMode==EnemyControlMode.Normal && ml.FallbackReason.Contains("fixture inference failure"),"deferred failure reason survives Normal activation");
  ml.SetMode(EnemyControlMode.Normal);Check(ml.FallbackReason=="","explicit Normal clears fallback diagnostic");
  var delayed=ml.gameObject.AddComponent<ContractHeuristicPolicy>();delayed.holdCallback=true;ml.agentPolicy=delayed;ml.SetMode(EnemyControlMode.ML);Invoke(ml,"CollectAgent",obs,true,true);var frame=delayed.LastFrame;var callback=delayed.LastCallback;
  actor.Runner.Tick(.02f);Invoke(ml,"CollectAgent",obs,true,true);Check(ml.ActiveMode==EnemyControlMode.Normal && ml.FallbackReason.Contains("designated tick"),"missed callback fallback");callback(frame.generation,frame.sequence,0,2);Check(actor.Runner.Action==CombatAction.Free,"stale callback cannot start attack");
  for(int i=0;i<8;i++){ml.SetMode(EnemyControlMode.ML);Invoke(ml,"CollectAgent",obs,true,true);frame=delayed.LastFrame;callback=delayed.LastCallback;uint generation=ml.Generation;ml.enabled=true;ml.enabled=false;callback(frame.generation,frame.sequence,0,2);Check(ml.Generation!=generation && actor.Runner.Action==CombatAction.Free,"repeated host enable/disable invalidates callback "+i);}
  ml.agentPolicy=policy;ml.SetMode(EnemyControlMode.ML);actor.Runner.TryStart(CombatRequest.Heavy,actor.facing.forward);float health=actor.Health.Health,posture=actor.Posture;uint gen=ml.Generation;
  Check(ml.SetMode(EnemyControlMode.Normal)==BrainSwitchResult.Deferred && ml.Generation==gen && actor.Runner.Action==CombatAction.Attack,"Normal switch deferred through heavy commitment");Free(actor);ml.GetType().GetMethod("Collect",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ml,null);Check(ml.ActiveMode==EnemyControlMode.Normal && actor.Health.Health==health && actor.Posture<=posture,"switch preserves health, natural posture recovery allowed");
  // Shared volume/dedup test with both enemy identities and same target, independent of policy quality.
  var target=new GameObject("shared target fixture");var defender=target.AddComponent<Combatant>();target.AddComponent<BoxCollider>().center=Vector3.up;target.AddComponent<Hurtbox>().owner=defender;defender.faction=0;
  foreach(var host in new[]{ml,normal}){var source=host.GetComponent<Combatant>();Free(source);source.Runner.TryStart(CombatRequest.Light,source.facing.forward);var contact=CombatResolver.Evaluate(source,defender,source.light,source.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{contact},source.Runner.Clock);}
  Check(defender.Health.Health==76,"Normal and ML actors apply identical 12 damage via same shared resolver");UnityEngine.Object.DestroyImmediate(target);
  Sounds(actor);
  AnimationHooks(actor);
 }
 static void AnimationHooks(Combatant actor){
  Free(actor);var anim=actor.GetComponentInChildren<Animator>();var original=anim.runtimeAnimatorController;
  var controller=UnityEngine.Object.Instantiate(original) as UnityEditor.Animations.AnimatorController;
  foreach(var name in new[]{"CombatStagger","CombatRecoil","CombatDodge"}){bool exists=false;foreach(var parameter in controller.parameters)if(parameter.name==name)exists=true;if(!exists)controller.AddParameter(name,AnimatorControllerParameterType.Bool);}
  anim.runtimeAnimatorController=controller;var p=actor.GetComponent<CombatPresentation>();
  actor.Runner.SetGuard(true);p.SendMessage("Update");Check(anim.GetBool("Blocking"),"optional guard bool follows shared guard");actor.Runner.SetGuard(false);
  actor.Runner.Interrupt(.2f,CombatAction.Stagger);p.SendMessage("Update");Check(anim.GetBool("CombatStagger"),"optional stagger bool follows authoritative action");Free(actor);p.SendMessage("Update");Check(!anim.GetBool("CombatStagger") && !anim.GetBool("Blocking"),"optional reaction/guard bools clear at completion");
  actor.Runner.Interrupt(.2f,CombatAction.Recoil);p.SendMessage("Update");Check(anim.GetBool("CombatRecoil"),"optional recoil bool follows shared timer");Free(actor);actor.Runner.Tick(1);actor.Runner.TryStart(CombatRequest.Dodge,Vector3.back);p.SendMessage("Update");Check(anim.GetBool("CombatDodge"),"optional dodge bool follows shared dodge");
  p.enabled=false;Check(!anim.GetBool("CombatDodge"),"presentation disable clears optional animation bools");p.enabled=true;Free(actor);anim.enabled=false;actor.Runner.TryStart(CombatRequest.Light,actor.facing.forward);Free(actor);Check(actor.Runner.Action==CombatAction.Free,"disabled Animator with presentation hooks cannot hang combat");anim.enabled=true;anim.runtimeAnimatorController=original;UnityEngine.Object.DestroyImmediate(controller);
 }
 static void Sounds(Combatant actor){
  Free(actor);var p=actor.GetComponent<CombatPresentation>();var clip=AudioClip.Create("synthetic test pulse",128,1,8000,false);p.lightAttackSound=p.heavyAttackSound=p.dodgeSound=p.hitSound=p.blockSound=p.deflectSound=p.evadeSound=p.postureBreakSound=p.deathSound=clip;
  int count=p.SoundPlayCount;actor.Runner.TryStart(CombatRequest.Light,actor.facing.forward);p.SendMessage("Changed");Check(p.SoundPlayCount==count+1,"action sound once per accepted attack despite duplicate Changed");Free(actor);
  var result=new CombatResult{attackerId=901,defenderId=actor.Id,attackInstanceId=42,outcome=CombatOutcome.Block};count=p.SoundPlayCount;actor.Publish(result);actor.Publish(result);Check(p.SoundPlayCount==count+1,"duplicate result callback plays one defender sound");
  p.enabled=false;p.enabled=true;actor.Publish(result);Check(p.SoundPlayCount==count+1,"reenable cannot replay prior result");result.attackInstanceId++;actor.Publish(result);Check(p.SoundPlayCount==count+2,"new attack can play next result sound");
  p.lightAttackSound=null;p.heavyAttackSound=null;Free(actor);count=p.SoundPlayCount;actor.Runner.TryStart(CombatRequest.Light,actor.facing.forward);Free(actor);Check(p.SoundPlayCount==count && actor.Runner.Action==CombatAction.Free,"missing sound clips do not affect combat completion");UnityEngine.Object.DestroyImmediate(clip);
 }
 static void Fail(Exception e){Debug.LogException(e);Finish(1);}
 static void Finish(int code){done=true;CombatWorld.AfterStep-=Sample;SessionState.SetBool("RuntimeIntegrationValidation",false);if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
}

