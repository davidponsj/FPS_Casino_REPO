using System.Collections;
using UnityEngine;

/// <summary>
/// Efectos al morir el enemigo: una explosión (fichas de casino, chispas, humo...) en el centro de su cuerpo y,
/// opcionalmente, una micro‑pausa del juego (hit stop) que hace que cada baja "pese".
///
/// Va en la raíz del enemigo, junto a EnemyHealth, y escucha su OnDied: vale para cualquier muerte (balas, granadas, charcos).
/// EnemyHealth avisa ANTES de apagar los demás scripts del cuerpo, así que la explosión y la pausa se lanzan a tiempo.
/// La pausa es compartida: si mueren varios a la vez (escopetazo), no se encadenan varias pausas, solo se alarga un poco.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyDeathFX : MonoBehaviour
{
    [Header("Explosión")]
    [Tooltip("Prefab de la explosión (PF_RobotDeathExplosion). Se borra solo. Vacío = sin explosión.")]
    [SerializeField] private GameObject explosionPrefab;
    [Tooltip("Renderer del cuerpo, para saber dónde está su centro. Vacío = el primero que encuentre en los hijos.")]
    [SerializeField] private Renderer bodyRenderer;
    [Tooltip("Metros por encima del centro del cuerpo donde sale la explosión. 0 = justo en el centro.")]
    [SerializeField] private float heightOffset = 0f;

    [Header("Micro‑pausa al matar (hit stop)")]
    [Tooltip("Segundos reales que se frena el juego al matar. 0.04 = casi no se ve pero se siente. 0 = sin pausa.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float hitStopSeconds = 0.04f;
    [Tooltip("Velocidad del juego durante la pausa. 0 = congelado; 0.05 = cámara superlenta.")]
    [Range(0f, 1f)]
    [SerializeField] private float hitStopTimeScale = 0.05f;
    [Tooltip("Lo máximo que puede durar la pausa aunque mueran muchos a la vez. 0.1 = nunca se nota como un tirón.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float maxHitStopSeconds = 0.1f;

    [Header("Depuración")]
    [Tooltip("Muestra sobre el enemigo si tiene explosión asignada y si su muerte ha lanzado la pausa.")]
    [SerializeField] private bool showDebug;

    // Compartido por todos los enemigos: una sola pausa a la vez.
    private static bool hitStopActive;
    private static float hitStopStartTime;
    private static float hitStopEndTime;
    private static float timeScaleBeforeHitStop = 1f;
    private bool runsHitStop; // este enemigo es el que está llevando la pausa (y la deshace)

    private EnemyHealth health;

    // Con "Enter Play Mode" sin recargar el dominio, lo estático sobrevive entre partidas: se limpia al empezar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hitStopActive = false;
        timeScaleBeforeHitStop = 1f;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
    }

    private void OnEnable() => health.OnDied += HandleDied;
    private void OnDisable() => health.OnDied -= HandleDied;

    private void OnDestroy()
    {
        // Si el enemigo desaparece en mitad de la pausa (cambio de escena), que el juego no se quede frenado.
        if (runsHitStop)
        {
            Time.timeScale = timeScaleBeforeHitStop;
            hitStopActive = false;
        }
    }

    private void HandleDied(EnemyHealth deadEnemy)
    {
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, GetBodyCenter(), Quaternion.identity);

        if (hitStopSeconds > 0f)
            StartHitStop();
    }

    /// <summary>Centro del cuerpo según su renderer (los colliders ya se apagan al morir). Sin renderer, la posición del enemigo.</summary>
    private Vector3 GetBodyCenter()
    {
        Vector3 center = bodyRenderer != null ? bodyRenderer.bounds.center : transform.position;
        return center + Vector3.up * heightOffset;
    }

    // ---------------- MICRO‑PAUSA ----------------

    private void StartHitStop()
    {
        float now = Time.unscaledTime;

        if (hitStopActive)
        {
            // Ya hay una pausa: se alarga, pero sin pasar del máximo desde que empezó.
            hitStopEndTime = Mathf.Min(Mathf.Max(hitStopEndTime, now + hitStopSeconds), hitStopStartTime + maxHitStopSeconds);
            return;
        }

        hitStopActive = true;
        hitStopStartTime = now;
        hitStopEndTime = now + hitStopSeconds;
        timeScaleBeforeHitStop = Time.timeScale;
        Time.timeScale = hitStopTimeScale;
        runsHitStop = true;

        // La corrutina sigue aunque EnemyHealth apague este script justo después (solo se para si se destruye el objeto).
        StartCoroutine(EndHitStopWhenDone());
    }

    private IEnumerator EndHitStopWhenDone()
    {
        while (Time.unscaledTime < hitStopEndTime)
            yield return null;

        Time.timeScale = timeScaleBeforeHitStop;
        hitStopActive = false;
        runsHitStop = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string explosion = explosionPrefab != null ? explosionPrefab.name : "sin explosión";
        string pause = runsHitStop ? " · pausa en curso" : "";
        DebugLabels.Above(transform, DebugLabels.RobotLightsLine + 1, $"Muerte · {explosion}{pause}", DebugLabels.Health);
    }
#endif
}
