using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

// Runs the saved GameScene with its ordinary Update/FixedUpdate/Animator/physics loop.
// Unlike the prefab fixture, this does not manually tick the combat runner.
[InitializeOnLoad]
public static class GameSceneStunValidation {
 static int stage, checks, punishHits; static double due, brokenAt, highAt, sampleAt=-1, attackAt; static Vector3 start, modelStart, hipsStart;
 static float rootShift, modelShift, hipsShift, animationProgress; static enemy body; static Combatant source, actor;
 static bool done; static bool errors; static int stunTicks;
 static GameSceneStunValidation(){if(SessionState.GetBool("GameSceneStunValidation",false))Attach();}
 static void Attach(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;Application.logMessageReceived-=Log;Application.logMessageReceived+=Log;}
 [MenuItem("Tools/Combat/Test stun in saved GameScene")]
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");SessionState.SetBool("GameSceneStunValidation",true);Attach();EditorApplication.EnterPlaymode();}
 static void Log(string text,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors=true;}
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("PASS: "+message);}
 static Transform Hips()=>body.animator.transform.Find("mixamorig:Hips");
 static void Contact(){var hit=CombatResolver.Evaluate(source,actor,source.light,source.transform.position);CombatResolver.ApplyBatch(new List<ResolvedContact>{hit},actor.Runner.Clock);Debug.Log($"SCENE_CONTACT outcome={hit.outcome} posture={actor.Posture:R} broken={actor.Runner.Broken} state={body.CurrentStateName} pos={body.transform.position}");}
 static void OrdinaryGuard(){actor.Runner.SetGuard(false);actor.Runner.SetGuard(true,false);actor.Runner.SetGuard(true,true);}
 static void Finish(int code){done=true;SessionState.SetBool("GameSceneStunValidation",false);EditorApplication.update-=Tick;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
 static void Tick(){if(done || !EditorApplication.isPlaying)return;try{
 if(!body){body=UnityEngine.Object.FindFirstObjectByType<enemy>();var p=UnityEngine.Object.FindFirstObjectByType<Player>();if(!body || !p)return;actor=body.GetComponent<Combatant>();source=p.GetComponent<Combatant>();due=EditorApplication.timeSinceStartup+.5;}
 if(EditorApplication.timeSinceStartup<due)return;
 var r=actor.Runner;var host=body.GetComponent<EnemyBrainHost>();
 switch(stage){
 case 0:
  if(!body.CheckGround() || r.Action!=CombatAction.Free)break;
  Check(body.gameObject.scene.path=="Assets/Scenes/GameScene.unity","actual saved GameScene enemy loaded");
  Check(body.animator && body.animator.HasState(0,Animator.StringToHash("Stunned")),"scene Animator has Stunned state");
  // Put source in front of the real scene enemy. Keep original terrain/NavMesh and components.
  var pos=body.transform.position+body.transform.forward*1.3f;var player=source.GetComponent<Player>();pos.y=body.enemyCollider.bounds.min.y+(player.transform.position.y-player.playerCollider.bounds.min.y);player.rb.position=pos;player.transform.position=pos;player.rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();
  Check(host.ActiveMode==EnemyControlMode.Normal && !host.DebugOwned,"actual Normal brain active, defense debug off, no debug owner");
  body.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; // Track bones even when outside the game camera.
  actor.Result+=hit=>{if(brokenAt<=0 && hit.defenderId==actor.Id && hit.outcome==CombatOutcome.PostureBreak){brokenAt=r.Clock;Check(hit.actualHealthDamage==0,"actual player hit volume breaks ordinary guard without health damage");}};
  stage++;break;
 case 1:
  if(r.Action!=CombatAction.Free)break;
  OrdinaryGuard();for(int i=0;i<4;i++){Contact();Check(!r.Broken,"ordinary light block "+(i+1)+" does not break early");}
  Check(actor.Posture==80,"real saved block values fill posture to 80");start=body.transform.position;highAt=r.Clock;stage++;break;
 case 2:
  if(r.Clock-highAt<.3)break;
  Check(r.Action!=CombatAction.Dodge && host.Intent.localMove.y>=0 && Vector3.ProjectOnPlane(body.transform.position-start,Vector3.up).magnitude<.001f,"Normal scene enemy no longer retreats/dodges automatically at 80 posture");
  var attackingPlayer=source.GetComponent<Player>();if(source.Runner.Action!=CombatAction.Free || attackingPlayer.AttackFacingLocked || !attackingPlayer.IsGrounded())break;
  attackingPlayer.model.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(body.transform.position-source.transform.position,Vector3.up));
  attackingPlayer.inputComponent.QueueCombat(CombatRequest.Light);attackAt=r.Clock;stage++;break;
 case 3:
  if(!r.Broken){if(r.Clock-attackAt>3)throw new Exception($"actual player light never filled posture: posture={actor.Posture:R} enemy={r.Action} player={source.Runner.Action} {source.GetComponent<PlayerCombatAdapter>().InputDiagnostic}");break;}
  Check(r.Broken && body.IsStunned && body.CurrentStateName=="EnemyStunnedState","fifth actual light block immediately enters full StunnedState");
  Check(actor.Posture==actor.tuning.maxPosture && body.rb.isKinematic,"full posture owns stationary scene Rigidbody");
  Check(brokenAt>0,"full break came from the actual CombatWorld hit volume, not direct posture injection");due=EditorApplication.timeSinceStartup+.06;stage++;break;
 case 4:
  Check(body.animator.GetCurrentAnimatorStateInfo(0).IsName("Stunned"),"real frame evaluates Stunned animation");
  start=body.transform.position;modelStart=body.animator.transform.position;hipsStart=Hips().position;stage++;goto case 5;
 case 5:
  if(r.Clock==sampleAt)break;sampleAt=r.Clock;
  if(r.Broken){stunTicks++;rootShift=Mathf.Max(rootShift,Vector3.Distance(start,body.transform.position));modelShift=Mathf.Max(modelShift,Vector3.Distance(modelStart,body.animator.transform.position));hipsShift=Mathf.Max(hipsShift,Vector3.ProjectOnPlane(Hips().position-hipsStart,Vector3.up).magnitude);animationProgress=Mathf.Max(animationProgress,body.animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
   if(!body.IsStunned || !body.rb.isKinematic || r.Guarding || r.SamplesActive || host.Intent.localMove!=Vector2.zero || !body.animator.GetCurrentAnimatorStateInfo(0).IsName("Stunned"))throw new Exception("scene stun ownership/action/animation gate failed");
   if(punishHits<3 && r.Clock-brokenAt> .5+punishHits){float elapsed=r.Elapsed;Contact();punishHits++;Check(r.Elapsed==elapsed,"punish hit does not reset/shorten stun "+punishHits);}
   break;
  }
  Check(stunTicks>=150 && punishHits==3,"normal scene physics sustained full stun and three health hits");
  Check(animationProgress>.5f,"original Generic-rig stun animation actually advances in scene: "+animationProgress);
  Check(rootShift<.001f && modelShift<.001f && hipsShift<.1f,$"scene root/model/hips stay put: {rootShift:R}/{modelShift:R}/{hipsShift:R}");
  Check(r.Clock-brokenAt>=3.49 && !body.IsStunned && !body.rb.isKinematic,"scene exits only after full 3.5 seconds");
  Check(actor.Health.Health==70 && actor.Posture<=40,"scene punish damage and posture reset correct");
  Check(!errors,"no main-project Unity errors");Debug.Log($"GAME_SCENE_STUN_VALIDATION_PASS {checks} checks ticks={stunTicks} root={rootShift:R} model={modelShift:R} hips={hipsShift:R}");Finish(0);break;
 }
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
