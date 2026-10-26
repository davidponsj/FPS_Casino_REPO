using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Resuelve el contacto físico de la horda en un solo sitio, una vez por frame, justo después de que
/// todos los NavMeshAgent se hayan movido (LateUpdate). Hace el papel del "Crowd Manager" de Unreal/Detour
/// con la resolución de colisiones activada. Va en el JUGADOR (necesita su CharacterController).
///
/// Reglas:
///  1. Enemigo ↔ jugador (body block): son sólidos y el jugador no los atraviesa, pero un enemigo
///     NUNCA empuja al jugador. Si un enemigo acaba dentro del radio de contacto del jugador,
///     se le saca en ese mismo frame (restricción dura).
///  2. Jugador → enemigo: si el jugador camina contra un enemigo, lo aparta con resistencia
///     (patrón OnControllerColliderHit de la documentación de Unity). Los que están pegados
///     atacando pesan más y ceden menos.
///  3. Enemigo ↔ enemigo: separación blanda, repartida según el peso de cada uno.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class HordeCollision : MonoBehaviour
{
    [Header("Enemigo ↔ enemigo (colisión blanda)")]
    [Tooltip("Metros de hueco extra entre dos enemigos, además de sus radios. 0.1 = casi rozándose.")]
    [SerializeField, Min(0f)] private float enemyGap = 0.1f;

    [Tooltip("Velocidad máxima a la que se corrige un solape entre enemigos (m/s). Bajo = blando, alto = rígido. 3 = blando.")]
    [SerializeField, Min(0f)] private float separationSpeed = 3f;

    [Header("Enemigo ↔ jugador (bloquean, nunca empujan)")]
    [Tooltip("Metros de margen extra entre el jugador y un enemigo, además de los radios y el Skin Width del CharacterController.")]
    [SerializeField, Min(0f)] private float playerContactMargin = 0.02f;

    [Header("Jugador → enemigo (abrirse paso)")]
    [Tooltip("Si está marcado, el jugador aparta a los enemigos al caminar contra ellos.")]
    [SerializeField] private bool playerCanShove = true;

    [Tooltip("Velocidad en m/s a la que el jugador aparta a un enemigo libre (se divide por su peso). Menor que la del jugador = se nota la resistencia. 2.5 = cuesta un poco.")]
    [SerializeField, Min(0f)] private float shoveSpeed = 2.5f;

    [Header("Otros")]
    [Tooltip("Metros de diferencia de altura a partir de los que dos cuerpos no se afectan (otro piso). 2 = un piso.")]
    [SerializeField, Min(0f)] private float maxHeightDifference = 2f;

    [Header("Depuración")]
    [Tooltip("Muestra el radio de contacto del jugador (ningún enemigo entra) y el radio de cada enemigo, en la vista Scene (y en Game con el botón Gizmos activado).")]
    [FormerlySerializedAs("drawGizmos")]
    [SerializeField] private bool showDebug;

    // Registro estático: los enemigos se apuntan al activarse, exista ya el solver o no.
    private static readonly List<EnemyMovement> agents = new List<EnemyMovement>();
    private static readonly Dictionary<Collider, EnemyMovement> colliderOwners = new Dictionary<Collider, EnemyMovement>();

    public static HordeCollision Instance { get; private set; }

    private CharacterController controller;

    private Vector3[] positions = new Vector3[64];
    private Vector3[] corrections = new Vector3[64];
    private float[] radii = new float[64];
    private float[] weights = new float[64];

    public float PlayerRadius =>
        controller.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

    /// <summary>Distancia mínima entre el centro del jugador y el de un enemigo.</summary>
    public float ContactDistance(float enemyRadius) =>
        PlayerRadius + enemyRadius + controller.skinWidth + playerContactMargin;

    // ---------------------------------------------------------------
    //  Registro
    // ---------------------------------------------------------------

    public static void Register(EnemyMovement enemy)
    {
        if (agents.Contains(enemy)) return;

        agents.Add(enemy);
        foreach (Collider c in enemy.Colliders)
            if (c != null) colliderOwners[c] = enemy;
    }

    public static void Unregister(EnemyMovement enemy)
    {
        agents.Remove(enemy);
        foreach (Collider c in enemy.Colliders)
            if (c != null) colliderOwners.Remove(c);
    }

    // ---------------------------------------------------------------
    //  Ciclo de vida
    // ---------------------------------------------------------------

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Unity lo llama cuando el CharacterController choca con algo durante su Move.
    /// Si es un enemigo, le pedimos que se aparte en la dirección en la que camina el jugador.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!playerCanShove) return;
        if (!colliderOwners.TryGetValue(hit.collider, out EnemyMovement enemy)) return;

        // El jugador está encima del enemigo: no se empuja hacia los lados.
        if (hit.normal.y > 0.5f) return;

        Vector3 direction = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (direction.sqrMagnitude < 0.01f) return;

        enemy.AddShove(direction.normalized * (shoveSpeed / enemy.Weight));
    }

    private void LateUpdate()
    {
        int count = agents.Count;
        if (count == 0) return;

        EnsureCapacity(count);
        float dt = Time.deltaTime;

        for (int i = 0; i < count; i++)
        {
            EnemyMovement agent = agents[i];
            positions[i] = agent.transform.position;
            radii[i] = agent.Radius;
            weights[i] = agent.Weight;
            corrections[i] = agent.ConsumeShove() * dt;   // empujón del jugador
        }

        SolveEnemyPairs(count, dt);   // blando
        SolvePlayerContact(count);    // duro: va el último para que nada lo deshaga

        for (int i = 0; i < count; i++)
        {
            if (corrections[i].sqrMagnitude > 1e-8f)
                agents[i].ApplyCorrection(corrections[i]);
        }
    }

    // ---------------------------------------------------------------
    //  Resolución
    // ---------------------------------------------------------------

    /// <summary>
    /// Cada par de enemigos que se solapa se separa un poco por frame (blando),
    /// y la corrección se reparte según el peso: el más pesado se mueve menos.
    /// 30 enemigos = 435 pares por frame, trivial. Para cientos haría falta una rejilla espacial.
    /// </summary>
    private void SolveEnemyPairs(int count, float dt)
    {
        float maxStep = separationSpeed * dt;

        for (int i = 0; i < count - 1; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                Vector3 offset = positions[i] - positions[j];
                if (Mathf.Abs(offset.y) > maxHeightDifference) continue;
                offset.y = 0f;

                float minDistance = radii[i] + radii[j] + enemyGap;
                float sqrDistance = offset.sqrMagnitude;
                if (sqrDistance >= minDistance * minDistance) continue;

                float distance = Mathf.Sqrt(sqrDistance);
                Vector3 direction = distance > 0.0001f ? offset / distance : TieBreakDirection(i, j);

                float step = Mathf.Min(minDistance - distance, maxStep);
                float totalWeight = weights[i] + weights[j];

                corrections[i] += direction * (step * weights[j] / totalWeight);
                corrections[j] -= direction * (step * weights[i] / totalWeight);
            }
        }
    }

    /// <summary>
    /// Restricción dura: ningún enemigo termina el frame dentro del radio de contacto del jugador.
    /// Por eso el CharacterController nunca tiene que "desincrustarse" y nunca sale empujado.
    /// </summary>
    private void SolvePlayerContact(int count)
    {
        Vector3 playerPosition = transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector3 next = positions[i] + corrections[i];
            Vector3 offset = next - playerPosition;
            if (Mathf.Abs(offset.y) > maxHeightDifference) continue;
            offset.y = 0f;

            float contact = ContactDistance(radii[i]);
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance >= contact * contact) continue;

            float distance = Mathf.Sqrt(sqrDistance);
            Vector3 direction;

            if (distance > 0.0001f)
            {
                direction = offset / distance;
            }
            else
            {
                direction = -agents[i].transform.forward;
                direction.y = 0f;
                direction.Normalize();
            }

            corrections[i] += direction * (contact - distance);
        }
    }

    private static Vector3 TieBreakDirection(int i, int j)
    {
        // Dos enemigos exactamente en el mismo punto: dirección fija pero distinta para cada par.
        float angle = (i * 92821 + j * 68917) % 360;
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    private void EnsureCapacity(int count)
    {
        if (positions.Length >= count) return;

        int size = Mathf.NextPowerOfTwo(count);
        positions = new Vector3[size];
        corrections = new Vector3[size];
        radii = new float[size];
        weights = new float[size];
    }

    // ---------------------------------------------------------------
    //  Depuración
    // ---------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || controller == null) return;

        // Radio de contacto con un enemigo de radio 0.5: ningún centro de enemigo entra aquí.
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, ContactDistance(0.5f));

        // Radio de separación de cada enemigo.
        UnityEditor.Handles.color = new Color(1f, 1f, 1f, 0.5f);
        foreach (EnemyMovement enemy in agents)
            if (enemy != null) UnityEditor.Handles.DrawWireDisc(enemy.transform.position, Vector3.up, enemy.Radius + enemyGap * 0.5f);
    }
#endif
}