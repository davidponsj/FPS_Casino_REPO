using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private GameObject bottleModel;
    [SerializeField] private Key drinkKey = Key.F;
    [SerializeField] private Key throwKey = Key.Q;

    [Header("Lanzar")]
    [Tooltip("Prefab de la botella que sale volando (con ThrownBottle, Rigidbody y Collider).")]
    [SerializeField] private ThrownBottle thrownBottlePrefab;
    [Tooltip("Cámara del jugador: la botella sale hacia donde miras. Si lo dejas vacío usa Camera.main.")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float throwForce = 14f;
    [Tooltip("Grados hacia arriba al lanzar, para que haga parábola.")]
    [SerializeField] private float throwUpAngle = 10f;
    [Tooltip("Velocidad de giro de la botella en el aire (rad/s).")]
    [SerializeField] private float throwSpin = 8f;

    [Header("Pruebas")]
    [Tooltip("Ticket para probar sin pasar por la ruleta ni el barman: en Play, menú ⋮ del componente → TEST: Dar botella, o tecla T.")]
    [SerializeField] private TicketData testTicket;

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
        Debug.Log($"[Botella] Recibes botella {ticket.displayName}. {drinkKey} para beber, {throwKey} para lanzar.");
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

        if (!HasBottle || Keyboard.current == null) return;

        if (Keyboard.current[drinkKey].wasPressedThisFrame)
            Drink();
        else if (Keyboard.current[throwKey].wasPressedThisFrame)
            Throw();
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
        Debug.Log($"[Botella] Has bebido botella {drunk.displayName}");

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
        Debug.Log($"[Botella] Efecto de {ended.displayName} terminado");
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
        Vector3 direction = Quaternion.AngleAxis(-throwUpAngle, aim.right) * aim.forward;

        Vector3 velocity = direction * throwForce;
        if (playerController != null)
            velocity += playerController.Velocity; // si lanzas corriendo, la botella lleva tu inercia

        ThrownBottle bottle = Instantiate(thrownBottlePrefab, spawnPosition, spawnRotation);
        bottle.Launch(velocity, throwSpin, thrown, effect, playerCollider);
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
}