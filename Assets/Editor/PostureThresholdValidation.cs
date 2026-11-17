using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameCombat;

[InitializeOnLoad]
public static class PostureThresholdValidation {
 static bool started;static int checks;
 static PostureThresholdValidation(){if(SessionState.GetBool("PostureThresholdValidation",false))EditorApplication.update+=Tick;}
 [MenuItem("Tools/Combat/Test posture threshold and early retreat")]
 public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("PostureThresholdValidation",true);EditorApplication.update+=Tick;EditorApplication.EnterPlaymode();}
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Debug.Log("PASS: "+message);}
 static void Tick(){if(started || !EditorApplication.isPlaying)return;started=true;SessionState.SetBool("PostureThresholdValidation",false);int code=0;try{Rules();Debug.Log($"POSTURE_THRESHOLD_VALIDATION_PASS {checks} checks");}catch(Exception e){Debug.LogException(e);code=1;}finally{if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}}
 static void Rules(){
 var brain=new ScriptedCombatBrain();var o=new CombatObservation{time=1,visible=true,grounded=true,dodgeReady=true,heavyReady=true,distance=1.9f,selfPosture=80,selfAction=CombatAction.Free};
 var intent=brain.Decide(o);Check(intent.localMove==Vector2.zero && intent.request==CombatRequest.None,"80 posture cannot automatically order retreat before full break");
 o.opponentAction=CombatAction.Attack;o.opponentPhase=AttackPhase.Windup;o.opponentAttackId=1;o.opponentElapsed=.4f;o.opponentWindup=.45f;o.opponentReach=2;intent=brain.Decide(o);
 Check(intent.guardHeld && intent.request==CombatRequest.None && intent.localMove==Vector2.zero,"high posture facing observed threat cannot automatically dodge backward");
 brain.Reset();o.opponentElapsed=0;intent=brain.Decide(o);Check(intent.guardHeld,"high posture guards on observable warning rather than always timing a deflection");
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Enemy/Enemy.prefab");Check(prefab,"real enemy prefab available");
 foreach(var origin in new[]{EnemyState.EnemyStateType.idle,EnemyState.EnemyStateType.wander,EnemyState.EnemyStateType.chase,EnemyState.EnemyStateType.combat}){
 var g=UnityEngine.Object.Instantiate(prefab);try{var e=g.GetComponent<enemy>();var a=g.GetComponent<Combatant>();var r=a.Runner;var host=g.GetComponent<EnemyBrainHost>();var motor=g.GetComponent<EnemyMotor>();e.stateMachine.ForceChangeState(origin);
 a.AddPosture(99,r.Clock);Check(!r.Broken && !e.IsStunned,"99 posture is below full threshold from "+origin);
 r.SetGuard(true);uint generation=host.Generation;a.AddPosture(1,r.Clock);
 Check(a.Posture==100 && r.Broken && e.IsStunned,"99+1 enters explicit StunnedState before AddPosture returns from "+origin);
 Check(r.Action==CombatAction.Stagger && !r.Guarding && !r.SamplesActive && e.rb.isKinematic && motor.Owner==EnemyMotor.MovementOwner.Stunned,"full threshold cancels all actions and owns stationary body from "+origin);
 Check(host.Generation>generation && host.Intent.localMove==Vector2.zero && !host.EscapePending,"full threshold clears old brain commands from "+origin);
 float time=r.Remaining;r.Tick(.02f);float elapsed=r.Elapsed;a.AddPosture(1,r.Clock);Check(r.Elapsed==elapsed && r.Remaining<time,"additional posture cannot restart full break from "+origin);
 for(int i=1;i<175;i++){r.Collect();r.Tick(.02f);r.Move();}Check(!e.IsStunned && !r.Broken && a.Posture<=40,"full threshold releases only through existing shared timer from "+origin);
 }finally{UnityEngine.Object.DestroyImmediate(g);}}
 // Four real deflections fill attacker posture. Only the fourth is a full stun.
 var attackerObject=UnityEngine.Object.Instantiate(prefab);var defenderObject=new GameObject("near-full deflecting defender");
 try{var a=attackerObject.GetComponent<Combatant>();var d=defenderObject.AddComponent<Combatant>();a.transform.position=Vector3.zero;d.transform.position=Vector3.forward*1.3f;d.facing.rotation=Quaternion.Euler(0,180,0);d.AddPosture(99,0);
 for(int i=0;i<4;i++){Check(a.Runner.TryStart(CombatRequest.Light,Vector3.forward)==RequestResult.Accepted,"deflection source attack accepted "+i);a.Runner.Tick(a.light.Windup+.01f);d.Runner.SetGuard(true);var contact=CombatResolver.Evaluate(a,d,a.light,a.transform.position);CombatResolver.ApplyBatch(new System.Collections.Generic.List<ResolvedContact>{contact},a.Runner.Clock);
 Check(contact.outcome==CombatOutcome.Deflect && d.Health.Health==100 && d.Posture==99 && !d.Runner.Broken,"successful deflection leaves near-full defender unharmed "+i);
 Check(a.Posture==25*(i+1) && !a.Runner.SamplesActive && (i<3?a.Runner.Action==CombatAction.Recoil && !a.Runner.Broken:a.Runner.Broken && attackerObject.GetComponent<enemy>().IsStunned),"only full attacker posture upgrades deflection recoil to StunnedState "+i);
 if(i<3)a.Runner.Tick(a.tuning.recoil);d.Runner.SetGuard(false);d.Runner.Tick(.4f);
 }
 }finally{UnityEngine.Object.DestroyImmediate(attackerObject);UnityEngine.Object.DestroyImmediate(defenderObject);}
 // Fractional accumulation must not need a second contact once maximum is reached.
 var plain=new GameObject("shared posture threshold");try{var a=plain.AddComponent<Combatant>();a.AddPosture(float.NaN,0);a.AddPosture(-1,0);Check(a.Posture==0 && !a.Runner.Broken,"invalid/negative posture ignored");a.AddPosture(99.9f,0);a.AddPosture(.2f,0);Check(a.Posture==a.tuning.maxPosture && a.Runner.Broken,"fractional overflow clamps and breaks atomically");a.Health.takeDamage(1000);Check(a.Runner.Action==CombatAction.Dead && !a.Runner.Broken,"death still overrides full threshold");}finally{UnityEngine.Object.DestroyImmediate(plain);}
 }
}
