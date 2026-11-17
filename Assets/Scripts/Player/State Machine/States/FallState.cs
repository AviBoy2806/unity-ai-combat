using UnityEngine;

public class FallState : PlayerState
{

    public override void Exit()
    {
        context.input.jumpTriggered = false;
    }

    public override void Update()
    {
        if (context.grounded && context.rb.linearVelocity.y <= 0f)
        {
            if (context.input.attackJustPressed)
            {
                context.stateMachine.TryChangeState(this, PlayerStateType.attack);
            }

            else if(context.input.movement == Vector2.zero)
            {    
                context.stateMachine.TryChangeState(
                    this,
                    PlayerStateType.idle
                );
            }

            else if (context.input.runJustPressed)
            {
                context.stateMachine.TryChangeState(this, PlayerStateType.dodge);
            }
            else if (context.input.runPressed)
            {
                context.stateMachine.TryChangeState(this, PlayerStateType.run);
            }
            else
            {
                context.stateMachine.TryChangeState(this, PlayerStateType.walk);
            }

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