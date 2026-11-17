using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPerceptionNav : MonoBehaviour
{
    [Header("Vision & Detection")]
    public Transform eye;
    public LayerMask obstacleLayers;
    public bool detectPlayer = true;
    public float detectionRange = 15f;
    public float loseTargetRange = 22f;

    [Header("Chase & Investigation")]
    public float chaseSpeed = 5f;
    public float memoryDuration = 3f;
    public float investigationTimeout = 15f;

    public Vector3 LastSeenPosition { get; private set; }
    public float LastSeenTime { get; private set; } = float.NegativeInfinity;

    private NavMeshAgent agent;

    public bool CanNavigate => agent != null && agent.enabled && agent.isOnNavMesh;
    public NavMeshAgent Agent => agent;

    public Transform target;
    public bool requireInjectedTarget;
    public void ResetEpisodeState(){LastSeenPosition=Vector3.zero;LastSeenTime=float.NegativeInfinity;StopMoving();}
    public Transform Target {get {var t=target ? target : !requireInjectedTarget && PlayerRefManager.Instance && PlayerRefManager.Instance.HasPlayer() ? PlayerRefManager.Instance.PlayerRef.transform : null;var own=GetComponent<GameCombat.Combatant>();var other=t?t.GetComponentInParent<GameCombat.Combatant>():null;return own && other && own.arenaId!=other.arenaId?null:t;}}
    public bool HasPlayer { get {var t=Target;if(!t)return false;var a=t.GetComponentInParent<GameCombat.Combatant>();return !a || !a.Dead;} }
    public float PlayerDistance => HasPlayer ? Vector3.Distance(transform.position,Target.position) : Mathf.Infinity;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Keep NavMeshAgent decoupled so it doesn't fight the dynamic Rigidbody
        if (agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
        }
    }

    private void Update()
    {
        if (CanSeePlayer())
        {
            LastSeenPosition = Target.position;
            LastSeenTime = Time.time;
        }
    }

    public bool CanSeePlayer()
    {
        if (!detectPlayer || !HasPlayer)
            return false;

        if (PlayerDistance > Mathf.Max(detectionRange, loseTargetRange))
            return false;

        Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 0.5f;
        Vector3 targetCenter = Target.position + Vector3.up * 0.5f;

        return !Physics.Linecast(origin, targetCenter, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    public bool DetectPlayer()
    {
        return HasPlayer &&
               PlayerDistance <= detectionRange &&
               CanSeePlayer();
    }

    public void MoveTo(Vector3 position, float speed, float stoppingDistance)
    {
        var actor=GetComponent<enemy>();if(actor && actor.IsStunned)return;
        if (!CanNavigate)
            return;

        agent.speed = Mathf.Max(0f, speed);
        agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        agent.autoBraking = stoppingDistance > 0f;
        agent.isStopped = false;

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, agent.areaMask))
        {
            agent.ResetPath();
            return;
        }

        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            agent.SetPath(path);
        }
        else
        {
            agent.ResetPath();
        }
    }

    public void StopMoving()
    {
        if (!CanNavigate)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }
}
