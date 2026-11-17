using UnityEngine;

[RequireComponent(typeof(Animator))]
public class RootMotionHandler : MonoBehaviour
{
    private Animator anim;
    private Player player;

    [Header("Position Correction")]
    [SerializeField] private float soleClearance = 0.035f;
    [SerializeField] private LayerMask footGroundMask = 1 << 7;
    private float footLift;
    public void ResetEpisodeState(){ClearFootLift();}

    private void Awake()
    {
        anim = GetComponent<Animator>();
        player = GetComponentInParent<Player>();
    }

    private static bool IsCombatClip(AnimatorStateInfo state)
    {
        return state.IsName("Attack 1") || state.IsName("Attack 2") ||
               state.IsName("Attack 3") || state.IsName("Heavy Attack") || state.IsName("dodge");
    }


    private bool HeavyVisualActive()
    {
        return anim && anim.isHuman && (anim.GetCurrentAnimatorStateInfo(0).IsName("Heavy Attack") ||
            (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName("Heavy Attack")));
    }
    private void ClearFootLift()
    {
        if (player && player.model && footLift > 0) player.model.position -= Vector3.up * footLift;
        footLift=0;
    }
    private void LateUpdate()
    {
        ClearFootLift();
        if (!player || !player.model || !HeavyVisualActive()) return;
        // Correct the visible humanoid pose, never the capsule or Rigidbody.
        foreach(var bone in new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightToes})
        {
            var foot=anim.GetBoneTransform(bone); if(!foot)continue;
            if(Physics.Raycast(foot.position+Vector3.up,Vector3.down,out var ground,2f,footGroundMask,QueryTriggerInteraction.Ignore))
                {
                float soleHeight = bone == HumanBodyBones.LeftFoot ? anim.leftFeetBottomHeight :
                    bone == HumanBodyBones.RightFoot ? anim.rightFeetBottomHeight : 0;
                footLift=Mathf.Max(footLift,ground.point.y+soleClearance+soleHeight-foot.position.y);
            }
        }
        if(footLift>0)player.model.position+=Vector3.up*footLift;
    }
    private void OnDisable(){ClearFootLift();}

    private void OnAnimatorMove()
    {
        if (player == null || Time.deltaTime <= 0) return;
        if (player.combat && player.combat.Actor.Runner.Action == GameCombat.CombatAction.Attack)
        {
            if (player.useRootMotion.xzPlayer)
                player.combat.AccumulateAttackRootMotion(anim.deltaPosition);
            return; // Vertical gameplay movement remains physics-owned.
        }
        if (player.combat && player.combat.Actor.Runner.OwnsMovement) return;

        // The action timer can finish before the outgoing animation blend does.
        // Never let that attack/dodge clip resume moving the body after ownership ends.
        if (player.combat && (IsCombatClip(anim.GetCurrentAnimatorStateInfo(0)) ||
            (anim.IsInTransition(0) && IsCombatClip(anim.GetNextAnimatorStateInfo(0))))) return;
        if (player.stateMachine?.currentState is IdleState) return;

        var settings = player.useRootMotion;

        Vector3 delta = anim.deltaPosition;
        Vector3 velocity = delta / Time.deltaTime;

        // Player Rigidbody
        if (settings.xzPlayer)
            player.SetVelocity(x: velocity.x, z: velocity.z);

        if (settings.yPlayer)
            player.SetVelocity(y: velocity.y);
    }
}

