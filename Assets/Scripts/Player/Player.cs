using UnityEngine;
using GameCombat;

[RequireComponent(typeof(PlayerInputComponent))]
public class Player : MonoBehaviour
{
    [Tooltip("Training only: no singleton, camera lookup or cursor ownership.")] public bool standaloneControl;
    public void ResetEpisodeState(){stateMachine?.currentState?.Exit();inputComponent.ResetEpisodeState();headingHeld=false;useRootMotion=default;rootMotionOwner=null;velocity=Vector3.zero;grounded=false;stateMachine=new PlayerStateMachine(this);combat.ResetEpisodeState();}
    public PlayerCombatAdapter combat { get; private set; }
    private HealthComponent health;
    public PlayerInputComponent inputComponent { get; private set; }
    public LockOn lockOn { get; private set; }

    [HideInInspector]
    public PlayerInputComponent.PlayerInput input => inputComponent.input;
    public Rigidbody rb;
    public Animator animator;
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float jumpForce = 10f;
    public float dodgeSpeed = 20f;
    public float heavyAttackForce = 15f;
    public Vector3 velocity = Vector3.zero;
    public PlayerStateMachine stateMachine;

    [System.Serializable]
    public struct UseRootMotion
    {
        public bool xzPlayer;
        public bool yPlayer;
    }
    
    [HideInInspector]
    public UseRootMotion useRootMotion;
    public StateRootMotion rootMotionOwner;

    
    [SerializeField] Transform camTransform;
    [field: SerializeField] public Transform model{get; private set;}
    [SerializeField] float rotationSpeed = 720f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private LayerMask groundMask = ~0;
    public Collider playerCollider;
    public bool grounded;

    [Header("Air Movement")]
    [SerializeField] public float airAcceleration = 12f;
    [SerializeField] public float airControl = 0.6f;
    [SerializeField] public float airMaxSpeed = 6f;

    [Header("Air Gravity")]
    [SerializeField] public float maxFallVelocity = 20f;
    [SerializeField] public float ascendGravityMultiplier = 1f;
    [SerializeField] public float fallGravityMultiplier = 1.5f;
    [SerializeField] public float apexGravityMultiplier = 0.75f;
    [SerializeField] public float apexThreshold = 2f;

    private int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private int VerticalDirHash = Animator.StringToHash("VerticalDir");


    void Awake()
    {
        lockOn = GetComponent<LockOn>();
        lockOn.enabled = false;
        // Automatically create/register PlayerRefManager
        if(!standaloneControl)PlayerRefManager.CreateManager(this);
        inputComponent = GetComponent<PlayerInputComponent>();
        health = GetComponentInChildren<HealthComponent>();
        health.healthChanged += OnHealthChanged;
        combat = GetComponent<PlayerCombatAdapter>();

        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if (playerCollider == null)
            playerCollider = GetComponent<Collider>();

        if (!standaloneControl && camTransform == null && Camera.main != null)
            camTransform = Camera.main.transform;

        stateMachine = new PlayerStateMachine(this);

        if(!standaloneControl){Cursor.lockState = CursorLockMode.Locked;Cursor.visible = false;}
        input.movement = Vector2.zero;
    }

    // Update is called once per frame
    void Update()
    {
        velocity = rb.linearVelocity;

        if(animator) animator.SetFloat(VerticalDirHash, rb.linearVelocity.y);

        if (input.lockOnJustPressed)
            lockOn.enabled = !lockOn.enabled;

        if(combat) combat.SyncState();
        if(AttackFacingLocked && combat && combat.Actor.Runner.Action==CombatAction.Free)input.jumpTriggered=false;
        else stateMachine?.Update();

        grounded = IsGrounded();
        if(animator) animator.SetBool(IsGroundedHash, grounded);
    }
    
    

    void FixedUpdate()
    {
        grounded=IsGrounded();
        if(!combat || !combat.Actor.Runner.OwnsMovement && !AttackFacingLocked) stateMachine?.FixedUpdate();
        if(combat && !combat.Actor.Runner.OwnsMovement && AttackFacingLocked)SetVelocity(x:0,z:0);

        if (!grounded)
        {
            ApplyAirGravity();
            ClampFallVelocity();
        }
    }

