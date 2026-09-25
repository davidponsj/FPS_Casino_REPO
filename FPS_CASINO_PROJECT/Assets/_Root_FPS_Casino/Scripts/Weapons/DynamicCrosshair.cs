using UnityEngine;

/// <summary>
/// Retícula estilo Counter-Strike: 4 líneas (arriba/abajo/izq/dcha) que se separan del
/// centro según la dispersión actual del arma equipada. Quieto = líneas pegadas al centro
/// (o incluso un punto fijo); moviéndote = se abren proporcionalmente.
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
    [SerializeField] private float smoothing = 18f;

    private float currentGap;

    /// <summary>Llamar a esto al cambiar de arma (cuando montéis el inventario/cambio de arma).</summary>
    public void SetWeapon(WeaponBase newWeapon)
    {
        weapon = newWeapon;
    }

    private void Update()
    {
        if (weapon == null) return;

        float targetGap = Mathf.Lerp(minGap, maxGap, weapon.NormalizedSpread);
        currentGap = Mathf.Lerp(currentGap, targetGap, smoothing * Time.deltaTime);

        if (top != null) top.anchoredPosition = new Vector2(0f, currentGap);
        if (bottom != null) bottom.anchoredPosition = new Vector2(0f, -currentGap);
        if (left != null) left.anchoredPosition = new Vector2(-currentGap, 0f);
        if (right != null) right.anchoredPosition = new Vector2(currentGap, 0f);
    }
}