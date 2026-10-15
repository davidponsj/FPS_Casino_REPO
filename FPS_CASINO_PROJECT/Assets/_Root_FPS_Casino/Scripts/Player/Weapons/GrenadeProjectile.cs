using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Granada del lanzagranadas. Es un objeto físico (Rigidbody) que cae con la gravedad y rebota
/// usando el Physics Material de su Collider. Cuenta cuántas veces choca y, al superar
/// maxBounces, explota haciendo daño en área a todo lo IDamageable que haya en el radio.
///
/// - maxBounces = 0: explota al primer contacto.
/// - maxBounces = 3: rebota en 3 superficies y explota al tocar la cuarta.
/// Además explota sí o sí al pasar fuseTime segundos (por si se queda rodando por el suelo)
/// y, si explodeOnDirectHit está activo, al darle directamente a un enemigo.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class GrenadeProjectile : MonoBehaviour
{
    [Header("Rebotes")]
    [Tooltip("Rebotes antes de explotar. 0 = explota al primer contacto. 3 = rebota 3 veces y explota en el 4º contacto.")]
    [Min(0)]
    [SerializeField] private int maxBounces = 0;
    [Tooltip("Si le das directamente a algo que recibe daño (un enemigo), explota aunque le queden rebotes.")]
    [SerializeField] private bool explodeOnDirectHit = true;
    [Tooltip("Segundos máximos antes de explotar sí o sí, aunque no haya gastado los rebotes.")]
    [SerializeField] private float fuseTime = 4f;
    [Tooltip("Tiempo mínimo entre dos choques para contarlos como rebotes distintos (evita contar dos veces el mismo golpe).")]
    [SerializeField] private float minTimeBetweenBounces = 0.05f;

    [Header("Caída")]
    [Tooltip("Multiplica la gravedad de Unity para esta granada. 1 = normal, >1 cae antes (más arcade).")]
    [SerializeField] private float gravityMultiplier = 1.5f;

    [Header("Explosión")]
    [SerializeField] private float explosionRadius = 4f;
    [Tooltip("Daño en el centro de la explosión.")]
    [SerializeField] private float explosionDamage = 90f;
    [Tooltip("Porcentaje del daño que se hace en el borde del radio (0.2 = un 20%). Entre medias baja de forma lineal.")]
    [Range(0f, 1f)]
    [SerializeField] private float edgeDamagePercent = 0.2f;
    [Tooltip("Capas a las que afecta la explosión.")]
    [SerializeField] private LayerMask explosionMask = ~0;
    [Tooltip("Si está activo, las paredes protegen: solo recibe daño lo que 've' el centro de la explosión.")]
    [SerializeField] private bool requireLineOfSight = true;
    [Tooltip("Empuje a los objetos con Rigidbody que pille la explosión (cajas, botellas...).")]
    [SerializeField] private float explosionForce = 500f;
    [SerializeField] private GameObject explosionEffectPrefab;

    private Rigidbody rb;
    private int bouncesDone;
    private float lastBounceTime = -1f;
    private bool hasExploded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Va rápido: con detección continua no atraviesa paredes finas entre un frame de física y otro.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    /// <summary>Lo llama el lanzagranadas justo después de instanciarla.</summary>
    public void Launch(Vector3 velocity, Collider ownerCollider)
    {
        if (ownerCollider != null)
            Physics.IgnoreCollision(GetComponent<Collider>(), ownerCollider);

        rb.linearVelocity = velocity;
        Invoke(nameof(ExplodeAtCurrentPosition), fuseTime);
    }

    private void FixedUpdate()
    {
        // La gravedad normal ya la aplica el Rigidbody; aquí solo sumamos la parte extra.
        if (gravityMultiplier != 1f)
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasExploded) return;

        ContactPoint contact = collision.GetContact(0);
        // Un pelín separado de la superficie, para que el centro de la explosión no quede dentro de la pared.
        Vector3 explosionPoint = contact.point + contact.normal * 0.1f;

        if (explodeOnDirectHit && collision.collider.GetComponentInParent<IDamageable>() != null)
        {
            Explode(explosionPoint);
            return;
        }

        if (Time.time - lastBounceTime < minTimeBetweenBounces) return;
        lastBounceTime = Time.time;

        bouncesDone++;
        if (bouncesDone > maxBounces)
            Explode(explosionPoint);
    }

    private void ExplodeAtCurrentPosition()
    {
        Explode(transform.position);
    }

    private void Explode(Vector3 center)
    {
        if (hasExploded) return;
        hasExploded = true;
        CancelInvoke();

        if (explosionEffectPrefab != null)
            Instantiate(explosionEffectPrefab, center, Quaternion.identity);

        // Un enemigo puede tener varios colliders (cuerpo, cabeza...): con el HashSet
        // cada uno recibe daño una sola vez por explosión.
        HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();

        Collider[] hits = Physics.OverlapSphere(center, explosionRadius, explosionMask, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            if (hit.attachedRigidbody == rb) continue; // la propia granada

            Vector3 closestPoint = hit.bounds.ClosestPoint(center);
            if (requireLineOfSight && !HasLineOfSight(center, closestPoint, hit)) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null && damaged.Add(damageable))
            {
                // Daño completo en el centro y edgeDamagePercent en el borde, bajando en línea recta.
                float distance01 = Mathf.Clamp01(Vector3.Distance(center, closestPoint) / explosionRadius);
                float damage = explosionDamage * Mathf.Lerp(1f, edgeDamagePercent, distance01);
                damageable.TakeDamage(damage);
            }

            Rigidbody body = hit.attachedRigidbody;
            if (body != null && pushed.Add(body))
                body.AddExplosionForce(explosionForce, center, explosionRadius, 0.5f);
        }

        Destroy(gameObject);
    }

    /// <summary>True si no hay ninguna pared entre la explosión y el objetivo.</summary>
    private bool HasLineOfSight(Vector3 from, Vector3 to, Collider target)
    {
        if (!Physics.Linecast(from, to, out RaycastHit blocker, explosionMask, QueryTriggerInteraction.Ignore))
            return true;

        // Si lo primero que encuentra la línea es el propio objetivo (u otra parte del mismo
        // enemigo / del mismo objeto físico), no hay pared en medio.
        Collider first = blocker.collider;
        if (first == target) return true;
        if (first.attachedRigidbody != null && first.attachedRigidbody == target.attachedRigidbody) return true;

        IDamageable targetDamageable = target.GetComponentInParent<IDamageable>();
        return targetDamageable != null && first.GetComponentInParent<IDamageable>() == targetDamageable;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}