using UnityEngine;

public class RunState : PlayerState
{
    public float dampTime = 0.1f;

    public override void Update()
    {
        SetDampedFloat(SpeedHash, 1, 0.1f);
        if(HandleTransition()) return;

        if (!context.lockOn.enabled)
            context.RotateModel(context.GetCameraRelativeMove());
        else
            context.RotateModel(context.GetCameraForward());
        
    }

    public override void FixedUpdate()
    {
        Vector3 move =
            context.GetCameraRelativeMove() * context.runSpeed;

        context.SetVelocity(x: move.x, z: move.z);
    }

    private bool HandleTransition()
    {

        if (context.input.jumpTriggered)
        {
            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.jump
            );
            return true;
        }

        if (context.input.movement == Vector2.zero)
        {
            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.idle
            );
            return true;
        }
        if (!context.input.runPressed)
        {
            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.walk
            );
            return true;
        }
        if (context.input.attackJustPressed)
        {
            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.heavyAttack
            );
            return true;
        }

        return false;
    }
}