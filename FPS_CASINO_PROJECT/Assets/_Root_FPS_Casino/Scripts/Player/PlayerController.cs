using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador principal del jugador FPS, pensado para usarse junto a un
/// componente Player Input con Behavior = "Invoke Unity Events".
/// En el Inspector del Player Input, dentro de "Events > Player", conecta
/// cada acción (Move, Look, Sprint, Jump, Slide) al método correspondiente
/// de este script (OnMove, OnLook, OnSprint, OnJump, OnSlide).
/// Requiere un CharacterController en el mismo GameObject (con el pivote en los pies)
/// y una cámara hija asignada. No depende de ningún manager externo: todo el estado vive aquí.
///
/// Incluye:
/// - Momentum: la velocidad por encima del sprint no se corta de golpe, se disipa poco a poco
///   (mucho más lento en el aire). Slide, slide-jump y walljump suman velocidad hasta un tope.
/// - Control aéreo estilo Quake: en el aire el input solo añade velocidad, nunca la recorta,
///   así que un salto no te frena y se puede "strafear" para girar sin perder inercia.
/// - Slide físico: hereda la velocidad que llevas, tiene fricción, acelera cuesta abajo,
///   se puede dirigir un poco y se puede cancelar con un salto conservando el momentum.
///   Solo se puede iniciar mientras se mantiene pulsado Sprint. Si hay techo encima, no te
///   levantas hasta que haya hueco.
/// - Slide buffering + boost: pulsar Slide en el aire lo ejecuta al aterrizar, más rápido (jump slide).
/// - Wallrun: en el aire, avanzando y con velocidad suficiente, te enganchas a paredes laterales.
///   La gravedad empieza muy reducida y va recuperándose hasta que te sueltas.
/// - Walljump: saltar durante un wallrun (o justo después), o pegado a una pared en el aire,
///   te impulsa hacia fuera conservando la velocidad paralela a la pared. No se puede repetir
///   en la misma pared hasta tocar el suelo (evita escalar una pared infinita).
/// - Coyote time + jump buffer: salto más permisivo, como en cualquier shooter pulido.
/// - Detección de suelo con SphereCast, pendientes (sin "rebotes" al bajar cuestas) y
///   recorte de velocidad al chocar con paredes y techos para no acumular velocidad fantasma.
/// - Efectos de cámara: FOV según estado y velocidad, inclinación en wallrun/strafe y
///   "dip" al aterrizar.
/// - Máquina de estados simple (PlayerMovementState) con eventos, pensada para engancharse
///   después a un Animator o a sonidos sin tocar el resto del código.
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
        Airborne,
        WallRunning
    }

    [Header("Referencias")]
    [SerializeField] private Transform cameraHolder; // Empty vacío hijo del player, a la altura de los ojos
    [SerializeField] private Camera playerCamera;     // Camera hija del cameraHolder (para el FOV kick)
    [SerializeField] private CharacterController controller;

    [Header("Detección de suelo")]
    [Tooltip("Capas que cuentan como suelo / obstáculo (también se usan para comprobar el techo al levantarse del slide).")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Distancia extra por debajo de los pies a la que se sigue considerando que estás en el suelo.")]
    [SerializeField] private float groundCheckDistance = 0.15f;

    [Header("Movimiento")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8.5f;
    [Tooltip("Aceleración en el suelo (m/s²) hasta la velocidad objetivo.")]
    [SerializeField] private float groundAcceleration = 70f;
    [Tooltip("Frenada en el suelo (m/s²) al soltar el input.")]
    [SerializeField] private float groundDeceleration = 55f;

    [Header("Momentum")]
    [Tooltip("Velocidad horizontal máxima absoluta (m/s), sumando slides, walljumps, etc.")]
    [SerializeField] private float maxMomentumSpeed = 22f;
    [Tooltip("m/s² que se pierden en el suelo mientras vas más rápido que el sprint.")]
    [SerializeField] private float groundMomentumDecay = 9f;
    [Tooltip("m/s² que se pierden en el aire mientras vas más rápido que el sprint. Bajo = mucha inercia.")]
    [SerializeField] private float airMomentumDecay = 0.6f;
    [Tooltip("Grados por segundo a los que el input puede redirigir el momentum en el suelo.")]
    [SerializeField] private float groundTurnRate = 540f;
    [Tooltip("Frenada extra (m/s²) al pulsar en sentido contrario a la inercia en el suelo.")]
    [SerializeField] private float brakeDeceleration = 25f;

    [Header("Control aéreo")]
    [Tooltip("Aceleración en el aire (m/s²). Solo añade velocidad en la dirección del input, nunca la recorta.")]
    [SerializeField] private float airAcceleration = 25f;
    [Tooltip("Rozamiento del aire (m/s²) sin input cuando vas a velocidad normal.")]
    [SerializeField] private float airDeceleration = 2f;

    [Header("Salto y gravedad")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f; // pequeña fuerza hacia abajo para mantener pegado al suelo
    [SerializeField] private float maxFallSpeed = 40f;
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
    [SerializeField] private float wallRunFovBoost = 10f;
    [Tooltip("FOV extra que se va sumando según la velocidad supera el sprint, hasta maxMomentumSpeed.")]
    [SerializeField] private float momentumFovBoost = 8f;
    [SerializeField] private float fovLerpSpeed = 8f;

    [Header("Inclinación de cámara")]
    [Tooltip("Grados de roll durante el wallrun (positivo = se inclina alejándose de la pared).")]
    [SerializeField] private float wallRunCameraTilt = 12f;
    [Tooltip("Grados de roll al moverte lateralmente.")]
    [SerializeField] private float strafeCameraTilt = 1.5f;
    [SerializeField] private float cameraTiltSpeed = 8f;

    [Header("Aterrizaje")]
    [Tooltip("Velocidad de caída (m/s) mínima para que la cámara haga el 'dip' al aterrizar.")]
    [SerializeField] private float landingMinImpact = 4f;
    [SerializeField] private float landingDipStrength = 0.06f;
    [SerializeField] private float maxLandingDipVelocity = 2f;
    [SerializeField] private float landingSpringStiffness = 150f;
    [SerializeField] private float landingSpringDamping = 18f;

    [Header("Deslizamiento (Slide)")]
    [Tooltip("Velocidad mínima con la que arranca un slide (si ya vas más rápido, se conserva tu velocidad + slideEntryBoost).")]
    [SerializeField] private float slideSpeed = 11f;
    [Tooltip("Velocidad que se suma a la actual al iniciar el slide.")]
    [SerializeField] private float slideEntryBoost = 1.5f;
    [Tooltip("Duración máxima del slide en llano. Cuesta abajo no se consume.")]
    [SerializeField] private float slideDuration = 0.75f;
    [SerializeField] private float slideCooldown = 0.5f;
    [Tooltip("Fricción del slide (m/s²).")]
    [SerializeField] private float slideFriction = 6f;
    [Tooltip("Multiplicador de la gravedad a lo largo de la pendiente: >0 acelera cuesta abajo y frena cuesta arriba.")]
    [SerializeField] private float slideSlopeAcceleration = 1f;
    [Tooltip("Grados por segundo que puedes girar durante el slide con el input.")]
    [SerializeField] private float slideSteering = 60f;
    [Tooltip("Por debajo de esta velocidad el slide termina.")]
    [SerializeField] private float slideMinSpeed = 3f;
    [SerializeField] private float slideCameraHeightOffset = -0.6f; // baja la cámara al deslizar
    [SerializeField] private float slideControllerHeight = 1f;      // altura del CharacterController al deslizar
    [SerializeField] private float standingControllerHeight = 2f;
    [Tooltip("Velocidad (1/s) de la transición de altura al agacharse / levantarse.")]
    [SerializeField] private float crouchTransitionSpeed = 12f;
    [Tooltip("Margen en segundos durante el cual pulsar Slide en el aire queda 'guardado' para ejecutarse al aterrizar.")]
    [SerializeField] private float slideBufferWindow = 0.15f;
    [Tooltip("Multiplicador de velocidad del slide cuando viene de un salto (jump slide). 1 = sin boost.")]
    [SerializeField] private float airSlideBoostMultiplier = 1.35f;

    [Header("Wallrun")]
    [SerializeField] private bool enableWallRun = true;
    [Tooltip("Capas sobre las que se puede hacer wallrun / walljump.")]
    [SerializeField] private LayerMask wallMask = ~0;
    [Tooltip("Distancia desde la superficie de la cápsula a la que se detecta una pared.")]
    [SerializeField] private float wallCheckDistance = 0.4f;
    [Tooltip("|normal.y| máximo para que una superficie cuente como pared (0 = totalmente vertical).")]
    [SerializeField] private float wallMaxNormalY = 0.3f;
    [Tooltip("Altura mínima sobre el suelo para poder engancharse a una pared.")]
    [SerializeField] private float wallRunMinHeight = 1f;
    [Tooltip("Velocidad horizontal mínima para empezar un wallrun. Debe ser mayor que walkSpeed para que andando no te enganches: hay que llegar esprintando o con momentum.")]
    [SerializeField] private float wallRunMinSpeed = 7.5f;
    [Tooltip("Velocidad mínima que se mantiene corriendo por la pared (si llegas más rápido, se conserva con poco desgaste).")]
    [SerializeField] private float wallRunSpeed = 9.5f;
    [SerializeField] private float wallRunMomentumDecay = 1.5f;
    [SerializeField] private float wallRunDuration = 1.6f;
    [Tooltip("Gravedad al inicio del wallrun. Se interpola hacia la gravedad normal según se agota el tiempo.")]
    [SerializeField] private float wallRunGravity = -2f;
    [Tooltip("Velocidad vertical máxima que se conserva al engancharse (si venías subiendo de un salto).")]
    [SerializeField] private float wallRunMaxEntryUpSpeed = 3f;
    [Tooltip("Fuerza hacia la pared para mantenerte pegado en esquinas y curvas.")]
    [SerializeField] private float wallRunStickForce = 2f;
    [Tooltip("Impulso hacia fuera de la pared al soltarte sin saltar.")]
    [SerializeField] private float wallRunExitPush = 1.5f;
    [Tooltip("Tiempo tras soltarte de una pared antes de poder engancharte a otra.")]
    [SerializeField] private float wallReattachTime = 0.25f;

    [Header("Walljump")]
    [SerializeField] private float wallJumpUpForce = 7.5f;
    [SerializeField] private float wallJumpSideForce = 7f;
    [Tooltip("Velocidad extra en la dirección en la que corrías por la pared.")]
    [SerializeField] private float wallJumpForwardBoost = 1.5f;
    [Tooltip("Margen tras soltarte de un wallrun en el que todavía puedes hacer walljump.")]
    [SerializeField] private float wallJumpCoyoteTime = 0.2f;
    [Tooltip("Permite walljump estando pegado a una pared en el aire aunque no estés haciendo wallrun.")]
    [SerializeField] private bool enableWallKick = true;
    [Tooltip("Tiempo tras un walljump durante el que el input aéreo no actúa, para que no anule el impulso.")]
    [SerializeField] private float wallJumpControlLockTime = 0.2f;

    /// <summary>Se dispara cada vez que cambia el estado de movimiento. Útil para animaciones/sonido.</summary>
    public event Action<PlayerMovementState> OnStateChanged;
    /// <summary>Salto normal desde el suelo (incluye slide-jump).</summary>
    public event Action OnJumped;
    /// <summary>Walljump; recibe la normal de la pared.</summary>
    public event Action<Vector3> OnWallJumped;
    /// <summary>Aterrizaje; recibe la velocidad de caída (m/s, positiva).</summary>
    public event Action<float> OnLanded;

    public PlayerMovementState CurrentState { get; private set; } = PlayerMovementState.Idle;

    // Input recibido desde Player Input (Invoke Unity Events)
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool lookIsMouse; // para decidir si aplicar deltaTime o no al mirar
    private bool sprintHeld;

    // Estado interno
    private float pitch;
    private Vector3 velocity; // velocidad completa (horizontal + vertical) en m/s
    private bool isGrounded;
    private bool wasGrounded;
    private Vector3 groundNormal = Vector3.up;
    private bool isSprinting;
    private bool isSliding;
    private float slideTimer;
    private float slideCooldownTimer;
    private float crouchAmount; // 0 = de pie, 1 = altura de slide
    private float defaultCameraLocalY;
    private float baseFov;

    // Coyote time / jump buffer
    private float coyoteTimer;
    private float jumpBufferTimer;

    // Buffer de slide
    private float slideBufferTimer;
    private bool slideBufferedInAir;

    // Wallrun / walljump
    private bool isWallRunning;
    private float wallRunTimer;
    private Vector3 wallNormal;
    private Collider wallCollider;
    private int wallSide;                 // +1 pared a la derecha, -1 a la izquierda
    private Collider lastWallCollider;    // última pared usada: no se puede repetir hasta tocar suelo
    private Vector3 lastWallNormal;
    private float wallReattachTimer;
    private float wallCoyoteTimer;
    private float airControlLockTimer;

    // Efectos de cámara
    private float cameraRoll;
    private float landingOffset;
    private float landingOffsetVelocity;

    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    public bool IsSliding => isSliding;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsWallRunning => isWallRunning;
    /// <summary>Velocidad actual completa (m/s).</summary>
    public Vector3 Velocity => velocity;
    /// <summary>Magnitud de la velocidad horizontal actual, usada por las armas para calcular la dispersión de disparo.</summary>
    public float CurrentHorizontalSpeed => HorizontalVelocity.magnitude;

    private Vector3 HorizontalVelocity => new Vector3(velocity.x, 0f, velocity.z);
    private Vector3 CapsuleCenter => transform.TransformPoint(controller.center);
    private Vector3 FeetPosition => CapsuleCenter - Vector3.up * (controller.height * 0.5f);

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
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // juego en pausa (timeScale = 0)

        UpdateGroundCheck();
        ApplyLook();
        HandleTimers(dt);

        isSprinting = sprintHeld && moveInput.y > 0.1f && !isSliding;

        UpdateWallRun();
        HandleJump();

        if (isSliding)
            ApplySlideMovement(dt);
        else if (isWallRunning)
            ApplyWallRunMovement(dt);
        else
            HandleMovement(dt);

        ApplyGravity(dt); // la gravedad se aplica siempre, tanto de pie como deslizándote
        MoveController(dt);

        UpdateCrouch(dt);
        UpdateCameraEffects(dt);
        UpdateMovementState();
        UpdateFov(dt);
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

    // ---------------- SUELO ----------------

    private void UpdateGroundCheck()
    {
        wasGrounded = isGrounded;
        groundNormal = Vector3.up;

        float radius = controller.radius * 0.9f;
        float distance = controller.height * 0.5f - radius + controller.skinWidth + groundCheckDistance;

        bool hitWalkableGround = false;
        if (SphereCastIgnoringSelf(CapsuleCenter, radius, Vector3.down, distance, groundMask, out RaycastHit hit)
            && Vector3.Angle(hit.normal, Vector3.up) <= controller.slopeLimit)
        {
            hitWalkableGround = true;
            groundNormal = hit.normal;
        }

        // Mientras subimos (salto, walljump) nunca estamos en el suelo, aunque el cast aún lo roce.
        isGrounded = (hitWalkableGround || controller.isGrounded) && velocity.y <= 0.01f;

        if (isGrounded && !wasGrounded)
            Land();
    }

    private void Land()
    {
        float impact = Mathf.Max(0f, -velocity.y);

        if (impact >= landingMinImpact)
            landingOffsetVelocity -= Mathf.Min(impact * landingDipStrength, maxLandingDipVelocity);

        // Tocar el suelo "recarga" todas las paredes.
        StopWallRun(false);
        lastWallCollider = null;
        wallCoyoteTimer = 0f;
        airControlLockTimer = 0f;

        OnLanded?.Invoke(impact);
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
    }

    private void UpdateCameraEffects(float dt)
    {
        // Roll: en wallrun se inclina alejándose de la pared; al strafear, un leve lean.
        float targetRoll = isWallRunning
            ? wallSide * wallRunCameraTilt
            : -moveInput.x * strafeCameraTilt;
        cameraRoll = Mathf.Lerp(cameraRoll, targetRoll, 1f - Mathf.Exp(-cameraTiltSpeed * dt));

        // Muelle amortiguado para el "dip" al aterrizar.
        float springAccel = -landingSpringStiffness * landingOffset - landingSpringDamping * landingOffsetVelocity;
        landingOffsetVelocity += springAccel * dt;
        landingOffset += landingOffsetVelocity * dt;

        if (cameraHolder == null) return;

        Vector3 camPos = cameraHolder.localPosition;
        camPos.y = defaultCameraLocalY + slideCameraHeightOffset * crouchAmount + landingOffset;
        cameraHolder.localPosition = camPos;
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, cameraRoll);
    }

    private void UpdateFov(float dt)
    {
        if (playerCamera == null) return;

        // Usamos las banderas directas (no CurrentState) porque el estado Airborne tiene
        // prioridad en la máquina de estados y, si no, el FOV bajaría al saltar aunque
        // sigas esprintando en el aire.
        float boost = 0f;
        if (isSliding)
            boost = slideFovBoost;
        else if (isWallRunning)
            boost = wallRunFovBoost;
        else if (isSprinting)
            boost = sprintFovBoost;

        // Cuanto más momentum por encima del sprint, más FOV: transmite la sensación de velocidad.
        boost += Mathf.InverseLerp(sprintSpeed, maxMomentumSpeed, CurrentHorizontalSpeed) * momentumFovBoost;

        float targetFov = baseFov + boost;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovLerpSpeed * dt));
    }

    // ---------------- MOVIMIENTO NORMAL ----------------

    private void HandleMovement(float dt)
    {
        Vector3 inputDir = GetInputDirection();
        float inputMagnitude = inputDir.magnitude;
        Vector3 wishDir = inputMagnitude > 0.0001f ? inputDir / inputMagnitude : Vector3.zero;

        // Sin "&& isGrounded": si saltas mientras esprintas y sigues pulsando Sprint + adelante,
        // el objetivo de velocidad en el aire sigue siendo el de sprint, así que el salto no te frena.
        float targetSpeed = (isSprinting ? sprintSpeed : walkSpeed) * inputMagnitude;

        if (isGrounded)
            ApplyGroundAcceleration(wishDir, targetSpeed, dt);
        else
            ApplyAirAcceleration(airControlLockTimer > 0f ? Vector3.zero : wishDir, targetSpeed, dt);

        // Si hay un slide "guardado" en el buffer, seguimos esprintando y ya estamos en el suelo, lo disparamos ya.
        bool canStartSlide = slideBufferTimer > 0f && isGrounded && slideCooldownTimer <= 0f && sprintHeld;
        if (canStartSlide)
        {
            StartSlide(slideBufferedInAir);
            slideBufferTimer = 0f;
        }
    }

    /// <summary>
    /// Por debajo del sprint: aceleración/frenada clásica y responsiva.
    /// Por encima (momentum de slide, walljump...): la velocidad se conserva y solo se disipa
    /// poco a poco, mientras el input puede redirigirla o frenarla.
    /// </summary>
    private void ApplyGroundAcceleration(Vector3 wishDir, float targetSpeed, float dt)
    {
        Vector3 horizontal = HorizontalVelocity;
        float speed = horizontal.magnitude;
        float momentumThreshold = Mathf.Max(targetSpeed, sprintSpeed);
        bool hasInput = wishDir.sqrMagnitude > 0.0001f;

        if (speed > momentumThreshold)
        {
            Vector3 dir = horizontal / speed;
            float decay = groundMomentumDecay;

            if (hasInput)
            {
                float alignment = Vector3.Dot(dir, wishDir);
                if (alignment > -0.5f)
                    dir = Vector3.RotateTowards(dir, wishDir, groundTurnRate * Mathf.Deg2Rad * dt, 0f);
                if (alignment < 0f)
                    decay += brakeDeceleration * -alignment;
            }

            speed = Mathf.Max(speed - decay * dt, momentumThreshold);
            horizontal = dir * speed;
        }
        else
        {
            float rate = hasInput ? groundAcceleration : groundDeceleration;
            horizontal = Vector3.MoveTowards(horizontal, wishDir * targetSpeed, rate * dt);
        }

        SetHorizontalVelocity(horizontal);
    }

    /// <summary>
    /// Aceleración aérea estilo Quake: solo se añade velocidad en la dirección del input
    /// hasta targetSpeed (proyectada), nunca se resta. Así el salto conserva la inercia y el
    /// input sirve para corregir la trayectoria.
    /// </summary>
    private void ApplyAirAcceleration(Vector3 wishDir, float targetSpeed, float dt)
    {
        Vector3 horizontal = HorizontalVelocity;

        if (wishDir.sqrMagnitude > 0.0001f)
        {
            float currentAlongWish = Vector3.Dot(horizontal, wishDir);
            float addSpeed = targetSpeed - currentAlongWish;
            if (addSpeed > 0f)
                horizontal += wishDir * Mathf.Min(airAcceleration * dt, addSpeed);
        }

        float speed = horizontal.magnitude;
        if (speed > sprintSpeed)
        {
            float newSpeed = Mathf.Max(speed - airMomentumDecay * dt, sprintSpeed);
            horizontal *= newSpeed / speed;
        }
        else if (wishDir.sqrMagnitude <= 0.0001f)
        {
            horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, airDeceleration * dt);
        }

        SetHorizontalVelocity(horizontal);
    }

    // ---------------- SALTO Y GRAVEDAD ----------------

    private void HandleJump()
    {
        if (jumpBufferTimer <= 0f) return;

        // 1. Salto normal (con coyote time). Desde un slide también: conserva el momentum.
        if (coyoteTimer > 0f && !isWallRunning && (!isSliding || CanStand()))
        {
            GroundJump();
            return;
        }

        if (isGrounded) return;

        // 2. Walljump desde un wallrun.
        if (isWallRunning)
        {
            WallJump(wallNormal, wallCollider);
            return;
        }

        // 3. Walljump justo después de soltarte de un wallrun.
        if (wallCoyoteTimer > 0f && lastWallCollider != null)
        {
            WallJump(lastWallNormal, lastWallCollider);
            return;
        }

        // 4. Wall kick: pegado a una pared en el aire, sin wallrun.
        if (enableWallKick && FindNearbyWall(out RaycastHit hit) && !IsLastWall(hit.collider, hit.normal))
            WallJump(hit.normal, hit.collider);
    }

    private void GroundJump()
    {
        if (isSliding) EndSlide();

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isGrounded = false;

        OnJumped?.Invoke();
    }

    private void WallJump(Vector3 normal, Collider wall)
    {
        StopWallRun(false);

        Vector3 flatNormal = new Vector3(normal.x, 0f, normal.z).normalized;

        // Se conserva la velocidad paralela a la pared y se le suma el empuje hacia fuera.
        Vector3 alongWall = Vector3.ProjectOnPlane(HorizontalVelocity, flatNormal);
        Vector3 boostDir = alongWall.sqrMagnitude > 0.01f ? alongWall.normalized : Vector3.zero;
        Vector3 horizontal = alongWall + boostDir * wallJumpForwardBoost + flatNormal * wallJumpSideForce;

        SetHorizontalVelocity(Vector3.ClampMagnitude(horizontal, maxMomentumSpeed));
        velocity.y = wallJumpUpForce;

        lastWallCollider = wall;
        lastWallNormal = normal;
        wallReattachTimer = wallReattachTime;
        wallCoyoteTimer = 0f;
        airControlLockTimer = wallJumpControlLockTime;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isGrounded = false;

        OnWallJumped?.Invoke(normal);
    }

    private void ApplyGravity(float dt)
    {
        if (isGrounded && velocity.y <= 0f)
        {
            velocity.y = groundedGravity;
            return;
        }

        float currentGravity = gravity;
        if (isWallRunning)
        {
            // Empieza muy ligera y se va recuperando: el wallrun "cae" en arco de forma natural.
            float t = 1f - Mathf.Clamp01(wallRunTimer / wallRunDuration);
            currentGravity = Mathf.Lerp(wallRunGravity, gravity, t * t);
        }

        velocity.y = Mathf.Max(velocity.y + currentGravity * dt, -maxFallSpeed);
    }

    private void MoveController(float dt)
    {
        Vector3 horizontal = Vector3.ClampMagnitude(HorizontalVelocity, maxMomentumSpeed);
        SetHorizontalVelocity(horizontal);

        Vector3 move;
        if (isGrounded)
        {
            // Proyectamos sobre la pendiente para no "rebotar" al bajar cuestas.
            Vector3 slopeMove = Vector3.ProjectOnPlane(horizontal, groundNormal);
            if (slopeMove.sqrMagnitude > 0.0001f)
                slopeMove = slopeMove.normalized * horizontal.magnitude;
            move = slopeMove + Vector3.up * velocity.y;
        }
        else
        {
            move = velocity;
        }

        CollisionFlags flags = controller.Move(move * dt);

        if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Vector3 normal = hit.normal;

        if (Mathf.Abs(normal.y) < 0.7f)
        {
            // Los escalones también devuelven normales "de pared": los ignoramos para no frenar en escaleras.
            if (hit.point.y <= FeetPosition.y + controller.stepOffset) return;

            // Recortamos la velocidad que va contra la pared para no acumular momentum fantasma.
            Vector3 flatNormal = new Vector3(normal.x, 0f, normal.z).normalized;
            Vector3 horizontal = HorizontalVelocity;
            float into = Vector3.Dot(horizontal, flatNormal);
            if (into < 0f)
                SetHorizontalVelocity(horizontal - flatNormal * into);
        }
        else if (normal.y < -0.7f && velocity.y > 0f)
        {
            velocity.y = 0f; // techo
        }
    }

    // ---------------- TIMERS ----------------

    private void HandleTimers(float dt)
    {
        // Coyote time: se recarga mientras estás en el suelo, cuenta atrás en cuanto lo dejas.
        coyoteTimer = isGrounded ? coyoteTime : coyoteTimer - dt;

        Tick(ref jumpBufferTimer, dt);
        Tick(ref slideCooldownTimer, dt);
        Tick(ref slideBufferTimer, dt);
        Tick(ref wallReattachTimer, dt);
        Tick(ref wallCoyoteTimer, dt);
        Tick(ref airControlLockTimer, dt);
    }

    private static void Tick(ref float timer, float dt)
    {
        if (timer > 0f) timer -= dt;
    }

    // ---------------- SLIDE ----------------

    private void StartSlide(bool boosted)
    {
        Vector3 horizontal = HorizontalVelocity;
        float speed = horizontal.magnitude;

        Vector3 dir;
        if (speed > 1f)
        {
            dir = horizontal / speed;
        }
        else
        {
            Vector3 inputDir = GetInputDirection();
            dir = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : transform.forward;
        }

        // Se conserva la velocidad que llevas y se le suma un pequeño empujón,
        // con un mínimo de slideSpeed. El jump slide multiplica ambos.
        float multiplier = boosted ? airSlideBoostMultiplier : 1f;
        float entrySpeed = Mathf.Max(slideSpeed * multiplier, speed + slideEntryBoost * multiplier);
        SetHorizontalVelocity(dir * Mathf.Min(entrySpeed, maxMomentumSpeed));

        isSliding = true;
        slideTimer = slideDuration;
        slideCooldownTimer = slideCooldown;
    }

    private void ApplySlideMovement(float dt)
    {
        Vector3 horizontal = HorizontalVelocity;

        // Pendiente: la gravedad proyectada sobre el suelo acelera cuesta abajo y frena cuesta arriba.
        Vector3 slopeGravity = Vector3.ProjectOnPlane(Vector3.up * gravity, groundNormal);
        slopeGravity.y = 0f;
        horizontal += slopeGravity * (slideSlopeAcceleration * dt);

        float speed = horizontal.magnitude;
        Vector3 dir = speed > 0.01f ? horizontal / speed : transform.forward;
        bool goingDownhill = isGrounded && Vector3.Dot(slopeGravity, dir) > 0.5f;

        speed = Mathf.Max(0f, speed - slideFriction * dt);

        Vector3 inputDir = GetInputDirection();
        if (inputDir.sqrMagnitude > 0.01f)
            dir = Vector3.RotateTowards(dir, inputDir.normalized, slideSteering * Mathf.Deg2Rad * dt, 0f);

        // Cuesta abajo el slide no se agota: puedes bajar una rampa entera deslizando.
        if (!goingDownhill)
            slideTimer -= dt;

        bool shouldEnd = !isGrounded || slideTimer <= 0f || speed < slideMinSpeed;
        if (shouldEnd)
        {
            if (CanStand())
                EndSlide();
            else
                speed = Mathf.Max(speed, slideMinSpeed); // hay techo encima: seguimos avanzando agachados
        }

        SetHorizontalVelocity(dir * speed);
    }

    private void EndSlide()
    {
        isSliding = false; // la altura y la cámara vuelven suavemente en UpdateCrouch
    }

    private void UpdateCrouch(float dt)
    {
        float target = isSliding ? 1f : 0f;
        if (Mathf.Approximately(crouchAmount, target)) return;

        crouchAmount = Mathf.MoveTowards(crouchAmount, target, crouchTransitionSpeed * dt);

        float height = Mathf.Lerp(standingControllerHeight, slideControllerHeight, crouchAmount);
        controller.height = height;
        controller.center = new Vector3(controller.center.x, height / 2f, controller.center.z);
    }

    /// <summary>Comprueba si hay hueco encima para volver a la altura de pie.</summary>
    private bool CanStand()
    {
        float missingHeight = standingControllerHeight - controller.height;
        if (missingHeight <= 0.01f) return true;

        float radius = controller.radius * 0.95f;
        Vector3 topSphere = FeetPosition + Vector3.up * (controller.height - controller.radius);
        return !SphereCastIgnoringSelf(topSphere, radius, Vector3.up, missingHeight + controller.skinWidth, groundMask, out _);
    }

    // ---------------- WALLRUN ----------------

    private void UpdateWallRun()
    {
        if (isWallRunning)
        {
            wallRunTimer -= Time.deltaTime;

            bool stillOnWall = CheckWall(-wallNormal, wallCheckDistance + 0.2f, out RaycastHit hit);
            bool pushingAway = Vector3.Dot(GetInputDirection(), wallNormal) > 0.5f;

            if (!enableWallRun || isGrounded || wallRunTimer <= 0f || moveInput.y < 0.1f || !stillOnWall || pushingAway)
            {
                StopWallRun(!isGrounded);
                return;
            }

            // Actualizamos la normal cada frame para poder seguir paredes curvas y esquinas abiertas.
            wallNormal = hit.normal;
            wallCollider = hit.collider;
            wallSide = Vector3.Dot(transform.right, -wallNormal) > 0f ? 1 : -1;
            return;
        }

        if (CanStartWallRun() && FindWallRunWall(out RaycastHit wallHit))
            StartWallRun(wallHit);
    }

    private bool CanStartWallRun()
    {
        if (!enableWallRun || isGrounded || isSliding) return false;
        if (wallReattachTimer > 0f) return false;
        if (moveInput.y < 0.1f) return false;
        if (CurrentHorizontalSpeed < wallRunMinSpeed) return false;

        // Hay que estar a cierta altura: evita engancharse al saltar un bordillo.
        return !SphereCastIgnoringSelf(FeetPosition + Vector3.up * 0.1f, 0.1f, Vector3.down, wallRunMinHeight, groundMask, out _);
    }

    private bool FindWallRunWall(out RaycastHit wallHit)
    {
        wallHit = default;
        float bestDistance = float.MaxValue;
        bool found = TryPickWallRunWall(transform.right, ref wallHit, ref bestDistance);
        found |= TryPickWallRunWall(-transform.right, ref wallHit, ref bestDistance);
        return found;
    }

    private bool TryPickWallRunWall(Vector3 direction, ref RaycastHit best, ref float bestDistance)
    {
        if (!CheckWall(direction, wallCheckDistance, out RaycastHit hit)) return false;
        if (IsLastWall(hit.collider, hit.normal)) return false;

        // Tienes que ir más o menos paralelo a la pared, no alejándote de ella ni de frente.
        Vector3 moveDir = HorizontalVelocity.normalized;
        Vector3 alongWall = Vector3.Cross(hit.normal, Vector3.up).normalized;
        if (Mathf.Abs(Vector3.Dot(moveDir, alongWall)) < 0.35f) return false;
        if (Vector3.Dot(moveDir, hit.normal) > 0.3f) return false;

        return TryPickCloser(hit, ref best, ref bestDistance);
    }

    private void StartWallRun(RaycastHit hit)
    {
        isWallRunning = true;
        wallRunTimer = wallRunDuration;
        wallNormal = hit.normal;
        wallCollider = hit.collider;
        wallSide = Vector3.Dot(transform.right, -wallNormal) > 0f ? 1 : -1;

        // Al engancharse se anula la caída y se limita la subida: arranque estable y predecible.
        velocity.y = Mathf.Clamp(velocity.y, 0f, wallRunMaxEntryUpSpeed);

        isSliding = false;
        slideBufferTimer = 0f;
    }

    private void ApplyWallRunMovement(float dt)
    {
        Vector3 horizontal = HorizontalVelocity;
        Vector3 alongWall = Vector3.Cross(wallNormal, Vector3.up);
        alongWall.y = 0f;
        alongWall.Normalize();

        Vector3 reference = horizontal.sqrMagnitude > 0.01f ? horizontal : transform.forward;
        if (Vector3.Dot(alongWall, reference) < 0f)
            alongWall = -alongWall;

        float speed = Vector3.Dot(horizontal, alongWall);
        speed = speed < wallRunSpeed
            ? Mathf.MoveTowards(speed, wallRunSpeed, groundAcceleration * 0.5f * dt)
            : Mathf.MoveTowards(speed, wallRunSpeed, wallRunMomentumDecay * dt);

        Vector3 flatNormal = new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;
        SetHorizontalVelocity(alongWall * speed - flatNormal * wallRunStickForce);
    }

    private void StopWallRun(bool pushOff)
    {
        if (!isWallRunning) return;

        isWallRunning = false;
        lastWallCollider = wallCollider;
        lastWallNormal = wallNormal;
        wallReattachTimer = wallReattachTime;
        wallCoyoteTimer = wallJumpCoyoteTime;

        if (pushOff)
        {
            Vector3 flatNormal = new Vector3(wallNormal.x, 0f, wallNormal.z).normalized;
            SetHorizontalVelocity(HorizontalVelocity + flatNormal * wallRunExitPush);
        }
    }

    /// <summary>Pared más cercana en cualquier dirección horizontal (para el wall kick).</summary>
    private bool FindNearbyWall(out RaycastHit wallHit)
    {
        wallHit = default;
        float bestDistance = float.MaxValue;
        bool found = false;
        if (CheckWall(transform.right, wallCheckDistance, out RaycastHit hit)) found |= TryPickCloser(hit, ref wallHit, ref bestDistance);
        if (CheckWall(-transform.right, wallCheckDistance, out hit)) found |= TryPickCloser(hit, ref wallHit, ref bestDistance);
        if (CheckWall(transform.forward, wallCheckDistance, out hit)) found |= TryPickCloser(hit, ref wallHit, ref bestDistance);
        if (CheckWall(-transform.forward, wallCheckDistance, out hit)) found |= TryPickCloser(hit, ref wallHit, ref bestDistance);
        return found;
    }

    private static bool TryPickCloser(RaycastHit hit, ref RaycastHit best, ref float bestDistance)
    {
        if (hit.distance >= bestDistance) return false;
        bestDistance = hit.distance;
        best = hit;
        return true;
    }

    private bool CheckWall(Vector3 direction, float distanceFromSurface, out RaycastHit hit)
    {
        direction.y = 0f;
        direction.Normalize();

        const float probeRadius = 0.2f;
        float distance = controller.radius - probeRadius + distanceFromSurface;

        return SphereCastIgnoringSelf(CapsuleCenter, probeRadius, direction, distance, wallMask, out hit)
            && Mathf.Abs(hit.normal.y) <= wallMaxNormalY;
    }

    private bool IsLastWall(Collider wall, Vector3 normal)
    {
        return lastWallCollider != null && wall == lastWallCollider && Vector3.Dot(normal, lastWallNormal) > 0.9f;
    }

    // ---------------- UTILIDADES ----------------

    private Vector3 GetInputDirection()
    {
        Vector3 inputDir = transform.right * moveInput.x + transform.forward * moveInput.y;
        return Vector3.ClampMagnitude(inputDir, 1f);
    }

    private void SetHorizontalVelocity(Vector3 horizontal)
    {
        velocity.x = horizontal.x;
        velocity.z = horizontal.z;
    }

    /// <summary>SphereCast que ignora los colliders del propio jugador (armas, hijos...) y los triggers.</summary>
    private bool SphereCastIgnoringSelf(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask mask, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, distance, mask, QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // solapado desde el inicio: normal no fiable

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                closest = hit;
                found = true;
            }
        }

        return found;
    }

    // ---------------- MÁQUINA DE ESTADOS ----------------

    private void UpdateMovementState()
    {
        PlayerMovementState newState;

        if (isWallRunning)
            newState = PlayerMovementState.WallRunning;
        else if (!isGrounded)
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

    private void OnDrawGizmosSelected()
    {
        CharacterController cc = controller != null ? controller : GetComponent<CharacterController>();
        if (cc == null) return;

        Vector3 center = transform.TransformPoint(cc.center);
        float reach = cc.radius + wallCheckDistance;

        Gizmos.color = isWallRunning ? Color.green : Color.cyan;
        Gizmos.DrawLine(center, center + transform.right * reach);
        Gizmos.DrawLine(center, center - transform.right * reach);

        if (isWallRunning)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(center, center + wallNormal);
        }
    }
}
