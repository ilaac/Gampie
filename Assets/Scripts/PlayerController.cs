using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 6f;
    public float crouchSpeed = 3f;
    public float acceleration = 10f;
    public float deceleration = 15f;

    [Header("Jump Settings")]
    public float jumpHeight = 1.5f;
    public float gravity = -9.81f;
    public float coyoteTime = 0.15f; // seconds of grace after leaving ground

    [Header("Crouch Settings")]
    public float crouchHeight = 1f;
    public float standingHeight = 2f;
    public float crouchTransitionSpeed = 8f;
    public bool isCrouching;

    [Header("Dash Settings")]
    public float dashDistance = 8f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    public bool isDashing;

    [Header("Look Settings")]
    public Transform cameraTransform;
    public float sensitivityX = 200f;
    public float sensitivityY = 200f;
    public float minY = -80f;
    public float maxY = 80f;

    [Header("Headbob Settings - Standing")]
    public bool enableHeadbob = true;
    public float bobFrequency = 1.5f;
    public float bobAmplitude = 0.05f;
    public float bobSwayAmplitude = 0.05f;
    public float bobSmooth = 8f;

    [Header("Headbob Settings - Crouching")]
    public float crouchBobFrequency = 1.0f;
    public float crouchBobAmplitude = 0.03f;
    public float crouchBobSwayAmplitude = 0.03f;

    public Animator anim;

    private CharacterController controller;

    private Vector3 moveVelocity;
    private Vector3 velocity;
    private Vector3 dashDirection;
    private float dashTimer;
    private float dashCooldownTimer;

    private float xRotation;
    private Vector3 startCamPos;
    private float bobTimer;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;
    private bool crouchPressed;
    private bool dashPressed;

    private float coyoteTimer; // grace jump timer

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        startCamPos = cameraTransform.localPosition;

        controller.height = standingHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovementAndJump();
        HandleCrouch();
        HandleDash();
        HandleLook();
        if (enableHeadbob) HandleHeadbob();
    }

    // --- Unity Events called by PlayerInput ---
    public void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
    public void OnLook(InputAction.CallbackContext context) => lookInput = context.ReadValue<Vector2>();
    public void OnJump(InputAction.CallbackContext context) { if (context.started) jumpPressed = true; }
    public void OnCrouch(InputAction.CallbackContext context) { if (context.started) crouchPressed = true; }
    public void OnDash(InputAction.CallbackContext context) { if (context.started) dashPressed = true; }

    // --- Movement + Jump combined ---
    void HandleMovementAndJump()
    {
        if (isDashing)
        {
            controller.Move(dashDirection * (dashDistance / dashDuration) * Time.deltaTime);
            return;
        }

        // Horizontal movement
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        Vector3 moveDir = transform.TransformDirection(inputDir);

        float speed = isCrouching ? crouchSpeed : walkSpeed;
        float targetSpeed = speed * inputDir.magnitude;

        float currentSpeed = new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
        if (targetSpeed > currentSpeed)
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, deceleration * Time.deltaTime);

        moveVelocity.x = moveDir.x * currentSpeed;
        moveVelocity.z = moveDir.z * currentSpeed;

        // --- Ground check & coyote timer ---
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
            coyoteTimer = coyoteTime; // reset grace time when grounded
        }
        else
        {
            coyoteTimer -= Time.deltaTime; // count down when not grounded
        }

        // --- Jump ---
        if (jumpPressed && coyoteTimer > 0f && !isDashing)
        {
            if (isCrouching) // Stand up before jumping
                isCrouching = false;

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            coyoteTimer = 0f; // consume grace time
        }
        jumpPressed = false;

        // --- Gravity ---
        velocity.y += gravity * Time.deltaTime;

        // Move character
        Vector3 finalVelocity = new Vector3(moveVelocity.x, velocity.y, moveVelocity.z);
        controller.Move(finalVelocity * Time.deltaTime);
    }

    void HandleCrouch()
    {
        if (crouchPressed)
        {
            if (controller.isGrounded && !isDashing)
                isCrouching = !isCrouching;
        }
        crouchPressed = false;

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        float cameraTargetY = isCrouching ? crouchHeight / 2f : standingHeight / 2f;
        Vector3 camPos = cameraTransform.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, cameraTargetY, Time.deltaTime * crouchTransitionSpeed);
        cameraTransform.localPosition = camPos;
    }

    void HandleDash()
    {
        dashCooldownTimer -= Time.deltaTime;

        if (dashPressed && !isDashing && dashCooldownTimer <= 0f)
        {
            Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
            if (inputDir.sqrMagnitude < 0.1f)
                inputDir = Vector3.forward;

            dashDirection = transform.TransformDirection(inputDir.normalized);
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
        }
        dashPressed = false;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) isDashing = false;
        }
    }

    void HandleLook()
    {
        float mouseX = lookInput.x * sensitivityX * Time.deltaTime;
        float mouseY = lookInput.y * sensitivityY * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minY, maxY);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleHeadbob()
    {
        Vector3 horizontalVel = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
        float speed = horizontalVel.magnitude;

        // Use crouch-specific settings if crouching
        float freq = isCrouching ? crouchBobFrequency : bobFrequency;
        float amp = isCrouching ? crouchBobAmplitude : bobAmplitude;
        float sway = isCrouching ? crouchBobSwayAmplitude : bobSwayAmplitude;

        if (speed > 0.1f && controller.isGrounded)
        {
            bobTimer += Time.deltaTime * freq * (speed / 2f);
            float bobY = Mathf.Sin(bobTimer) * amp;
            float bobX = Mathf.Cos(bobTimer / 2f) * sway;
            Vector3 targetPos = startCamPos + new Vector3(bobX, bobY, 0f);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, Time.deltaTime * bobSmooth);
        }
        else
        {
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, startCamPos, Time.deltaTime * bobSmooth);
            bobTimer = 0f;
        }
    }

    void StopShooting()
    {
        anim.SetBool("IsShooting", false);
    }
}
