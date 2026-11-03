using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vuelve todas las teclas a las de PlayerControls y borra las guardadas. Se llama desde la opción
/// "Reset" del panel de Controles del ATM (On Select → KeyBindingsReset.ResetAll).
/// </summary>
public class KeyBindingsReset : MonoBehaviour
{
    [Tooltip("Las acciones del jugador (PlayerControls).")]
    [SerializeField] private InputActionAsset actions;

    public void ResetAll()
    {
        if (actions == null)
        {
            Debug.LogWarning("[KeyBindingsReset] Falta asignar PlayerControls.", this);
            return;
        }

        KeyBindings.ResetToDefaults(actions);
    }
}