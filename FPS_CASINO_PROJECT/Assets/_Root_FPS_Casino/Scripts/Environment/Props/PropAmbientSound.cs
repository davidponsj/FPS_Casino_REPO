using UnityEngine;

/// <summary>
/// Sonido ambiente de un prop (el tintineo de una tragaperras, el zumbido de un neón...). Arranca el AudioSource del prop
/// en un punto al azar del clip y con un tono un poco distinto, para que varias copias del mismo prop en la sala
/// NO suenen sincronizadas como un coro de clones.
///
/// Va en la raíz del prefab del prop, junto a su AudioSource (con el clip puesto, Loop marcado y Play On Awake desmarcado:
/// lo arranca este script).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PropAmbientSound : MonoBehaviour
{
    [Header("Variación entre copias")]
    [Tooltip("Arranca el clip en un punto al azar. Así dos copias iguales no suenan a la vez.")]
    [SerializeField] private bool randomStartTime = true;
    [Tooltip("Cuánto puede variar el tono de cada copia. 0.05 = ±5%, se nota que son máquinas distintas sin desafinar.")]
    [Range(0f, 0.3f)]
    [SerializeField] private float pitchVariation = 0.05f;
    [Tooltip("Cuánto puede variar el volumen de cada copia. 0.1 = ±10%.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float volumeVariation = 0.1f;

    [Header("Depuración")]
    [Tooltip("Muestra sobre el prop el clip que suena y su tono.")]
    [SerializeField] private bool showDebug;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false; // lo arranca Start, con su variación
    }

    private void Start()
    {
        if (audioSource.clip == null)
        {
            Debug.LogWarning($"{name}: PropAmbientSound no tiene clip en su AudioSource. El prop no sonará.", this);
            return;
        }

        audioSource.pitch *= 1f + Random.Range(-pitchVariation, pitchVariation);
        audioSource.volume *= 1f + Random.Range(-volumeVariation, volumeVariation);
        if (randomStartTime)
            audioSource.time = Random.Range(0f, audioSource.clip.length);

        audioSource.Play();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || audioSource == null || audioSource.clip == null) return;
        DebugLabels.At(transform.position + Vector3.up * 2f, 0, $"Sonido · {audioSource.clip.name} · tono {audioSource.pitch:0.00}", DebugLabels.Casino);
    }
#endif
}
