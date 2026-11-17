using UnityEngine;

public class DeadState : PlayerState
{
    public override void Enter()
    {
        context.SetVelocity(x: 0f, z: 0f);
        if(context.animator) context.animator.SetTrigger(DieHash);
    }
}