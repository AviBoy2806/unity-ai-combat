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
    public string NavigationDiagnostic {get;private set;}

    public Transform target;
    public bool requireInjectedTarget;
    public void ResetEpisodeState(){LastSeenPosition=Vector3.zero;LastSeenTime=float.NegativeInfinity;StopMoving();}
    public Transform Target {get {var t=target ? target : !requireInjectedTarget && PlayerRefManager.Instance && PlayerRefManager.Instance.HasPlayer() ? PlayerRefManager.Instance.PlayerRef.transform : null;var own=GetComponent<GameCombat.Combatant>();var other=t?t.GetComponentInParent<GameCombat.Combatant>():null;return own && other && own.arenaId!=other.arenaId?null:t;}}
    public bool HasPlayer { get {var t=Target;if(!t)return false;var a=t.GetComponentInParent<GameCombat.Combatant>();return !a || !a.Dead;} }
    public float PlayerDistance => HasPlayer ? Vector3.Distance(transform.position,Target.position) : Mathf.Infinity;
    public float PlanarPlayerDistance => HasPlayer ? Vector3.ProjectOnPlane(Target.position-transform.position,Vector3.up).magnitude : Mathf.Infinity;

    // A jump changes the observed position, not the walkable destination below it.
    // Cast first so a generous NavMesh sample cannot pick a nearby ledge or wall top.
    public bool TryGetNavigationPosition(Vector3 observedPosition,out Vector3 position)
    {
        position=default;
        var body=GetComponent<enemy>();
        int ground=body ? body.GroundMask.value : 1<<7;
        if(!Physics.Raycast(observedPosition+Vector3.up*.25f,Vector3.down,out var groundHit,64,ground,QueryTriggerInteraction.Ignore))return false;
        if(!NavMesh.SamplePosition(groundHit.point,out var surface,.6f,agent.areaMask))return false;
        position=surface.position;return true;
    }

    // Both decision controllers use navigation until melee is possible on the same
    // walkable surface. Otherwise they would steer straight into a deck's side.
    public bool CanEngagePlayer()
    {
        if(!HasPlayer || !CanNavigate)return false;
        var own=GetComponent<Collider>();var other=Target.GetComponentInParent<Collider>();
        float ownFeet=own ? own.bounds.min.y : transform.position.y;
        float targetFeet=other ? other.bounds.min.y : Target.position.y;
        if(Mathf.Abs(targetFeet-ownFeet)>1.25f)return false;
        if(!TryGetNavigationPosition(Target.position,out var destination))return false;
        Vector3 feet=new Vector3(transform.position.x,ownFeet,transform.position.z);
        if(!NavMesh.SamplePosition(feet,out var start,.6f,agent.areaMask))return false;
        return Mathf.Abs(destination.y-start.position.y)<=.25f &&
            !agent.Raycast(destination,out _);
    }

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

        if (PlanarPlayerDistance > Mathf.Max(detectionRange, loseTargetRange))
            return false;

        Vector3 origin = eye != null ? eye.position : transform.position + Vector3.up * 0.5f;
        Vector3 targetCenter = Target.position + Vector3.up * 0.5f;

        return !Physics.Linecast(origin, targetCenter, obstacleLayers, QueryTriggerInteraction.Ignore);
    }

    public bool DetectPlayer()
    {
        return HasPlayer &&
               PlanarPlayerDistance <= detectionRange &&
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

        // Retain the last valid route if an observation has no supported ground.
        if (!TryGetNavigationPosition(position,out var destination)){NavigationDiagnostic="No supported navigation point below "+position;return;}

        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            agent.SetPath(path);
            NavigationDiagnostic="Following "+destination;
        }
        else NavigationDiagnostic=$"Unreachable {position} projected {destination}: {path.status}";
    }

    public void StopMoving()
    {
        if (!CanNavigate)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }
}
