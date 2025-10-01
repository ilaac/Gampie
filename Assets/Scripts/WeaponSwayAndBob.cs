using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class WeaponSwayAndBobAdvanced : MonoBehaviour
{
    [Header("References")]
    public CharacterController characterController;
    public Camera playerCamera;
    public PlayerController playerController;

    [Header("Position Sway")]
    [Tooltip("How far the weapon moves with camera rotation (position)")]
    public float posSwayAmount = 0.02f;
    public float posSwayZAmount = 0.05f;
    public float maxPosSwayX = 0.05f;
    public float maxPosSwayY = 0.05f;
    public float maxPosSwayZ = 0.12f;
    public float posSwayLerpSpeed = 12f;       // how quickly to lerp to the sway offset

    [Header("Rotation Sway")]
    [Tooltip("How much the weapon rotates with camera rotation (degrees)")]
    public float rotSwayAmount = 1.5f;
    public float rotRollAmount = 0.75f;
    public float rotSwayLerpSpeed = 12f;       // how quickly to lerp to the rotation sway

    [Header("Dash Pullback")]
    public float dashPullbackZ = 0.12f;
    public float dashPullbackInSpeed = 20f;
    public float dashPullbackOutSpeed = 8f;

    [Header("Dash FOV")]
    public float dashFOVIncrease = 15f;
    public float dashFOVSmooth = 8f;

    [Header("General")]
    public bool useCameraDeltaForSway = true;

    // internals
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private Vector3 lastCameraEuler;
    private Vector2 lookDeltaFallback;

    // smoothed states
    private Vector3 currentPosSway;
    private Vector3 targetPosSway;
    private Quaternion currentRotSway;
    private Quaternion targetRotSway;
    private Vector3 currentDashOffset;
    private Vector3 dashOffsetVelocity;

    private float baseFOV;

    void Start()
    {
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();
        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        if (playerCamera != null)
        {
            lastCameraEuler = playerCamera.transform.eulerAngles;
            baseFOV = playerCamera.fieldOfView;
        }

        currentRotSway = initialLocalRot;
    }

    void LateUpdate()
    {
        Vector2 camDelta = GetLookDelta();
        UpdateSway(camDelta);
        UpdateDashPullback();
        UpdateDashFOV();
        ApplyOffsets();
    }

    Vector2 GetLookDelta()
    {
        if (useCameraDeltaForSway && playerCamera != null)
        {
            Vector3 cur = playerCamera.transform.eulerAngles;
            float dYaw = Mathf.DeltaAngle(lastCameraEuler.y, cur.y);
            float dPitch = Mathf.DeltaAngle(lastCameraEuler.x, cur.x);
            lastCameraEuler = cur;
            return new Vector2(dYaw, dPitch);
        }
        else
        {
            return lookDeltaFallback;
        }
    }

    void UpdateSway(Vector2 camDelta)
    {
        // --- Position Sway Target ---
        float x = Mathf.Clamp(camDelta.x * posSwayAmount, -maxPosSwayX, maxPosSwayX);
        float y = Mathf.Clamp(camDelta.y * posSwayAmount, -maxPosSwayY, maxPosSwayY);
        float lookMag = Mathf.Abs(camDelta.x) + Mathf.Abs(camDelta.y);
        float z = -Mathf.Clamp(lookMag * posSwayZAmount * 0.01f, 0f, maxPosSwayZ);

        targetPosSway = new Vector3(x, y, z);

        // --- Rotation Sway Target ---
        float rotX = -camDelta.y * rotSwayAmount;
        float rotY = camDelta.x * rotSwayAmount;
        float rotZ = camDelta.x * -rotRollAmount;
        targetRotSway = Quaternion.Euler(rotX, rotY, rotZ);

        // Smooth/Lerp towards target
        currentPosSway = Vector3.Lerp(currentPosSway, targetPosSway, Time.deltaTime * posSwayLerpSpeed);
        currentRotSway = Quaternion.Slerp(currentRotSway, targetRotSway, Time.deltaTime * rotSwayLerpSpeed);
    }

    void UpdateDashPullback()
    {
        bool dashing = playerController != null && playerController.isDashing;
        Vector3 dashTarget = dashing ? new Vector3(0f, 0f, -Mathf.Abs(dashPullbackZ)) : Vector3.zero;
        float speed = dashing ? dashPullbackInSpeed : dashPullbackOutSpeed;
        currentDashOffset = Vector3.SmoothDamp(currentDashOffset, dashTarget,
            ref dashOffsetVelocity, 1f / Mathf.Max(0.0001f, speed));
    }

    void UpdateDashFOV()
    {
        if (playerCamera == null) return;
        bool dashing = playerController != null && playerController.isDashing;
        float targetFOV = dashing ? baseFOV + dashFOVIncrease : baseFOV;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * dashFOVSmooth);
    }

    void ApplyOffsets()
    {
        Vector3 finalPos = initialLocalPos + currentPosSway + currentDashOffset;
        transform.localPosition = Vector3.Lerp(transform.localPosition, finalPos, Time.deltaTime * posSwayLerpSpeed);

        Quaternion finalRot = initialLocalRot * currentRotSway;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, finalRot, Time.deltaTime * rotSwayLerpSpeed);
    }

    public void OnLook(InputAction.CallbackContext ctx)
    {
        if (!useCameraDeltaForSway)
            lookDeltaFallback = ctx.ReadValue<Vector2>();
    }

    public void ResetOffsetsImmediate()
    {
        currentPosSway = Vector3.zero;
        targetPosSway = Vector3.zero;
        currentDashOffset = Vector3.zero;
        dashOffsetVelocity = Vector3.zero;
        currentRotSway = initialLocalRot;
        transform.localPosition = initialLocalPos;
        transform.localRotation = initialLocalRot;
    }
}
