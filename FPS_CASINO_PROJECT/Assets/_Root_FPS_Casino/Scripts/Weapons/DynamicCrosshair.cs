using UnityEngine;

/// <summary>
/// Retícula estilo Counter-Strike: 4 líneas (arriba/abajo/izq/dcha) que se separan del
/// centro según la dispersión actual del arma equipada. Quieto = líneas pegadas al centro;
/// moviéndote/disparando = se abren proporcionalmente. Sin arma equipada, se oculta por completo.
///
/// No aplica ningún suavizado propio: el arma (WeaponBase) ya suaviza su dispersión internamente,
/// así que la retícula simplemente refleja ese valor tal cual, 1:1, sin añadir más retraso.
///
/// Jerarquía esperada en el Canvas:
/// Crosshair (RectTransform vacío, anclado al centro de la pantalla)
///   - Top (Image)
///   - Bottom (Image)
///   - Left (Image)
///   - Right (Image)
/// Cada línea debe tener su pivote apuntando hacia el centro (Top con pivot Y=0, Bottom pivot Y=1, etc.)
/// para que al mover el anchoredPosition, la línea "crezca hacia afuera" en vez de moverse entera.
/// </summary>
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El arma actualmente equipada. Cuando tengáis cambio de arma, llamad a SetWeapon() al equipar una nueva.")]
    [SerializeField] private WeaponBase weapon;

    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform bottom;
    [SerializeField] private RectTransform left;
    [SerializeField] private RectTransform right;

    [Header("Distancias (píxeles desde el centro)")]
    [SerializeField] private float minGap = 4f;
    [SerializeField] private float maxGap = 32f;

    /// <summary>Llamar a esto al cambiar de arma (o pasar null al quedarte sin arma).</summary>
    public void SetWeapon(WeaponBase newWeapon)
    {
        weapon = newWeapon;
    }

    private void Update()
    {
        if (weapon == null)
        {
            SetLinesActive(false); // sin arma equipada, no hay nada que apuntar
            return;
        }

        SetLinesActive(true);

        float gap = Mathf.Lerp(minGap, maxGap, weapon.NormalizedSpread);

        if (top != null) top.anchoredPosition = new Vector2(0f, gap);
        if (bottom != null) bottom.anchoredPosition = new Vector2(0f, -gap);
        if (left != null) left.anchoredPosition = new Vector2(-gap, 0f);
        if (right != null) right.anchoredPosition = new Vector2(gap, 0f);
    }

    // Ojo: esto activa/desactiva las 4 líneas hijas, NUNCA el propio GameObject del script
    // (si se desactivara a sí mismo, su Update() dejaría de ejecutarse y se quedaría oculto para siempre).
    private void SetLinesActive(bool active)
    {
        if (top != null) top.gameObject.SetActive(active);
        if (bottom != null) bottom.gameObject.SetActive(active);
        if (left != null) left.gameObject.SetActive(active);
        if (right != null) right.gameObject.SetActive(active);
    }
}