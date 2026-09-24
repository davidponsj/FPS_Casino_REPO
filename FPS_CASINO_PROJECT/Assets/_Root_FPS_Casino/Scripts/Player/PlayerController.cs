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
/// Incluye "slide buffering": si pulsas Slide mientras estás en el aire (por ejemplo
/// justo tras saltar), el input se guarda un pequeño margen de tiempo y el deslizamiento
/// se ejecuta automáticamente en el instante en que aterrizas, con un boost de velocidad
/// (mecánica típica de "jump slide" / bunny hop de shooters tipo COD).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform cameraHolder; // Empty vacío hijo del player, a la altura de los ojos
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

    [Header("Cámara / Mirada")]
    [SerializeField] private float mouseSensitivity = 0.12f; // el delta del ratón viene en píxeles por frame, sensibilidad mucho menor que con Input Manager viejo
    [SerializeField] private float gamepadSensitivity = 120f; // stick derecho: valor -1..1, se multiplica por deltaTime
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Deslizamiento (Slide)")]
    [SerializeField] private float slideSpeed = 11f;
    [SerializeField] private float slideDuration = 0.75f;
    [SerializeField] private float slideCooldown = 0.5f;
    [SerializeField] private float slideCameraHeightOffset = -0.6f; // baja la cámara al deslizar
    [SerializeField] private float slideControllerHeight = 1f;      // altura del CharacterController al deslizar
    [SerializeField] private float standingControllerHeight = 2f;

    [Header("Jump Slide (buffer + boost)")]
    [Tooltip("Margen en segundos durante el cual pulsar Slide en el aire queda 'guardado' para ejecutarse al aterrizar.")]
    [SerializeField] private float slideBufferWindow = 0.15f;
    [Tooltip("Multiplicador de velocidad del slide cuando viene de un salto (jump slide). 1 = sin boost.")]
    [SerializeField] private float airSlideBoostMultiplier = 1.35f;

    // Input recibido desde Player Input (Invoke Unity Events)
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool lookIsMouse; // para decidir si aplicar deltaTime o no al mirar
    private bool sprintHeld;
    private bool jumpQueued;

    // Estado interno
    private float pitch;
    private Vector3 velocity;              // velocidad vertical (gravedad/salto)
    private Vector3 currentMoveVelocity;   // velocidad horizontal suavizada
    private bool isGrounded;
    private bool wasGroundedLastFrame;
    private bool isSprinting;
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private Vector3 slideDirection;
    private float currentSlideMaxSpeed;    // velocidad tope del slide actual (con o sin boost)
    private float defaultCameraLocalY;

    // Buffer de slide
    private float slideBufferTimer;
    private bool slideBufferedInAir;

    public bool IsSliding => isSliding;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (cameraHolder != null)
            defaultCameraLocalY = cameraHolder.localPosition.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        wasGroundedLastFrame = isGrounded;
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
        if (context.performed) jumpQueued = true;
    }

    public void OnSlide(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

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

    // ---------------- MOVIMIENTO NORMAL ----------------

    private void HandleMovement()
    {
        Vector3 inputDir = (transform.right * moveInput.x + transform.forward * moveInput.y);
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        isSprinting = sprintHeld && moveInput.y > 0.1f && isGrounded && !isSliding;

        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = inputDir * targetSpeed;

        float accel = acceleration * (isGrounded ? 1f : airControlMultiplier);
        currentMoveVelocity = Vector3.MoveTowards(currentMoveVelocity, targetVelocity, accel * Time.deltaTime * targetSpeed);

        // Si hay un slide "guardado" en el buffer y ya estamos en el suelo, lo disparamos ya.
        bool canStartSlide = slideBufferTimer > 0f && isGrounded && slideCooldownTimer <= 0f;
        if (canStartSlide)
        {
            // Dirección: si venimos cayendo, usa hacia donde mira el jugador si no hay input lateral
            Vector3 dir = inputDir.sqrMagnitude > 0.01f ? inputDir : transform.forward;
            StartSlide(dir, slideBufferedInAir);
            slideBufferTimer = 0f;
        }
    }

    // ---------------- SALTO Y GRAVEDAD ----------------

    private void ConsumeJump()
    {
        if (jumpQueued && isGrounded && !isSliding)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        jumpQueued = false;
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
}