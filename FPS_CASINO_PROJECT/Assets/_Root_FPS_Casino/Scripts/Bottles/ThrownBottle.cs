using UnityEngine;

/// <summary>
/// Botella lanzada. Vuela con física (Rigidbody), con el líquido moviéndose dentro gracias a
/// LiquidWobble, y al chocar con cualquier cosa se rompe: suelta las partículas de cristal y
/// líquido (BottleShatterEffect) y deja un charco del color de la botella en el suelo (BottlePuddle).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ThrownBottle : MonoBehaviour
{
    [Header("Rotura")]
    [Tooltip("Velocidad mínima del choque para romperse. 0 = se rompe con cualquier contacto.")]
    [SerializeField] private float minBreakSpeed = 0f;
    [Tooltip("Si no choca con nada en este tiempo (se cae del mapa), se destruye.")]
    [SerializeField] private float maxLifetime = 10f;

    [Header("Efectos")]
    [SerializeField] private BottleShatterEffect shatterEffectPrefab;
    [SerializeField] private BottlePuddle puddlePrefab;
    [Tooltip("Capas que cuentan como suelo para colocar el charco.")]
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Distancia máxima hacia abajo para buscar suelo donde dejar el charco (si rompe contra una pared).")]
    [SerializeField] private float puddleGroundSearch = 4f;

    private Rigidbody rb;
    private TicketData ticket;
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
    public void Launch(Vector3 velocity, float spin, TicketData bottleTicket, Collider ownerCollider)
    {
        ticket = bottleTicket;

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

        Destroy(gameObject, maxLifetime);
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
            BottleShatterEffect effect = Instantiate(shatterEffectPrefab, point, Quaternion.LookRotation(normal));
            effect.Play(LiquidColor);
        }

        SpawnPuddle(point);
        Destroy(gameObject);
    }

    private void SpawnPuddle(Vector3 point)
    {
        if (puddlePrefab == null) return;

        // El charco siempre va al suelo: si la botella rompe contra una pared, el líquido
        // "cae" y el charco aparece justo debajo del punto de impacto.
        Vector3 origin = point + Vector3.up * 0.2f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, puddleGroundSearch, groundMask, QueryTriggerInteraction.Ignore))
            return;

        // Solo en superficies más o menos planas (no en una pared vertical).
        if (hit.normal.y < 0.5f) return;

        // Un pelín por encima del suelo para que no parpadee con él (z-fighting) y alineado con la pendiente.
        Vector3 position = hit.point + hit.normal * 0.01f;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        BottlePuddle puddle = Instantiate(puddlePrefab, position, rotation);
        puddle.Init(LiquidColor, ticket);
    }
}
