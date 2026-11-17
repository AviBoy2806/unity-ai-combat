
using UnityEngine;
using Unity.Cinemachine;

public class CameraLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Player player;
    [SerializeField] private PlayerInputComponent inputComponent;
    [SerializeField] private LockOn lockOn;
    [SerializeField] private CinemachineThirdPersonFollow thirdPersonFollow;

    [Header("Camera Settings")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Lock On Rotation")]
    [SerializeField] private float lockOnPitchOffset = -10f;
    [SerializeField] private float lockOnPitchSmoothSpeed = 5f;
    
    private float currentPitchOffset;

    [SerializeField] private float lockOnRotationSpeed = 8f;
    [SerializeField] private float targetHeightOffset = 1.5f;

    [Header("Lock On Framing")]
    [Range(0f, 1f)]
    [SerializeField] private float framingBias = 0.6f;

    [SerializeField] private float closeDistance = 2f;
    [SerializeField] private float farDistance = 8f;

    [Header("Lock On Camera Position")]
    [SerializeField] private float lockedShoulderOffset = 1.5f;
    [SerializeField] private float closeShoulderOffset = 2.5f;
    [SerializeField] private float lockedCameraDistance = 5.5f;
    [SerializeField] private float closeCameraDistance = 6.5f;
    [SerializeField] private float cameraPositionSmoothSpeed = 5f;

    private float yaw;
    private float pitch;

    private Vector3 defaultShoulderOffset;
    private float defaultCameraDistance;

    private void Start()
    {
        if (cameraTarget == null)
            cameraTarget = transform;

        if (player == null)
            player = GetComponentInParent<Player>();

        if (player != null)
        {
            if (inputComponent == null)
                inputComponent =
                    player.GetComponent<PlayerInputComponent>();

            if (lockOn == null)
                lockOn = player.GetComponent<LockOn>();
        }

        if (thirdPersonFollow != null)
        {
            defaultShoulderOffset =
                thirdPersonFollow.ShoulderOffset;

            defaultCameraDistance =
                thirdPersonFollow.CameraDistance;
        }

        Vector3 angles = cameraTarget.eulerAngles;

        yaw = angles.y;
        pitch = Mathf.DeltaAngle(0f, angles.x);
    }

    private void Update()
    {
        if (cameraTarget == null || inputComponent == null)
            return;

        bool isLockedOn =
            lockOn != null &&
            lockOn.isActiveAndEnabled &&
            lockOn.target != null;

        float targetPitchOffset = isLockedOn
            ? lockOnPitchOffset
            : 0f;

        currentPitchOffset = Mathf.Lerp(
            currentPitchOffset,
            targetPitchOffset,
            GetSmoothFactor(lockOnPitchSmoothSpeed)
        );

        if (isLockedOn && player != null)
        {
            UpdateLockOnRotation();
            UpdateLockOnCameraPosition();
        }
        else
        {
            UpdateFreeRotation();
            RestoreCameraPosition();
        }

        cameraTarget.rotation = Quaternion.Euler(
            Mathf.Clamp(
                pitch + currentPitchOffset,
                minPitch,
                maxPitch
            ),
            yaw,
            0f
        );
    }

    private void UpdateFreeRotation()
    {
        Vector2 look = inputComponent.input.look;

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateLockOnRotation()
    {
        Vector3 playerPosition =
            player.transform.position +
            Vector3.up * targetHeightOffset;

        Vector3 enemyPosition =
            lockOn.target.transform.position +
            Vector3.up * targetHeightOffset;

        Vector3 framingPoint = Vector3.Lerp(
            playerPosition,
            enemyPosition,
            framingBias
        );

        Vector3 direction =
            framingPoint - cameraTarget.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        Vector3 angles = targetRotation.eulerAngles;

        float targetYaw = angles.y;

        float targetPitch = Mathf.Clamp(
            Mathf.DeltaAngle(0f, angles.x),
            minPitch,
            maxPitch
        );

        float t = GetSmoothFactor(lockOnRotationSpeed);

        yaw = Mathf.LerpAngle(yaw, targetYaw, t);
        pitch = Mathf.LerpAngle(pitch, targetPitch, t);
    }

    private void UpdateLockOnCameraPosition()
    {
        if (thirdPersonFollow == null)
            return;

        float enemyDistance = Vector3.Distance(
            player.transform.position,
            lockOn.target.transform.position
        );

        // 1 when enemy is close, 0 when far away.
        float closeness = 1f - Mathf.InverseLerp(
            closeDistance,
            farDistance,
            enemyDistance
        );

        float targetShoulderX = Mathf.Lerp(
            lockedShoulderOffset,
            closeShoulderOffset,
            closeness
        );

        float targetDistance = Mathf.Lerp(
            lockedCameraDistance,
            closeCameraDistance,
            closeness
        );

        Vector3 targetShoulderOffset =
            defaultShoulderOffset;

        targetShoulderOffset.x = targetShoulderX;

        float t = GetSmoothFactor(
            cameraPositionSmoothSpeed
        );

        thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
            thirdPersonFollow.ShoulderOffset,
            targetShoulderOffset,
            t
        );

        thirdPersonFollow.CameraDistance = Mathf.Lerp(
            thirdPersonFollow.CameraDistance,
            targetDistance,
            t
        );
    }

    private void RestoreCameraPosition()
    {
        if (thirdPersonFollow == null)
            return;

        float t = GetSmoothFactor(
            cameraPositionSmoothSpeed
        );

        thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
            thirdPersonFollow.ShoulderOffset,
            defaultShoulderOffset,
            t
        );

        thirdPersonFollow.CameraDistance = Mathf.Lerp(
            thirdPersonFollow.CameraDistance,
            defaultCameraDistance,
            t
        );
    }

    private float GetSmoothFactor(float speed)
    {
        return 1f - Mathf.Exp(
            -speed * Time.deltaTime
        );
    }
}
