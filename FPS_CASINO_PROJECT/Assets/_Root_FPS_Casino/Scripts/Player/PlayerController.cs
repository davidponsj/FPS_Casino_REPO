using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal del jugador FPS, pensado para usarse junto a un
/// componente Player Input con Behavior = "Invoke Unity Events".
/// En el Inspector del Player Input, dentro de "Events > Player", conecta
/// cada acción (Move, Look, Sprint, Jump, Slide) al método correspondiente
/// de este script (OnMove, OnLook, OnSprint, OnJump, OnSlide).
/// Requiere un CharacterController en el mismo GameObject y una cámara hija asignada.
/// No depende de ningún manager externo: todo el estado vive aquí.
///
/// Incluye:
/// - Slide buffering + boost: pulsar Slide en el aire lo ejecuta al aterrizar, más rápido (jump slide).
/// - El slide solo puede iniciarse mientras se mantiene pulsado Sprint.
/// - Coyote time + jump buffer: salto más permisivo, como en cualquier shooter pulido.
/// - FOV kick al esprintar / deslizar.
/// - Máquina de estados simple (PlayerMovementState) con evento OnStateChanged,
///   pensada para engancharse después a un Animator sin tocar el resto del código.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerMovementState
    {
        Idle,
        Walking,
        Sprinting,
        Sliding,
        Airborne
    }

    [Header("Referencias")]
    [SerializeField] private Transform cameraHolder; // Empty vacío hijo del player, a la altura de los ojos
    [SerializeField] private Camera playerCamera;     // Camera hija del cameraHolder (para el FOV kick)
    [SerializeField] private CharacterController controller;

    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8.5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float airControlMultiplier = 0.5f;

    [Header("Salto y gravedad")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f; // pequeña fuerza hacia abajo para mantener pegado al suelo
    [Tooltip("Margen tras salir de una plataforma en el que todavía puedes saltar.")]
    [SerializeField] private float coyoteTime = 0.15f;
    [Tooltip("Margen para bufferizar el salto si lo pulsas justo antes de tocar el suelo.")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Cámara / Mirada")]
    [SerializeField] private float mouseSensitivity = 0.12f; // el delta del ratón viene en píxeles por frame, sensibilidad mucho menor que con Input Manager viejo
    [SerializeField] private float gamepadSensitivity = 120f; // stick derecho: valor -1..1, se multiplica por deltaTime
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("FOV Kick")]
    [SerializeField] private float sprintFovBoost = 8f;
    [SerializeField] private float slideFovBoost = 12f;
    [SerializeField] private float fovLerpSpeed = 8f;

    [Header("Deslizamiento (Slide)")]
    [Tooltip("El slide solo se puede iniciar mientras se mantiene pulsado Sprint.")]
    [SerializeField] private float slideSpeed = 11f;
    [SerializeField] private float slideDuration = 0.75f;
    [SerializeField] private float slideCooldown = 0.5f;
    [SerializeField] private float slideCameraHeightOffset = -0.6f; // baja la cámara al deslizar
    [SerializeField] private float slideControllerHeight = 1f;      // altura del CharacterController al deslizar
    [SerializeField] private float standingControllerHeight = 2f;
    [Tooltip("Margen en segundos durante el cual pulsar Slide en el aire queda 'guardado' para ejecutarse al aterrizar.")]
    [SerializeField] private float slideBufferWindow = 0.15f;
    [Tooltip("Multiplicador de velocidad del slide cuando viene de un salto (jump slide). 1 = sin boost.")]
    [SerializeField] private float airSlideBoostMultiplier = 1.35f;

    /// <summary>Se dispara cada vez que cambia el estado de movimiento. Útil para animaciones/sonido.</summary>
    public event Action<PlayerMovementState> OnStateChanged;
    public PlayerMovementState CurrentState { get; private set; } = PlayerMovementState.Idle;

    // Input recibido desde Player Input (Invoke Unity Events)
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool lookIsMouse; // para decidir si aplicar deltaTime o no al mirar
    private bool sprintHeld;

    // Estado interno
    private float pitch;
    private Vector3 velocity;              // velocidad vertical (gravedad/salto)
    private Vector3 currentMoveVelocity;   // velocidad horizontal suavizada
    private bool isGrounded;
    private bool isSprinting;
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private float currentSlideMaxSpeed;    // velocidad tope del slide actual (con o sin boost)
    private float defaultCameraLocalY;
    private float baseFov;

    // Coyote time / jump buffer
    private float coyoteTimer;
    private float jumpBufferTimer;

    // Buffer de slide
    private float slideBufferTimer;
    private bool slideBufferedInAir;

    public bool IsSliding => isSliding;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    /// <summary>Magnitud de la velocidad horizontal actual, usada por las armas para calcular la dispersión de disparo.</summary>
    public float CurrentHorizontalSpeed => currentMoveVelocity.magnitude;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (cameraHolder != null)
            defaultCameraLocalY = cameraHolder.localPosition.y;

        if (playerCamera != null)
            baseFov = playerCamera.fieldOfView;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        isGrounded = controller.isGrounded;

        ApplyLook();
        HandleTimers();

        if (isSliding)
        {
            ApplySlideMovement();
        }
        else
        {
            HandleMovement();
        }

        ConsumeJump();
        ApplyGravity(); // la gravedad se aplica siempre, tanto de pie como deslizándote

        Vector3 finalMove = currentMoveVelocity + velocity;
        controller.Move(finalMove * Time.deltaTime);

        UpdateMovementState();
        UpdateFov();
    }

    // ---------------- EVENTOS DEL PLAYER INPUT (Invoke Unity Events) ----------------
    // Conecta estos métodos desde el Inspector del componente Player Input,
    // en Events > Player > <Accion> > + , arrastrando este GameObject y
    // seleccionando PlayerController > NombreDelMetodo (Dynamic InputAction.CallbackContext).

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
        lookIsMouse = context.control.device is Mouse;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed) sprintHeld = true;
        else if (context.canceled) sprintHeld = false;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) jumpBufferTimer = jumpBufferTime;
    }

    public void OnSlide(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!sprintHeld) return; // solo se puede deslizar mientras se esprinta, nunca andando

        // Guardamos el input un pequeño margen de tiempo. Si en ese margen
        // aterrizamos, el slide se dispara solo al tocar el suelo.
        slideBufferTimer = slideBufferWindow;
        slideBufferedInAir = !isGrounded;
    }

    // ---------------- CÁMARA ----------------

    private void ApplyLook()
    {
        // El ratón ya da delta por frame; el stick da un valor -1..1 que necesita deltaTime
        float scale = lookIsMouse ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

        float yaw = lookInput.x * scale;
        float mouseY = lookInput.y * scale;

        transform.Rotate(Vector3.up * yaw);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraHolder != null)
            cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateFov()
    {
        if (playerCamera == null) return;

        // Usamos las banderas directas (no CurrentState) porque el estado Airborne tiene
        // prioridad en la máquina de estados y, si no, el FOV bajaría al saltar aunque
        // sigas esprintando en el aire.
        float targetFov = baseFov;
        if (isSliding)
            targetFov = baseFov + slideFovBoost;
        else if (isSprinting)
            targetFov = baseFov + sprintFovBoost;

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fovLerpSpeed * Time.deltaTime);
    }

    // ---------------- MOVIMIENTO NORMAL ----------------

    private void HandleMovement()
    {
        Vector3 inputDir = (transform.right * moveInput.x + transform.forward * moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        // Sin "&& isGrounded": si saltas mientras esprintas y sigues pulsando Sprint + adelante,
        // el objetivo de velocidad en el aire sigue siendo el de sprint, así que el salto no te frena.
        isSprinting = sprintHeld && moveInput.y > 0.1f && !isSliding;

        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = inputDir * targetSpeed;

        float accel = acceleration * (isGrounded ? 1f : airControlMultiplier);
        currentMoveVelocity = Vector3.MoveTowards(currentMoveVelocity, targetVelocity, accel * Time.deltaTime * targetSpeed);

        // Si hay un slide "guardado" en el buffer, seguimos esprintando y ya estamos en el suelo, lo disparamos ya.
        bool canStartSlide = slideBufferTimer > 0f && isGrounded && slideCooldownTimer <= 0f && sprintHeld;
        if (canStartSlide)
        {
            Vector3 dir = inputDir.sqrMagnitude > 0.01f ? inputDir : transform.forward;
            StartSlide(dir, slideBufferedInAir);
            slideBufferTimer = 0f;
        }
    }

    // ---------------- SALTO Y GRAVEDAD ----------------

    private void ConsumeJump()
    {
        bool canJump = jumpBufferTimer > 0f && coyoteTimer > 0f && !isSliding;
        if (canJump)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }
    }

    private void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = groundedGravity;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    // ---------------- SLIDE ----------------

    private void HandleTimers()
    {
        // Coyote time: se recarga mientras estás en el suelo, cuenta atrás en cuanto lo dejas.
        coyoteTimer = isGrounded ? coyoteTime : coyoteTimer - Time.deltaTime;

        if (jumpBufferTimer > 0f)
            jumpBufferTimer -= Time.deltaTime;

        if (slideCooldownTimer > 0f)
            slideCooldownTimer -= Time.deltaTime;

        if (slideBufferTimer > 0f)
            slideBufferTimer -= Time.deltaTime;

        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f || !isGrounded)
            {
                EndSlide();
            }
        }
    }

    private void StartSlide(Vector3 inputDir, bool boosted)
    {
        isSliding = true;
        slideTimer = slideDuration;
        slideCooldownTimer = slideCooldown;
        slideDirection = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : transform.forward;

        currentSlideMaxSpeed = boosted ? slideSpeed * airSlideBoostMultiplier : slideSpeed;

        controller.height = slideControllerHeight;
        controller.center = new Vector3(controller.center.x, slideControllerHeight / 2f, controller.center.z);

        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = defaultCameraLocalY + slideCameraHeightOffset;
            cameraHolder.localPosition = camPos;
        }
    }

    private void ApplySlideMovement()
    {
        float t = 1f - (slideTimer / slideDuration);
        float speedNow = Mathf.Lerp(currentSlideMaxSpeed, walkSpeed * 0.5f, t);
        currentMoveVelocity = slideDirection * speedNow;
    }

    private void EndSlide()
    {
        isSliding = false;

        controller.height = standingControllerHeight;
        controller.center = new Vector3(controller.center.x, standingControllerHeight / 2f, controller.center.z);

        if (cameraHolder != null)
        {
            Vector3 camPos = cameraHolder.localPosition;
            camPos.y = defaultCameraLocalY;
            cameraHolder.localPosition = camPos;
        }
    }

    // ---------------- MÁQUINA DE ESTADOS ----------------

    private void UpdateMovementState()
    {
        PlayerMovementState newState;

        if (!isGrounded)
            newState = PlayerMovementState.Airborne;
        else if (isSliding)
            newState = PlayerMovementState.Sliding;
        else if (isSprinting)
            newState = PlayerMovementState.Sprinting;
        else if (moveInput.sqrMagnitude > 0.01f)
            newState = PlayerMovementState.Walking;
        else
            newState = PlayerMovementState.Idle;

        if (newState == CurrentState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
    }
}