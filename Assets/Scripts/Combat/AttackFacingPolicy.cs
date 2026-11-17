using UnityEngine;
namespace GameCombat {
public static class AttackFacingPolicy {
 public static bool IsAttack(AnimatorStateInfo s)=>s.IsName("Attack 1") || s.IsName("Attack 2") || s.IsName("Attack 3") || s.IsName("Heavy Attack");
 public static bool Locked(bool committed,Animator animator)=>committed || animator && animator.enabled && animator.runtimeAnimatorController && (IsAttack(animator.GetCurrentAnimatorStateInfo(0)) || animator.IsInTransition(0) && IsAttack(animator.GetNextAnimatorStateInfo(0)));
}
}
