using UnityEngine;

public class StateRootMotion : StateMachineBehaviour
{
    public Player.UseRootMotion useRootMotion;

    private Player player;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        player = animator.GetComponentInParent<Player>();

        if (player != null)
        {
            player.rootMotionOwner = this;
            player.useRootMotion = useRootMotion;
        }
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (player != null && player.rootMotionOwner == this)
        {
            player.rootMotionOwner = null;
            player.useRootMotion = default;
        }
    }
}