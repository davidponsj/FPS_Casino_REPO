using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Palanca de la tragaperras: va en la palanca y, cuando su SlotMachine empieza a girar, la baja
/// y la vuelve a subir. Solo es visual.
/// </summary>
public class SlotLeverAnimator : MonoBehaviour
{
    [Tooltip("Tragaperras que mueve esta palanca.")]
    [SerializeField] private SlotMachine machine;

    [Header("Animación")]
    [Tooltip("Eje local sobre el que se inclina la palanca")]
    [SerializeField] private Vector3 pullAxis = Vector3.right;
    [Tooltip("Grados que baja la palanca. Si baja hacia el lado contrario, pon el valor en negativo")]
    [FormerlySerializedAs("pullAngle")]
    [SerializeField] private float pullDegrees = 60f;
    [Tooltip("Segundos que tarda en bajar. 0.25 = tirón rápido.")]
    [FormerlySerializedAs("pullTime")]
    [Min(0.01f)]
    [SerializeField] private float pullSeconds = 0.25f;
    [Tooltip("Segundos que tarda en volver arriba. 0.4 = sube algo más lenta que baja.")]
    [FormerlySerializedAs("returnTime")]
    [Min(0.01f)]
    [SerializeField] private float returnSeconds = 0.4f;

    private Quaternion startRotation;
    private bool wasSpinning;
    private Coroutine routine;

    private void Awake()
    {
        startRotation = transform.localRotation;
    }

    private void Update()
    {
        if (machine == null) return;

        if (machine.IsSpinning && !wasSpinning)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(PullRoutine());
        }

        wasSpinning = machine.IsSpinning;
    }

    private IEnumerator PullRoutine()
    {
        Quaternion pulled = startRotation * Quaternion.AngleAxis(pullDegrees, pullAxis);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pullSeconds;
            transform.localRotation = Quaternion.Slerp(startRotation, pulled, Mathf.Clamp01(t));
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / returnSeconds;
            transform.localRotation = Quaternion.Slerp(pulled, startRotation, Mathf.Clamp01(t));
            yield return null;
        }

        transform.localRotation = startRotation;
    }
}