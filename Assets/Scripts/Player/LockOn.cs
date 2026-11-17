
using UnityEngine;

public class LockOn : MonoBehaviour
{
    public float movementAnimationMultiplier = 1f;

    [Header("Visibility Settings")]
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private float visibilityGracePeriod = 0.3f;
    [SerializeField] private float targetHeightOffset = 1.5f;

private float invisibleTimer;
    
    [Header("Lock On Settings")]
    public float LockOnRadius = 10f;
    

    [Header("Current Target")]
    public enemy target { get; private set; }

    private Animator animator;

    private readonly int LockedOnHash =
        Animator.StringToHash("Is Locked On");

    private readonly int ForwardDirHash =
        Animator.StringToHash("ForwardDir");

    private readonly int SideDirHash =
        Animator.StringToHash("SideDir");

    private readonly int MovingForwardHash =
        Animator.StringToHash("MovingForward");

    private PlayerInputComponent inputComponent;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        inputComponent = GetComponent<PlayerInputComponent>();
    }

    private void OnEnable()
    {
        invisibleTimer = 0f;

        FindTarget();

        if (target == null)
        {
            enabled = false;
            return;
        }

        if(animator) animator.SetBool(LockedOnHash, true);
    }

    private void OnDisable()
    {
        invisibleTimer = 0f;
        target = null;

        if (animator != null)
            animator.SetBool(LockedOnHash, false);
    }

    private void Update()
    {
        if (target == null || target.isDead || !target.isActiveAndEnabled)
        {
            enabled = false;
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            target.transform.position
        );

        if (distance > LockOnRadius)
        {
            enabled = false;
            return;
        }

        if (!IsTargetVisible(target))
        {
            invisibleTimer += Time.deltaTime;

            if (invisibleTimer >= visibilityGracePeriod)
            {
                enabled = false;
                return;
            }
        }
        else
        {
            invisibleTimer = 0f;
        }

        UpdateLockedOnParams();
    }

    private void FindTarget()
    {
        target = null;

        enemy[] enemies = FindObjectsByType<enemy>();

        float closestDistance = LockOnRadius;

        foreach (enemy candidate in enemies)
        {
            if (candidate == null || candidate.isDead || !candidate.isActiveAndEnabled)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                candidate.transform.position
            );

            if (distance >= closestDistance)
                continue;

            if (!IsTargetVisible(candidate))
                continue;

            closestDistance = distance;
            target = candidate;
        }
    }

    private void UpdateLockedOnParams()
    {
        if (inputComponent == null)
            return;

        
        Vector2 movement = inputComponent.input.movement;


        float forwardDir =
            movement.x == 0f && movement.y == 0f
                ? 0f
                : movement.y * movementAnimationMultiplier;

        float sideDir = movement.x * movementAnimationMultiplier;

        SetDampedFloat(ForwardDirHash, forwardDir, 0.1f);
        SetDampedFloat(SideDirHash, sideDir, 0.1f);

        if (movement.y > 0)
            SetDampedFloat(MovingForwardHash, 0, 0.1f);
        else if (movement.y < 0)
            SetDampedFloat(MovingForwardHash, 1, 0.1f);
    }

    private void SetDampedFloat(
        int hash,
        float targetValue,
        float dampTime)
    {
        if (animator == null)
            return;

        animator.SetFloat(
            hash,
            targetValue,
            dampTime,
            Time.deltaTime);
    }

    private bool IsTargetVisible(enemy candidate)
    {
        if (candidate == null)
            return false;

        Vector3 origin =
            transform.position + Vector3.up * targetHeightOffset;

        Vector3 targetPosition =
            candidate.transform.position + Vector3.up * targetHeightOffset;

        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;

        if (distance < 0.001f)
            return true;

        return !Physics.Raycast(
            origin,
            direction.normalized,
            distance,
            obstructionMask,
            QueryTriggerInteraction.Ignore
        );
    }
}
