using UnityEngine;

/// <summary>
/// Objeto "Salir" del menú: va en un objeto 3D con HoverOutline, y MenuHoverManager llama a Quit al hacer clic.
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