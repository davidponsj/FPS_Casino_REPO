using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Objeto "Salir" del menú: va en un objeto 3D con HoverOutline, y MenuHoverManager llama a Quit al hacer clic.
/// Primero lanza On Quit Start (p. ej. la salida del taxi) y cierra pasados Quit Delay Seconds.
/// En una build cierra el juego; en el editor solo lo avisa en la Console y el Play sigue.
/// </summary>
public class QuitGameTarget : MonoBehaviour
{
    [Tooltip("Segundos desde el clic hasta cerrar el juego. 3 = da tiempo a ver irse el taxi.")]
    [Min(0f)]
    [SerializeField] private float quitDelaySeconds = 3f;
    [Tooltip("Se lanza al hacer clic, antes de cerrar (animación, sonido...).")]
    [SerializeField] private UnityEvent onQuitStart;

    /// <summary>True desde el clic hasta que se cierra: el menú deja de responder.</summary>
    public static bool IsQuitting { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsQuitting = false;

    public void Quit()
    {
        if (IsQuitting) return;
        IsQuitting = true;

        onQuitStart?.Invoke();
        StartCoroutine(QuitAfterDelay());
    }

    private IEnumerator QuitAfterDelay()
    {
        yield return new WaitForSeconds(quitDelaySeconds);

#if UNITY_EDITOR
        Debug.Log("[Salir] Aquí se cerraría el juego (en el editor no se cierra).", this);
#else
        Application.Quit();
#endif
    }
}