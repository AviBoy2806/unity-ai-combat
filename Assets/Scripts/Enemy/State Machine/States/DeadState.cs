using UnityEngine;

public class EnemyDeadState : EnemyState
{
    public override void Enter()
    {
        context.isDead = true;

        context.GetComponent<GameCombat.EnemyMotor>()?.Clear();
        if(context.animator) context.animator.SetTrigger(DieHash);
    }
}