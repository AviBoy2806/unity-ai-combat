using UnityEngine;
using UnityEngine.AI;

public class EnemyChaseState : EnemyState
{
    private EnemyPerceptionNav nav;

    private float dampTime = 0.1f;
    private float rotationSpeed = 10f;
    private float arrivalThreshold = 0.55f;

    private float nextPathTime;
    private float searchStarted;
    private float arrivedTime;
    private float trackedSeenTime;

    public override void Init()
    {
        nav = context.GetComponent<EnemyPerceptionNav>();
    }

    public override void Enter()
    {
        if (nav == null)
            nav = context.GetComponent<EnemyPerceptionNav>();

        nextPathTime = 0f;
        searchStarted = Time.time;
        arrivedTime = -1f;
        trackedSeenTime = nav != null ? nav.LastSeenTime : float.NegativeInfinity;
    }

    public override void Exit()
    {
        context.horizontalVelocity = Vector3.zero;
        nav?.StopMoving();
    }

    public override void Update()
    {
        float targetAnimSpeed = context.horizontalVelocity.sqrMagnitude > 0.05f ? 1f : 0f;
        SetDampedFloat(SpeedHash, targetAnimSpeed, dampTime);

        if (nav == null || !nav.HasPlayer || float.IsNegativeInfinity(nav.LastSeenTime))
        {
            context.stateMachine.TryChangeState(this, EnemyStateType.idle);
            return;
        }

        bool visible = nav.CanSeePlayer();

        if (visible)
        {
            searchStarted = Time.time;
            arrivedTime = -1f;

            // Uncomment once EnemyStateType.attack is added:
            // if (nav.PlayerDistance <= attackRange)
            // {
            //     context.stateMachine.TryChangeState(this, EnemyStateType.attack);
            //     return;
            // }
        }

        if (trackedSeenTime != nav.LastSeenTime)
        {
            trackedSeenTime = nav.LastSeenTime;
            arrivedTime = -1f;
        }

        if (!visible)
        {
            bool arrived = false;

            if (nav.CanNavigate)
            {
                arrived = !nav.Agent.pathPending &&
                          nav.Agent.hasPath &&
                          nav.Agent.remainingDistance <= arrivalThreshold;

                if (!arrived && NavMesh.SamplePosition(
                    nav.LastSeenPosition, out NavMeshHit hit, 2f, nav.Agent.areaMask))
                {
                    Vector3 delta = nav.Agent.nextPosition - hit.position;
                    arrived = new Vector2(delta.x, delta.z).sqrMagnitude <= arrivalThreshold * arrivalThreshold &&
                              Mathf.Abs(delta.y) < 2f;
                }
            }

            if (arrived)
            {
                nav.StopMoving();
                context.horizontalVelocity = Vector3.zero;

                if (arrivedTime < 0f)
                    arrivedTime = Time.time;

                if (Time.time - arrivedTime >= Mathf.Max(0f, nav.memoryDuration))
                {
                    context.stateMachine.TryChangeState(this, EnemyStateType.idle);
                }
                return;
            }

            if (Time.time - searchStarted >= Mathf.Max(1f, nav.investigationTimeout))
            {
                context.stateMachine.TryChangeState(this, EnemyStateType.idle);
                return;
            }
        }

        if (Time.time >= nextPathTime)
        {
            float stopDist = visible ? 0.8f : 0f;
            nav.MoveTo(nav.LastSeenPosition, nav.chaseSpeed, stopDist);
            nextPathTime = Time.time + 0.2f;
        }
    }

    public override void FixedUpdate()
    {
        if (nav == null || !nav.CanNavigate || nav.Agent.isStopped)
        {
            context.horizontalVelocity = Vector3.zero;
            return;
        }

        Vector3 desiredVel = nav.Agent.desiredVelocity;
        desiredVel.y = 0f;

        context.horizontalVelocity = desiredVel;

        if (desiredVel.sqrMagnitude > 0.01f)
        {
            context.GetComponent<GameCombat.EnemyMotor>()?.Face(desiredVel);
        }
    }
}