    public Vector3 GetCameraRelativeMove()
    {
        Vector3 forward = camTransform ? camTransform.forward : transform.forward;
        Vector3 right = camTransform ? camTransform.right : transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        return (forward * input.movement.y +
                right * input.movement.x).normalized;
    }

    public Vector3 GetCameraForward()
    {
        Vector3 forward = camTransform ? camTransform.forward : transform.forward;

        forward.y = 0f;
        forward.Normalize();

        return forward;
    }

    public void SetVelocity(
        Vector3? velocity = null,
        float? x = null,
        float? y = null,
        float? z = null)
    {
        Vector3 newVelocity = velocity ?? rb.linearVelocity;

        if (x.HasValue) newVelocity.x = x.Value;
        if (y.HasValue) newVelocity.y = y.Value;
        if (z.HasValue) newVelocity.z = z.Value;

        rb.linearVelocity = newVelocity;
    }

    bool headingHeld; Quaternion attackHeading;
    public bool AttackFacingLocked => GameCombat.AttackFacingPolicy.Locked(combat && combat.Actor.Runner.Action==CombatAction.Attack,animator);
    public void EnforceAttackFacing(){
        if(!model)return;
        if(AttackFacingLocked){if(!headingHeld){attackHeading=model.rotation;headingHeld=true;}model.rotation=attackHeading;}
        else headingHeld=false;
    }
    void LateUpdate(){EnforceAttackFacing();}

    public void RotateModel(Vector3 direction, float speed = -1f)
    {
        EnforceAttackFacing();
        if(AttackFacingLocked)return;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        float currentSpeed = speed < 0f ? rotationSpeed : speed;

        model.rotation = Quaternion.RotateTowards(
            model.rotation,
            targetRotation,
            currentSpeed * Time.deltaTime
        );
    }

    public bool IsGrounded()
    {
        Bounds bounds = playerCollider.bounds;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.05f,
            bounds.center.z
        );

        grounded = Physics.Raycast(
            origin,
            Vector3.down,
            groundCheckDistance + 0.05f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        return grounded;
    }

    public void ApplyAirMovement()
    {
        Vector3 direction = GetCameraRelativeMove();
        direction = Vector3.ClampMagnitude(direction, 1f);

        Vector3 currentVelocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            currentVelocity.x,
            0f,
            currentVelocity.z
        );

        Vector3 targetVelocity = direction * airMaxSpeed;

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            airAcceleration * airControl * Time.fixedDeltaTime
        );

        SetVelocity(
            x: horizontalVelocity.x,
            z: horizontalVelocity.z
        );
    }

    public void ApplyAirGravity()
    {
        float gravityMultiplier;

        if (rb.linearVelocity.y > 0f)
        {
            gravityMultiplier = ascendGravityMultiplier;
        }
        else
        {
            gravityMultiplier = fallGravityMultiplier;
        }

        rb.AddForce(
            Physics.gravity * (gravityMultiplier - 1f),
            ForceMode.Acceleration
        );
    }

    public void ClampFallVelocity()
    {
        Vector3 currentVelocity = rb.linearVelocity;

        if (currentVelocity.y < -maxFallVelocity)
        {
            currentVelocity.y = -maxFallVelocity;
            rb.linearVelocity = currentVelocity;
        }
    }

    public Vector3 GetModelDirection()
    {
        Vector3 direction = model.forward;
        direction.y = 0f;
        return direction.normalized;
    }

    void OnDestroy(){if(health)health.healthChanged-=OnHealthChanged;}

    public void OnHealthChanged(float health)
    {
        if(health <= 0)
        {
            stateMachine.ForceChangeState(PlayerState.PlayerStateType.dead);
        }
    }

    public void SetDampedFloat(int hash, float target, float dampTime)
    {
        if(!animator)return;
        float current = animator.GetFloat(hash);

        if (Mathf.Abs(current - target) < 0.001f)
        {
            animator.SetFloat(hash, target);
            return;
        }

        animator.SetFloat(
            hash,
            target,
            dampTime,
            Time.deltaTime
        );
    }

}

