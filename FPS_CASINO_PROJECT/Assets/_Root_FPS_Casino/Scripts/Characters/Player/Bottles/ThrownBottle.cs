using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Botella lanzada. Vuela con física (Rigidbody), con el líquido moviéndose dentro gracias a
/// LiquidWobble, y al chocar con cualquier cosa se rompe:
/// - Partículas de cristal y líquido (BottleShatterEffect).
/// - Impacto en área si su BottleEffect lo tiene (daño o muerte instantánea).
/// - Charco del color de la botella en el suelo (BottlePuddle), con los efectos del BottleEffect.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ThrownBottle : MonoBehaviour
{
    // Daño "infinito" para el instakill: cualquier enemigo muere con esto.
    private const float KillDamage = 999999f;

    [Header("Rotura")]
    [Tooltip("Velocidad mínima del choque para romperse, en m/s. 0 = se rompe con cualquier contacto.")]
    [Min(0f)]
    [SerializeField] private float minBreakSpeed = 0f;
    [Tooltip("Segundos de vida si no choca con nada (se cae del mapa); luego se destruye. 10 = de sobra.")]
    [FormerlySerializedAs("maxLifetime")]
    [Min(0.1f)]
    [SerializeField] private float maxLifetimeSeconds = 10f;

    [Header("Efectos")]
    [Tooltip("Partículas de cristal y líquido al romperse. Opcional.")]
    [SerializeField] private BottleShatterEffect shatterEffectPrefab;
    [Tooltip("Charco que deja en el suelo. Opcional.")]
    [SerializeField] private BottlePuddle puddlePrefab;
    [Tooltip("Capas que cuentan como suelo para colocar el charco.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Metros que baja buscando suelo para el charco (si rompe contra una pared). 4 = vale para paredes altas.")]
    [FormerlySerializedAs("puddleGroundSearch")]
    [Min(0f)]
    [SerializeField] private float puddleGroundSearchMeters = 4f;
    [Tooltip("Capas a las que afecta el impacto (enemigos).")]
    [SerializeField] private LayerMask impactMask = ~0;

    private Rigidbody rb;
    private TicketData ticket;
    private BottleEffect effect;
    private bool broken;

    private Color LiquidColor
    {
        get
        {
            Color color = ticket != null ? ticket.ticketColor : Color.white;
            color.a = 1f;
            return color;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Va rápido: con detección continua no atraviesa paredes finas.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    /// <summary>Lo llama BottleHolder justo después de instanciarla.</summary>
    public void Launch(Vector3 velocity, float spin, TicketData bottleTicket, BottleEffect bottleEffect, Collider ownerCollider)
    {
        ticket = bottleTicket;
        effect = bottleEffect;

        // Que no choque con el propio jugador al salir de la mano.
        if (ownerCollider != null)
        {
            foreach (Collider bottleCollider in GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bottleCollider, ownerCollider);
        }

        LiquidWobble liquid = GetComponentInChildren<LiquidWobble>();
        if (liquid != null)
            liquid.SetLiquidColor(LiquidColor);

        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.onUnitSphere * spin; // un giro aleatorio queda más natural que siempre el mismo

        Destroy(gameObject, maxLifetimeSeconds);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (broken) return;
        if (collision.relativeVelocity.magnitude < minBreakSpeed) return;

        ContactPoint contact = collision.GetContact(0);
        Break(contact.point, contact.normal);
    }

    private void Break(Vector3 point, Vector3 normal)
    {
        broken = true;

        // Apagamos sus colliders para que el raycast del charco no choque con la propia botella.
        foreach (Collider bottleCollider in GetComponentsInChildren<Collider>())
            bottleCollider.enabled = false;

        if (shatterEffectPrefab != null)
        {
            BottleShatterEffect shatter = Instantiate(shatterEffectPrefab, point, Quaternion.LookRotation(normal));
            shatter.Play(LiquidColor);
        }

        if (effect != null && effect.HasImpact)
            ApplyImpact(point);

        // Sin efecto (botella sin configurar) dejamos el charco por defecto; con efecto, solo si tiene charco.
        if (effect == null || effect.puddleLifetime > 0f)
            SpawnPuddle(point);

        Destroy(gameObject);
    }

    /// <summary>Daño (o muerte) a los enemigos dentro del radio del impacto. El jugador no se hace daño.</summary>
    private void ApplyImpact(Vector3 point)
    {
        // Un enemigo puede tener varios colliders: con el HashSet cada uno recibe el golpe una sola vez.
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();

        foreach (Collider hit in Physics.OverlapSphere(point, effect.impactRadius, impactMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.GetComponentInParent<PlayerHealth>() != null) continue;

            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !damaged.Add(target)) continue;

            target.TakeDamage(effect.impactKills ? KillDamage : effect.impactDamage);
        }
    }

    private void SpawnPuddle(Vector3 point)
    {
        if (puddlePrefab == null) return;

        // El charco siempre va al suelo: si la botella rompe contra una pared, el líquido
        // "cae" y el charco aparece justo debajo del punto de impacto.
        Vector3 origin = point + Vector3.up * 0.2f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, puddleGroundSearchMeters, groundMask, QueryTriggerInteraction.Ignore))
            return;

        // Solo en superficies más o menos planas (no en una pared vertical).
        if (hit.normal.y < 0.5f) return;

        // Un pelín por encima del suelo para que no parpadee con él (z-fighting) y alineado con la pendiente.
        Vector3 position = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        BottlePuddle puddle = Instantiate(puddlePrefab, position, rotation);
        puddle.Init(LiquidColor, effect);
    }
}
