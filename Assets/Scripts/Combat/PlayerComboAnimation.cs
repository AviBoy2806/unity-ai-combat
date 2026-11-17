using UnityEngine;
namespace GameCombat {
// Visual queue only. Never feeds Animator timing back into combat legality or damage.
public sealed class PlayerComboAnimation {
 readonly Animator animator;
 string request;
 string waiting; ulong attackId; bool hasAttack;
 public PlayerComboAnimation(Animator animator){this.animator=animator;}
 static bool IsLight(string name)=>name=="Attack 1" || name=="Attack 2" || name=="Attack 3";
 static string Trigger(string state)=>IsLight(state)?"Attack":state=="dodge"?"Dodge":"Heavy Attack";
 void ClearTriggers(){foreach(var name in new[]{"Attack","Heavy Attack","Dodge"})animator.ResetTrigger(name);}
 public void Changed(CombatActionRunner runner) {
 if(!animator)return;
 if(!animator.enabled || !animator.runtimeAnimatorController){Clear();return;}
 animator.speed=1; // Respect controller state speeds (including Attack 2's authored 1x).
 if(runner.Action==CombatAction.Attack){
 if(!hasAttack || runner.AttackId!=attackId){attackId=runner.AttackId;hasAttack=true;request=runner.Attack.animationState;waiting=null;ClearTriggers();SetLate(false);SetRecovery(false);}
 }else if(runner.Action==CombatAction.Dodge){request="dodge";waiting=null;ClearTriggers();SetLate(false);SetRecovery(false);}
 else if(runner.Action==CombatAction.Free){Clear();}
 else if(runner.Action==CombatAction.Stagger || runner.Action==CombatAction.Recoil || runner.Action==CombatAction.Dead){
 request=null;waiting=null;ClearTriggers();animator.SetBool("CombatComboHold",false);SetLate(false);SetRecovery(false);
 // Use configured reaction states when present; retain the animation-free/old-controller fallback.
 string reaction=runner.Action==CombatAction.Recoil?"Recoil":"HitReaction";
 if(runner.Action!=CombatAction.Dead && !animator.HasState(0,Animator.StringToHash(reaction)) && animator.HasState(0,Animator.StringToHash("GroundMovement")))animator.CrossFadeInFixedTime("GroundMovement",.08f);
 }
 }
 void SetRecovery(bool value){foreach(var p in animator.parameters)if(p.name=="CombatLateRecovery"){animator.SetBool("CombatLateRecovery",value);return;}}
 void SetLate(bool value){foreach(var p in animator.parameters)if(p.name=="CombatLateCombo"){animator.SetBool("CombatLateCombo",value);return;}}
 public void Clear(){request=null;waiting=null;if(animator){ClearTriggers();animator.SetBool("CombatComboHold",false);SetLate(false);SetRecovery(false);}}
 public void Tick(CombatActionRunner runner){
 if(!animator)return;
 if(!animator.enabled || !animator.runtimeAnimatorController){Clear();return;}
 var current=animator.GetCurrentAnimatorStateInfo(0);bool transition=animator.IsInTransition(0);
 var next=transition?animator.GetNextAnimatorStateInfo(0):default;
 if(waiting!=null && (current.IsName(waiting) || (transition && next.IsName(waiting)))){animator.ResetTrigger(Trigger(waiting));waiting=null;SetLate(false);SetRecovery(false);}
 string head=request;
 // A new chain must let the old visual return through its authored exit first.
 bool newChain=head=="Attack 1" || head=="Heavy Attack" || head=="dodge";
 bool hold=runner.Action==CombatAction.Attack && !newChain && (current.IsName("Attack 1") || current.IsName("Attack 2") || current.IsName("Attack 3") || current.IsName("Heavy Attack"));
 animator.SetBool("CombatComboHold",hold);
 SetRecovery(runner.Action==CombatAction.Free && AttackFacingPolicy.IsAttack(current) && current.normalizedTime>=1);
 if(waiting!=null || head==null || transition)return;
 bool ready=head=="Attack 2"?current.IsName("Attack 1"):head=="Attack 3"?current.IsName("Attack 2"):
 current.IsName("GroundMovement") || current.IsName("Locked On 0");
 if(!ready)return;
 if(!animator.HasState(0,Animator.StringToHash(head))){request=null;return;}
 request=null;waiting=head;SetLate(head=="Attack 2" && current.normalizedTime>.89677684f || head=="Attack 3" && current.normalizedTime>1.0001f);animator.SetTrigger(Trigger(head));
 }
}
}
