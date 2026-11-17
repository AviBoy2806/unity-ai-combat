using UnityEngine;
using GameCombat;
// Full posture break. The shared runner alone owns its duration; no AI or navigation runs here.
public sealed class EnemyStunnedState : EnemyState {
 public override void Enter(){
 canExit=false;
 context.GetComponent<EnemyPerceptionNav>()?.StopMoving();
 context.GetComponent<EnemyBrainHost>()?.EnterStunned();
 context.GetComponent<EnemyMotor>()?.SetStunned(true);
 var anim=context.animator;
 if(anim && anim.runtimeAnimatorController){anim.SetFloat(SpeedHash,0);anim.ResetTrigger(AttackHash);anim.SetBool("Blocking",false);anim.SetBool("Stunned",true);if(anim.HasState(0,Animator.StringToHash("Stunned")))anim.Play("Stunned",0,0);}
 }
 public override void Exit(){
 context.GetComponent<EnemyMotor>()?.SetStunned(false);
 context.GetComponent<EnemyBrainHost>()?.ExitStunned();
 if(context.animator && context.animator.runtimeAnimatorController){context.animator.ResetTrigger(AttackHash);context.animator.SetBool("Stunned",false);}
 canExit=true;
 }
 public override void Update(){if(context.animator && context.animator.runtimeAnimatorController)context.animator.SetFloat(SpeedHash,0);}
}
