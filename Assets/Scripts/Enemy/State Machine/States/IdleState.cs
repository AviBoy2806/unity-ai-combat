
using UnityEngine;

public class EnemyIdleState : EnemyState
{
    float dampTime = 0.1f;
    float idleTimeUp = 0f;
    float idleTimer = 0f;    

    EnemyPerceptionNav nav;

    public override void Init()
    {
        nav = context.GetComponent<EnemyPerceptionNav>();
    }

    public override void Enter()
    {
        idleTimer = 0f;
        idleTimeUp = Random.Range(1f, 5f);
    }

    public override void Update()
    {
        SetDampedFloat(SpeedHash, 0f, dampTime);

        idleTimer += Time.deltaTime;
        if(idleTimer >= idleTimeUp)
        {
            context.stateMachine.TryChangeState(this, EnemyStateType.wander);
        }
        if (nav != null && nav.DetectPlayer())
        {
            context.stateMachine.TryChangeState(this, EnemyStateType.chase);
            return;
        }
    }
}
