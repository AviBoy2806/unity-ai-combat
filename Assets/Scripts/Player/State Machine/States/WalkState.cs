using UnityEngine;

public class WalkState : PlayerState
{
    public float dampTime = 0.1f;

    public override void Enter()
    {
        context.lockOn.movementAnimationMultiplier = 0.5f;
    }

    public override void Exit()
    {
        context.lockOn.movementAnimationMultiplier = 1f;
    }

    public override void Update()
    {
        SetDampedFloat(SpeedHash, 0.5f, 0.1f);
        if(HandleTransition()) return;

        if (!context.lockOn.enabled)
            context.RotateModel(context.GetCameraRelativeMove());
        else
            context.RotateModel(context.GetCameraForward());
    }

    public override void FixedUpdate()
    {
        Vector3 move =
            context.GetCameraRelativeMove() * context.walkSpeed;

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
        if (context.input.attackJustPressed)
        {
            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.attack
            );
            
            return true;
        }

        if (context.input.runJustPressed)
        {
            context.stateMachine.TryChangeState(
            this,
            PlayerStateType.dodge
            );
            return true;
        }

        if (context.input.runPressed && !context.input.runJustPressed)
        {
            context.stateMachine.TryChangeState(
            this,
            PlayerStateType.run
            );
            return true;
        }

        return false;
    }
}