using UnityEngine;

/// <summary>
/// Opción "Salir" del menú: se conecta a su evento onSelect en ATMScreen.
/// En una build cierra el juego; en el editor para el Play Mode (Application.Quit no hace nada ahí).
/// </summary>
public class QuitGameTarget : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}