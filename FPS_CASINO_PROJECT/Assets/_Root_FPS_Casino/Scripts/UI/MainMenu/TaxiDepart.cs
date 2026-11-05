using System.Collections;
using UnityEngine;

/// <summary>
/// Salida del taxi del menú: arranca (tiembla y cabecea) y se va acelerando hacia la derecha de la
/// pantalla. La lanza QuitGameTarget al hacer clic en el taxi (evento On Quit Start).
/// Va en el Taxi (el objeto de arriba, el que se mueve entero).
/// </summary>
public class TaxiDepart : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto que se mueve: el Taxi entero. Vacío = este objeto.")]
    [SerializeField] private Transform taxi;
    [Tooltip("Cámara del menú, para saber hacia dónde es la derecha. Vacío = Camera.main.")]
    [SerializeField] private Camera menuCamera;

    [Header("Arranque")]
    [Tooltip("Segundos que tiembla antes de salir.")]
    [Min(0f)]
    [SerializeField] private float startSeconds = 0.8f;
    [Tooltip("Cuánto tiembla, en unidades de la escena. 4 = se nota sin exagerar.")]
    [Min(0f)]
    [SerializeField] private float shakeAmount = 4f;
    [Tooltip("Cuánto cabecea al arrancar, en grados.")]
    [Min(0f)]
    [SerializeField] private float tiltDegrees = 2f;

    [Header("Salida")]
    [Tooltip("Aceleración hacia la derecha, en unidades por segundo al cuadrado. 800 = sale de cámara en menos de 2 segundos.")]
    [Min(0f)]
    [SerializeField] private float acceleration = 800f;
    [Tooltip("Segundos que sigue acelerando (luego ya está fuera de cámara).")]
    [Min(0f)]
    [SerializeField] private float driveSeconds = 2.2f;

    private bool departing;

    public void Depart()
    {
        if (departing) return;
        departing = true;

        if (taxi == null) taxi = transform;
        if (menuCamera == null) menuCamera = Camera.main;

        StartCoroutine(DepartRoutine());
    }

    private IEnumerator DepartRoutine()
    {
        Vector3 startPos = taxi.position;
        Quaternion startRot = taxi.rotation;

        // 1) Arranque: tiembla y cabecea, cada vez menos
        float t = 0f;
        while (t < startSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / startSeconds);

            Vector3 jitter = Random.insideUnitSphere * shakeAmount * (1f - 0.5f * k);
            float tilt = Mathf.Sin(k * Mathf.PI * 3f) * tiltDegrees * (1f - k);

            taxi.position = startPos + jitter;
            taxi.rotation = startRot * Quaternion.Euler(tilt, 0f, 0f);
            yield return null;
        }

        taxi.position = startPos;
        taxi.rotation = startRot;

        // 2) Sale acelerando hacia la derecha de la pantalla
        Vector3 right = menuCamera != null ? menuCamera.transform.right : taxi.right;
        right.y = 0f;
        right.Normalize();

        t = 0f;
        while (t < driveSeconds)
        {
            t += Time.deltaTime;
            float distance = 0.5f * acceleration * t * t;
            Vector3 rumble = Random.insideUnitSphere * shakeAmount * 0.25f;   // el motor sigue vibrando

            taxi.position = startPos + right * distance + rumble;
            yield return null;
        }
    }
}