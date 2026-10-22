using UnityEngine;

public class QuitGameTarget : MonoBehaviour
{
    public void Quit()
    {
        Debug.Log("Has cerrado el juego");

        // Cuando quieras cerrarlo de verdad, descomenta esta línea:
        // Application.Quit();
    }
}