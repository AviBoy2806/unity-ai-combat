using Unity.VisualScripting;
using UnityEngine;

public class JumpState : PlayerState
{
    public override void Enter()
    {

        if (context.grounded)
        {
            if(context.animator) context.animator.ResetTrigger(JumpHash);
            if(context.animator) context.animator.SetTrigger(JumpHash);

            context.rb.AddForce(
                Vector3.up * context.jumpForce,
                ForceMode.Impulse
            );
        }
    }
    public override void Update()
    {
        if(context.rb.linearVelocity.y < 0f)
        {
            context.stateMachine.TryChangeState(this, PlayerStateType.fall);
            return;
        }
        
        if (!context.lockOn.enabled)
            context.RotateModel(context.GetCameraRelativeMove());
        else
            context.RotateModel(context.GetCameraForward());

    }

    public override void FixedUpdate()
    {
        context.ApplyAirMovement();
    }
}