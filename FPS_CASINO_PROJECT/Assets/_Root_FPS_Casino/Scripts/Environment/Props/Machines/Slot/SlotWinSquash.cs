using System.Collections;
using UnityEngine;

/// <summary>
/// Squash & stretch de la tragaperras al ganar: se aplasta, se estira y rebota hasta quedarse
/// quieta, tantas veces como diga "repeats". Escucha el evento onWin de SlotMachine, así que no hace
/// falta tocar ningún otro script.
/// </summary>
[RequireComponent(typeof(SlotMachine))]
public class SlotWinSquash : MonoBehaviour
{
    [Tooltip("Objeto que se deforma. Vacío = la propia máquina. Se escala desde su pivote, así que conviene que esté en la base.")]
    [SerializeField] private Transform target;
    [Tooltip("Cuánto se aplasta y se estira. 0.25 = notable, 0.45 = dibujo animado, 0.7 = de goma.")]
    [Range(0f, 0.8f)]
    [SerializeField] private float amount = 0.45f;
    [Tooltip("Cuánto engorda al aplastarse (y adelgaza al estirarse) respecto a lo que cambia de alto. 0.5 = suave, 1 = muy exagerado.")]
    [Range(0f, 1.5f)]
    [SerializeField] private float bulge = 0.8f;
    [Tooltip("Rebotes por segundo. 3 = rebote ágil.")]
    [Min(0.1f)]
    [SerializeField] private float bouncesPerSecond = 3f;
    [Tooltip("Segundos que dura cada squash & stretch hasta quedarse quieta.")]
    [Min(0.05f)]
    [SerializeField] private float duration = 0.8f;
    [Tooltip("Lo rápido que se calman los rebotes. 1 = rebotes fuertes hasta el final, 2 = se apagan antes.")]
    [Range(0.5f, 3f)]
    [SerializeField] private float damping = 1f;
    [Tooltip("Cuántas veces seguidas lo hace. 2 = doble rebote.")]
    [Min(1)]
    [SerializeField] private int repeats = 2;
    [Tooltip("Segundos de pausa entre una vez y la siguiente.")]
    [Min(0f)]
    [SerializeField] private float pauseBetween = 0.05f;

    private SlotMachine machine;
    private Vector3 restScale;
    private Coroutine routine;

    private void Awake()
    {
        machine = GetComponent<SlotMachine>();
        if (target == null) target = transform;
        restScale = target.localScale;
    }

    private void OnEnable() => machine.onWin.AddListener(HandleWin);

    private void OnDisable()
    {
        machine.onWin.RemoveListener(HandleWin);
        if (target != null) target.localScale = restScale;
    }

    private void HandleWin(int points)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(SquashRoutine());
    }

    private IEnumerator SquashRoutine()
    {
        for (int r = 0; r < repeats; r++)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);

                // Onda que se va apagando: primero negativa (aplasta) y luego positiva (estira)
                float fade = Mathf.Pow(1f - k, damping);
                float wave = -Mathf.Sin(k * duration * bouncesPerSecond * 2f * Mathf.PI) * fade;
                float height = 1f + amount * wave;
                float width = Mathf.Max(0.05f, 1f - amount * bulge * wave);   // al estirarse adelgaza y al aplastarse engorda

                target.localScale = new Vector3(restScale.x * width, restScale.y * height, restScale.z * width);
                yield return null;
            }

            target.localScale = restScale;
            if (pauseBetween > 0f && r < repeats - 1)
                yield return new WaitForSeconds(pauseBetween);
        }

        routine = null;
    }
}