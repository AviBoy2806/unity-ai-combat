using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class EnemyWanderState : EnemyState
{
    private float dampTime = 0.1f;

    [Header("Wander Settings")]
    private int maxPatrolPoints = 4;
    private float maxWanderDistance = 8f;
    private float rotationSpeed = 8f;
    private float arrivalThreshold = 0.4f;

    private List<Vector3> patrolPoints = new List<Vector3>();
    private int currentPatrolIndex = 0;
    EnemyPerceptionNav nav;

    public override void Init()
    {
        nav = context.GetComponent<EnemyPerceptionNav>();
    }

    public override void Enter()
    {
        patrolPoints.Clear();
        currentPatrolIndex = 0;

        if (context.agent == null || !context.agent.isOnNavMesh)
        {
            context.stateMachine.TryChangeState(this, EnemyStateType.idle);
            return;
        }

        // Generate between 1 and maxPatrolPoints valid NavMesh waypoints
        int pointCount = Random.Range(1, maxPatrolPoints + 1);
        Vector3 origin = context.transform.position;

        for (int i = 0; i < pointCount; i++)
        {
            if (TryGetRandomNavMeshPoint(origin, maxWanderDistance, out Vector3 validPoint))
            {
                patrolPoints.Add(validPoint);
            }
        }

        // Fallback to idle if no valid points could be sampled on the NavMesh
        if (patrolPoints.Count == 0)
        {
            context.stateMachine.TryChangeState(this, EnemyStateType.idle);
            return;
        }

        context.agent.speed = context.walkSpeed;
        context.agent.stoppingDistance = 0f;
        context.agent.autoBraking = false;
        context.agent.isStopped = false;
        context.agent.SetDestination(patrolPoints[0]);
    }

    public override void Update()
    {
        SetDampedFloat(SpeedHash, 0.5f, dampTime);

        if (context.agent == null || !context.agent.isOnNavMesh || patrolPoints.Count == 0)
            return;

        Vector3 currentTarget = patrolPoints[currentPatrolIndex];
        Vector3 toTarget = currentTarget - context.transform.position;
        toTarget.y = 0f;

        // Wait until the NavMeshAgent finishes computing the path before checking arrival
        if (!context.agent.pathPending && 
            (context.agent.remainingDistance <= arrivalThreshold || toTarget.magnitude <= arrivalThreshold))
        {
            currentPatrolIndex++;

            if (currentPatrolIndex >= patrolPoints.Count)
            {
                // Reached final patrol point -> transition to Idle
                context.stateMachine.TryChangeState(this, EnemyStateType.idle);
                return;
            }

            context.agent.SetDestination(patrolPoints[currentPatrolIndex]);
        }

        if (nav != null && nav.DetectPlayer())
        {
            context.stateMachine.TryChangeState(this, EnemyStateType.chase);
            return;
        }
    }

    public override void FixedUpdate()
    {
        if (context.agent == null || !context.agent.isOnNavMesh)
        {
            context.horizontalVelocity = Vector3.zero;
            return;
        }

        // Keep agent speed synced if context.walkSpeed changes at runtime
        context.agent.speed = context.walkSpeed;

        // Pull velocity from the decoupled NavMeshAgent
        Vector3 desiredVel = context.agent.desiredVelocity;
        desiredVel.y = 0f;

        context.horizontalVelocity = desiredVel;

        // Smoothly rotate the Rigidbody to face the movement direction
        if (desiredVel.sqrMagnitude > 0.01f)
        {
            context.GetComponent<GameCombat.EnemyMotor>()?.Face(desiredVel);
        }
    }

    public override void Exit()
    {
        context.horizontalVelocity = Vector3.zero;

        if (context.agent != null && context.agent.isOnNavMesh)
        {
            context.agent.isStopped = true;
            context.agent.ResetPath();
        }
    }

    private bool TryGetRandomNavMeshPoint(Vector3 center, float radius, out Vector3 result)
    {
        // Try up to 10 times per point in case random offsets land off-mesh (walls/pits)
        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            Vector3 randomCandidate = center + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(randomCandidate, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = Vector3.zero;
        return false;
    }
}