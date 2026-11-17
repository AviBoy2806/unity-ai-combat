using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
[RequireComponent(typeof(GameCombat.Combatant),typeof(GameCombat.EnemyMotor),typeof(GameCombat.EnemyBrainHost))]
public class enemy : MonoBehaviour
{
    public bool standaloneControl;
    public void ResetEpisodeState(){stateMachine?.currentState?.Exit();isDead=false;GetComponent<GameCombat.EnemyMotor>().SetStunned(false);horizontalVelocity=Vector3.zero;verticalVelocity=0;grounded=false;player=null;playerRefManager=null;stateMachine=new EnemyStateMachine(this);}

    public float walkSpeed = 10f;
    public float runSpeed = 15f;

    public bool isDead = false;
    private readonly Collider[] visionResults = new Collider[16];

    [Header("Horizontal Movement")]
    public Vector3 horizontalVelocity;

    private Vector3 spawnPosition;

    [Header("Player Reference")]
    public PlayerRefManager playerRefManager;
    public Player player;

    [Header("State Machine")]
    public Animator animator;
    public EnemyStateMachine stateMachine { get; private set; }
    
    public Rigidbody rb;
    public CapsuleCollider enemyCollider;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.08f;
    public bool grounded;

    [Header("Air Gravity")]
    public float maxFallVelocity = 20f;
    public float ascendGravityMultiplier = 1f;
    public float fallGravityMultiplier = 1.5f;
    public float apexGravityMultiplier = 0.75f;
    public float apexThreshold = 2f;

    [Header("Movement")]
    public float verticalVelocity;

    public NavMeshAgent agent;
    private HealthComponent health;
    [SerializeField] private string currentStateName;
    public bool IsStunned => stateMachine?.currentState is EnemyStunnedState;
    public string CurrentStateName => currentStateName;
    internal void SetStateName(string name){currentStateName=name;}
    public void SyncCombatState(){stateMachine?.SyncCombatState();}
    void OnEnable(){GetComponent<GameCombat.CombatActionRunner>().Changed+=SyncCombatState;if(IsStunned)stateMachine.currentState.Enter();else SyncCombatState();}
    void OnDisable(){GetComponent<GameCombat.CombatActionRunner>().Changed-=SyncCombatState;}


    private void Awake()
    {
        spawnPosition = transform.position;

        health = GetComponentInChildren<HealthComponent>();
        if (health != null)
            health.healthChanged += OnHealthChanged;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if(animator) animator.applyRootMotion=false; // EnemyMotor is the only body movement writer.


        
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (enemyCollider == null)
            enemyCollider = GetComponent<CapsuleCollider>();

        // Configure for dynamic physics
        rb.isKinematic = false;
        rb.useGravity = false; // Disabled so custom apex/fall gravity multipliers still work
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    



    agent = GetComponent<NavMeshAgent>();
    if (agent != null)
    {
        agent.updatePosition = false;
        agent.updateRotation = false;
    }
        stateMachine=new EnemyStateMachine(this);
    }

    private void Start()
    {

        if(standaloneControl){SyncCombatState();return;}
        playerRefManager = PlayerRefManager.Instance;

        if (playerRefManager != null)
            player = playerRefManager.PlayerRef;
        else
            Debug.LogWarning("PlayerRefManager not found; assign perception Target for standalone actors.");
        if(stateMachine==null)stateMachine=new EnemyStateMachine(this);
        SyncCombatState();
        if (isDead) stateMachine.ForceChangeState(EnemyState.EnemyStateType.dead);
    }

    private void OnDestroy(){if(health)health.healthChanged-=OnHealthChanged;}

    private void Update()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // Keep the NavMeshAgent's internal position synced with the physical Rigidbody
            agent.nextPosition = rb.position;
        }

        stateMachine?.Update();
    }

    // Ground detection using the capsule's bottom hemisphere.
    public LayerMask GroundMask => groundMask;
    public bool CheckGround()
    {
        GetCapsule(out Vector3 p1, out Vector3 p2, out float radius);

        // Shrink radius slightly to avoid catching on vertical walls
        return Physics.CapsuleCast(
            p1,
            p2,
            radius * 0.95f,
            Vector3.down,
            out RaycastHit hit,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        ) && hit.normal.y > 0.5f;
    }

    // Get the capsule's world-space geometry for ground checking.
    private void GetCapsule(out Vector3 point1, out Vector3 point2, out float radius)
    {
        Transform t = enemyCollider.transform;
        Vector3 scale = t.lossyScale;
        int direction = enemyCollider.direction;

        Vector3 axis;
        float axisScale;
        float radiusScale;

        if (direction == 0)
        {
            axis = t.right;
            axisScale = Mathf.Abs(scale.x);
            radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
        else if (direction == 1)
        {
            axis = t.up;
            axisScale = Mathf.Abs(scale.y);
            radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }
        else
        {
            axis = t.forward;
            axisScale = Mathf.Abs(scale.z);
            radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        radius = enemyCollider.radius * radiusScale;
        float height = Mathf.Max(enemyCollider.height * axisScale, radius * 2f);
        float halfSegment = height * 0.5f - radius;
        Vector3 center = t.TransformPoint(enemyCollider.center);

        point1 = center + axis * halfSegment;
        point2 = center - axis * halfSegment;
    }

    public void OnHealthChanged(float currentHealth)
    {
        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            horizontalVelocity = Vector3.zero;
            stateMachine?.ForceChangeState(EnemyState.EnemyStateType.dead);
        }
    }

    public void ApplyAirGravity()
    {
        float gravityMultiplier;

        if (verticalVelocity > 0f)
        {
            gravityMultiplier = (verticalVelocity < apexThreshold) 
                ? apexGravityMultiplier 
                : ascendGravityMultiplier;
        }
        else
        {
            gravityMultiplier = fallGravityMultiplier;
        }

        verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.fixedDeltaTime;
    }

    public void ClampFallVelocity()
    {
        verticalVelocity = Mathf.Max(verticalVelocity, -maxFallVelocity);
    }

    public void Jump(float jumpForce)
    {
        if (!grounded)
            return;

        GetComponent<GameCombat.EnemyMotor>()?.QueueJump(jumpForce);

    }

    public bool HasPlayer()
    {
        return player != null;
    }
}
