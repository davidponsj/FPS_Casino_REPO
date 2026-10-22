using System.Collections;
using UnityEngine;

public class SlotLeverAnimator : MonoBehaviour
{
    [SerializeField] private SlotMachine machine;

    [Header("Animación")]
    [Tooltip("Eje local sobre el que se inclina la palanca")]
    [SerializeField] private Vector3 pullAxis = Vector3.right;
    [Tooltip("Si baja hacia el lado contrario, pon el valor en negativo")]
    [SerializeField] private float pullAngle = 60f;
    [SerializeField] private float pullTime = 0.25f;
    [SerializeField] private float returnTime = 0.4f;

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
        Quaternion pulled = startRotation * Quaternion.AngleAxis(pullAngle, pullAxis);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pullTime;
            transform.localRotation = Quaternion.Slerp(startRotation, pulled, Mathf.Clamp01(t));
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / returnTime;
            transform.localRotation = Quaternion.Slerp(pulled, startRotation, Mathf.Clamp01(t));
            yield return null;
        }

        transform.localRotation = startRotation;
    }
}