using UnityEngine;

/// <summary>
/// Textos de depuración ordenados, como el "gameplay debugger" de un motor profesional:
/// - Encima de cada objeto, un solo bloque: cada script escribe en SU línea (las de abajo), separadas en
///   píxeles de pantalla, así que no se pisan aunque alejes la cámara.
/// - Un color por sistema (IA, movimiento, vida...) y fondo oscuro para que se lea sobre cualquier cosa.
/// - Lo que no es de ningún objeto (ronda, horda) va en un panel fijo arriba a la derecha de la vista Game.
///
/// Se llama desde OnDrawGizmos (Above / At) o desde OnGUI (Panel), siempre dentro de #if UNITY_EDITOR.
/// Los textos de encima de los objetos se ven en la vista Scene, y en la Game con el botón Gizmos activado.
/// </summary>
public static class DebugLabels
{
    // Colores por sistema.
    public static readonly Color AI = new Color(1f, 0.85f, 0.25f);
    public static readonly Color Movement = new Color(0.35f, 0.9f, 1f);
    public static readonly Color Health = new Color(1f, 0.45f, 0.45f);
    public static readonly Color Player = new Color(0.55f, 1f, 0.55f);
    public static readonly Color Weapon = new Color(1f, 0.65f, 0.25f);
    public static readonly Color Casino = new Color(1f, 0.6f, 1f);
    public static readonly Color Interaction = Color.white;

    // Línea de cada script en el bloque de su objeto (0 = la de arriba).
    // Los textos de varias líneas van al final, para que no empujen a los demás.
    public const int EnemyAILine = 0;
    public const int EnemyMovementLine = 1;
    public const int EnemyHealthLine = 2;

    public const int PlayerControllerLine = 0;
    public const int PlayerHealthLine = 1;
    public const int PlayerPointsLine = 2;
    public const int PlayerBlackoutLine = 3;
    public const int TicketInventoryLine = 4;
    public const int PlayerBuffsLine = 5;

    // Línea de cada bloque en el panel de la derecha (la ronda ocupa 3).
    public const int RoundPanelLine = 0;
    public const int HordePanelLine = 4;

    private const float HeadHeight = 2.3f; // metros sobre el pivote donde empieza el bloque de un personaje
    private const float LineHeight = 17f;  // píxeles por línea
    private const float Margin = 10f;      // píxeles del panel al borde de la pantalla

#if UNITY_EDITOR
    private static GUIStyle style;

    /// <summary>Línea en el bloque que hay encima de un personaje (enemigo o jugador).</summary>
    public static void Above(Transform owner, int line, string text, Color color) =>
        At(owner.position + Vector3.up * HeadHeight, line, text, color);

    /// <summary>Texto centrado sobre un punto del mundo, bajado "line" líneas.</summary>
    public static void At(Vector3 anchor, int line, string text, Color color)
    {
        Vector3 screen = UnityEditor.HandleUtility.WorldToGUIPointWithDepth(anchor);
        if (screen.z < 0f) return; // detrás de la cámara

        GUIStyle s = Style(color);
        var content = new GUIContent(text);
        Vector2 size = s.CalcSize(content);

        UnityEditor.Handles.BeginGUI();
        GUI.Label(new Rect(screen.x - size.x * 0.5f, screen.y + line * LineHeight, size.x, size.y), content, s);
        UnityEditor.Handles.EndGUI();
    }

    /// <summary>Bloque en el panel fijo de arriba a la derecha de la vista Game. Llamar desde OnGUI.</summary>
    public static void Panel(int line, string text, Color color)
    {
        GUIStyle s = Style(color);
        var content = new GUIContent(text);
        Vector2 size = s.CalcSize(content);
        GUI.Label(new Rect(Screen.width - size.x - Margin, Margin + line * LineHeight, size.x, size.y), content, s);
    }

    private static GUIStyle Style(Color color)
    {
        if (style == null || style.normal.background == null)
        {
            var background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            background.Apply();

            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(4, 4, 1, 1),
                wordWrap = false,
            };
            style.normal.background = background;
        }

        style.normal.textColor = color;
        return style;
    }
#endif
}
