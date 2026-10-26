using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Botella de la mano izquierda. El Bartender llama a Give(ticket) y la botella aparece en la
/// mano con el color del ticket. Con ella puedes:
/// - Beber (F): se aplica el efecto de beber de esa botella (BottleEffect). Si dura un tiempo,
///   sale el temporizador en el HUD.
/// - Lanzar (Q): se aplica su efecto sobre ti (si tiene) y sale una botella física (ThrownBottle)
///   que se rompe al chocar: impacto en área y charco según el BottleEffect.
///
/// Qué hace cada botella lo dice su ticket (TicketData.bottleEffect).
/// En los dos casos la botella se gasta y la ruleta vuelve a estar disponible.
/// </summary>
public class BottleHolder : MonoBehaviour
{
    [Header("Mano")]
    [Tooltip("Modelo de la botella en la mano. Se enciende al recibir una botella y se apaga al gastarla.")]
    [SerializeField] private GameObject bottleModel;
    // Se borran al quitar el respaldo de teclado (T7.1).
    [SerializeField] private Key drinkKey = Key.F;
    [SerializeField] private Key throwKey = Key.Q;

    [Header("Lanzar")]
    [Tooltip("Prefab de la botella que sale volando (con ThrownBottle, Rigidbody y Collider).")]
    [SerializeField] private ThrownBottle thrownBottlePrefab;
    [Tooltip("Cámara del jugador: la botella sale hacia donde miras. Si lo dejas vacío usa Camera.main.")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Velocidad de salida de la botella en m/s (se le suma la tuya). 14 = lanzamiento medio.")]
    [FormerlySerializedAs("throwForce")]
    [Min(0f)]
    [SerializeField] private float throwSpeed = 14f;
    [Tooltip("Grados hacia arriba al lanzar, para que haga parábola. 10 = parábola suave.")]
    [FormerlySerializedAs("throwUpAngle")]
    [Range(0f, 45f)]
    [SerializeField] private float throwUpDegrees = 10f;
    [Tooltip("Velocidad de giro de la botella en el aire, en radianes por segundo. 8 = da vueltas rápido.")]
    [FormerlySerializedAs("throwSpin")]
    [Min(0f)]
    [SerializeField] private float throwSpinRadPerSecond = 8f;

    [Header("Pruebas")]
    [Tooltip("Ticket para probar sin pasar por la ruleta ni el barman: en Play, menú ⋮ del componente → TEST: Dar botella, o tecla T.")]
    [SerializeField] private TicketData testTicket;

    [Header("Depuración")]
    [Tooltip("Muestra la botella de la mano y el efecto activo con sus segundos en la vista Scene (y en Game con el botón Gizmos activado).")]
    [SerializeField] private bool showDebug;

    /// <summary>Al BEBER cualquier botella (lo usa, por ejemplo, la barra de blackout).</summary>
    public event Action<TicketData> OnEffectStarted;
    /// <summary>Cuando se acaba el temporizador del efecto (o lo sustituye otra botella).</summary>
    public event Action<TicketData> OnEffectEnded;

    private TicketData current;
    private LiquidWobble bottleLiquid;
    private Collider playerCollider;
    private PlayerController playerController;
    private PlayerBuffs playerBuffs;

    // Temporizador del último efecto bebido (solo para el HUD).
    private TicketData activeEffect;
    private float effectDuration;
    private float effectEndTime;

    public bool HasBottle => current != null;
    public bool HasActiveEffect => activeEffect != null;
    public TicketData ActiveEffect => activeEffect;
    public float EffectDuration => effectDuration;
    public float EffectTimeRemaining => HasActiveEffect ? Mathf.Max(0f, effectEndTime - Time.time) : 0f;

    private void Awake()
    {
        if (bottleModel != null)
        {
            bottleLiquid = bottleModel.GetComponentInChildren<LiquidWobble>(true);
            bottleModel.SetActive(false);
        }
        else
            Debug.LogWarning("[Botellas] Falta asignar bottleModel: no se podrá coger ninguna botella.", this);

        if (playerCamera == null)
            playerCamera = Camera.main;

        // El CharacterController también es un Collider: la botella lanzada lo ignorará.
        playerCollider = GetComponentInParent<CharacterController>();
        playerController = GetComponentInParent<PlayerController>();
        playerBuffs = GetComponentInParent<PlayerBuffs>();
    }

    public void Give(TicketData ticket)
    {
        current = ticket;

        // El color va al líquido (no al cristal), a través de LiquidWobble.
        if (bottleLiquid != null)
            bottleLiquid.SetLiquidColor(ticket.ticketColor);

        bottleModel.SetActive(true);
    }

    [ContextMenu("TEST: Dar botella")]
    private void TestGive()
    {
        if (testTicket != null) Give(testTicket);
    }

    private void Update()
    {
        UpdateEffectTimer();

        // TEST: con la T te das la botella de prueba (solo si hay testTicket y no llevas ya una).
        if (testTicket != null && !HasBottle && Keyboard.current != null && Keyboard.current[Key.T].wasPressedThisFrame)
            Give(testTicket);

        // shortcut: respaldo con teclado directo hasta conectar Drink/Throw en el Player Input de PF_Player; entonces se borra (T7.1).
        if (!HasBottle || Keyboard.current == null) return;

        if (Keyboard.current[drinkKey].wasPressedThisFrame)
            Drink();
        else if (Keyboard.current[throwKey].wasPressedThisFrame)
            Throw();
    }

    // Se conectan en el Player Input (Invoke Unity Events) a las acciones Drink y Throw.
    public void OnDrink(InputAction.CallbackContext context)
    {
        if (context.performed && HasBottle) Drink();
    }

    public void OnThrow(InputAction.CallbackContext context)
    {
        if (context.performed && HasBottle) Throw();
    }

    private BottleEffect GetEffect(TicketData ticket)
    {
        if (ticket != null && ticket.bottleEffect == null)
            Debug.LogWarning($"[Botellas] El ticket {ticket.displayName} no tiene BottleEffect asignado.", ticket);
        return ticket != null ? ticket.bottleEffect : null;
    }

    // ---------------- BEBER ----------------

    private void Drink()
    {
        TicketData drunk = ReleaseBottle();
        BottleEffect effect = GetEffect(drunk);

        OnEffectStarted?.Invoke(drunk);

        if (playerBuffs != null)
            playerBuffs.ApplyDrinkEffect(effect);

        // El temporizador del HUD solo tiene sentido si el efecto dura un tiempo.
        if (effect != null && effect.HasTimedDrinkEffect)
            StartEffectTimer(drunk, effect.drinkDuration);
    }

    private void StartEffectTimer(TicketData ticket, float duration)
    {
        // Si ya había un efecto, se termina antes de empezar el nuevo (no se acumulan).
        if (HasActiveEffect)
            OnEffectEnded?.Invoke(activeEffect);

        activeEffect = ticket;
        effectDuration = duration;
        effectEndTime = Time.time + duration;
    }

    private void UpdateEffectTimer()
    {
        if (!HasActiveEffect || Time.time < effectEndTime) return;

        TicketData ended = activeEffect;
        activeEffect = null;
        OnEffectEnded?.Invoke(ended);
    }

    // ---------------- LANZAR ----------------

    private void Throw()
    {
        // Guardamos dónde está la botella en la mano ANTES de ocultarla: de ahí sale la lanzada.
        Vector3 spawnPosition = bottleModel.transform.position;
        Quaternion spawnRotation = bottleModel.transform.rotation;

        TicketData thrown = ReleaseBottle();
        BottleEffect effect = GetEffect(thrown);

        if (playerBuffs != null)
            playerBuffs.ApplyThrowSelfEffect(effect);

        if (thrownBottlePrefab == null) return;

        Transform aim = playerCamera != null ? playerCamera.transform : transform;
        Vector3 direction = Quaternion.AngleAxis(-throwUpDegrees, aim.right) * aim.forward;

        Vector3 velocity = direction * throwSpeed;
        if (playerController != null)
            velocity += playerController.Velocity; // si lanzas corriendo, la botella lleva tu inercia

        ThrownBottle bottle = Instantiate(thrownBottlePrefab, spawnPosition, spawnRotation);
        bottle.Launch(velocity, throwSpinRadPerSecond, thrown, effect, playerCollider);
    }

    // ---------------- COMÚN ----------------

    /// <summary>Quita la botella de la mano (con eso la ruleta deja de estar bloqueada). Devuelve el ticket que tenía.</summary>
    private TicketData ReleaseBottle()
    {
        TicketData released = current;
        current = null;
        bottleModel.SetActive(false);
        return released;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string bottle = HasBottle ? $"botella {current.displayName}" : "sin botella";
        string active = HasActiveEffect ? $"efecto {activeEffect.displayName} · quedan {EffectTimeRemaining:0.0} s" : "sin efecto";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.3f, $"Mano · {bottle}\n{active}");
    }
#endif
}