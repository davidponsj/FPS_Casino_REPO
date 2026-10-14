using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Charco que deja una botella al romperse. Aparece creciendo, dura lo que diga su BottleEffect
/// (puddleLifetime) y desaparece encogiéndose. De momento es un placeholder (un cilindro aplanado)
/// hasta que tengamos el modelo bueno.
///
/// Efectos sobre los enemigos, SOLO mientras lo pisan:
/// - Ralentizar o paralizar (enemySpeedMultiplier), a través de IMovementModifiable.
/// - Daño por segundo (enemyDamagePerSecond), a través de IDamageable.
/// Con holdUntilPuddleEnds la ralentización no se quita al salir, sino cuando el charco desaparece.
/// El jugador nunca se ve afectado por los charcos.
///
/// Necesita un Collider en modo trigger. El Rigidbody cinemático se añade solo si falta: sin él,
/// Unity no avisaría de los enemigos que no tengan Rigidbody propio (p.ej. con NavMeshAgent).
/// </summary>
public class BottlePuddle : MonoBehaviour
{
    [SerializeField] private Renderer puddleRenderer;
    [Tooltip("Duración si la botella no tiene BottleEffect asignado.")]
    [SerializeField] private float defaultLifetime = 8f;
    [Tooltip("Segundos que tarda en extenderse al aparecer.")]
    [SerializeField] private float growTime = 0.3f;
    [Tooltip("Segundos que tarda en encogerse al final de su vida.")]
    [SerializeField] private float shrinkTime = 1f;

    // URP/Lit usa "_BaseColor" como color principal.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Vector3 fullScale;
    private float age;
    private float lifetime;
    private BottleEffect effect;

    // Enemigos dentro del charco y cuántos de sus colliders están tocándolo
    // (un enemigo con varios colliders solo "sale" cuando salen todos).
    private readonly Dictionary<IDamageable, int> enemiesInside = new Dictionary<IDamageable, int>();
    // Enemigos a los que este charco les ha cambiado la velocidad (para devolvérsela después).
    private readonly HashSet<IMovementModifiable> slowedEnemies = new HashSet<IMovementModifiable>();
    private readonly List<IDamageable> damageBuffer = new List<IDamageable>();

    /// <summary>Tipo de botella que creó el charco.</summary>
    public TicketData Ticket { get; private set; }

    private void Awake()
    {
        if (puddleRenderer == null)
            puddleRenderer = GetComponentInChildren<Renderer>();

        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        lifetime = defaultLifetime;
        fullScale = transform.localScale;
        transform.localScale = new Vector3(0f, fullScale.y, 0f);
    }

    public void Init(Color color, TicketData ticket, BottleEffect bottleEffect)
    {
        Ticket = ticket;
        effect = bottleEffect;

        if (effect != null)
        {
            lifetime = effect.puddleLifetime;
            // Un charco muy corto no puede tardar más en encogerse de lo que dura.
            shrinkTime = Mathf.Min(shrinkTime, lifetime * 0.5f);
            growTime = Mathf.Min(growTime, lifetime * 0.5f);
        }

        if (puddleRenderer != null)
        {
            // MaterialPropertyBlock: cada charco su color sin crear una copia del material.
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            puddleRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            puddleRenderer.SetPropertyBlock(block);
        }
    }

    private void Update()
    {
        age += Time.deltaTime;

        // Crece al aparecer, se mantiene y se encoge al final. Solo en X/Z: el charco se
        // extiende y se retira por el suelo, su grosor no cambia.
        float grow = growTime > 0f ? Mathf.Clamp01(age / growTime) : 1f;
        float shrink = shrinkTime > 0f ? Mathf.Clamp01((lifetime - age) / shrinkTime) : 1f;
        float size = Mathf.SmoothStep(0f, 1f, Mathf.Min(grow, shrink));

        transform.localScale = new Vector3(fullScale.x * size, fullScale.y, fullScale.z * size);

        DamageEnemiesInside();

        if (age >= lifetime)
            Destroy(gameObject);
    }

    // ---------------- EFECTOS SOBRE ENEMIGOS ----------------

    private void OnTriggerEnter(Collider other)
    {
        if (effect == null || !effect.PuddleAffectsEnemies) return;
        if (other.GetComponentInParent<PlayerHealth>() != null) return; // el jugador no

        IDamageable enemy = other.GetComponentInParent<IDamageable>();
        if (enemy == null) return;

        enemiesInside.TryGetValue(enemy, out int count);
        enemiesInside[enemy] = count + 1;

        if (count == 0 && effect.enemySpeedMultiplier < 1f)
        {
            IMovementModifiable movement = other.GetComponentInParent<IMovementModifiable>();
            if (movement != null)
            {
                movement.SetSpeedMultiplier(this, effect.enemySpeedMultiplier);
                slowedEnemies.Add(movement);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        IDamageable enemy = other.GetComponentInParent<IDamageable>();
        if (enemy == null || !enemiesInside.TryGetValue(enemy, out int count)) return;

        if (count > 1)
        {
            enemiesInside[enemy] = count - 1;
            return;
        }

        enemiesInside.Remove(enemy);

        // Ya no lo pisa: se le quita la ralentización (salvo que deba durar hasta que acabe el charco).
        if (!effect.holdUntilPuddleEnds)
        {
            IMovementModifiable movement = other.GetComponentInParent<IMovementModifiable>();
            if (movement != null && slowedEnemies.Remove(movement))
                movement.ClearSpeedMultiplier(this);
        }
    }

    private void DamageEnemiesInside()
    {
        if (effect == null || effect.enemyDamagePerSecond <= 0f || enemiesInside.Count == 0) return;

        // Copiamos la lista: hacer daño puede matar al enemigo y no queremos modificar el
        // diccionario mientras lo recorremos.
        damageBuffer.Clear();
        damageBuffer.AddRange(enemiesInside.Keys);

        foreach (IDamageable enemy in damageBuffer)
        {
            // Si el enemigo ya ha muerto y se ha destruido, lo quitamos de la lista.
            if (enemy is Object unityObject && unityObject == null)
            {
                enemiesInside.Remove(enemy);
                continue;
            }

            enemy.TakeDamage(effect.enemyDamagePerSecond * Time.deltaTime);
        }
    }

    private void OnDestroy()
    {
        // Al desaparecer el charco, todos los enemigos recuperan su velocidad.
        foreach (IMovementModifiable movement in slowedEnemies)
        {
            if (movement is Object unityObject && unityObject == null) continue;
            movement.ClearSpeedMultiplier(this);
        }
        slowedEnemies.Clear();
    }
}
