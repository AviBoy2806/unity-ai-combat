using UnityEngine;

public class IdleState : PlayerState
{
    public float dampTime = 0.1f;
    public override void Enter()
    {
        
        context.SetVelocity(x: 0f, z: 0f);
    }

    public override void Update()
    {
        if(HandleTransition()) return;

        if (!context.lockOn.enabled)
            context.RotateModel(context.GetCameraRelativeMove());
        else
            context.RotateModel(context.GetCameraForward());
    }

    bool HandleTransition()
    {
                SetDampedFloat(SpeedHash, 0f, 0.1f);
        
        if (context.input.jumpTriggered)
        {
            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.jump
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

        if (context.input.movement != Vector2.zero)
        {
            if (context.input.runPressed && !context.input.runJustPressed)
            {
                context.stateMachine.TryChangeState(
                this,
                PlayerStateType.run
                );
                return true;
            }

            context.stateMachine.TryChangeState(
                this,
                PlayerStateType.walk
            );
            return true;
        }
        return false;
    }
}