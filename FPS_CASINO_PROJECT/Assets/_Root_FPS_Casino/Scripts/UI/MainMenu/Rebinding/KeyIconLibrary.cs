using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Iconos de las teclas para el panel de Controles. Saca cada tecla de la imagen de Kenney
/// (T_InputKeys): letras, números, F1-F12, flechas, Alt, Tab, ratón... Si arrastras sprites a
/// "Icons", esos tienen prioridad (se buscan por nombre: SPR_KeyW, Key_Shift...). Las teclas que
/// no están en la imagen usan la tecla vacía con su nombre escrito encima.
/// </summary>
[CreateAssetMenu(fileName = "SO_KeyIcons", menuName = "Casino/Key Icons")]
public class KeyIconLibrary : ScriptableObject
{
    [Tooltip("Imagen con todas las teclas de Kenney (T_InputKeys).")]
    [SerializeField] private Texture2D keyAtlas;
    [Tooltip("Píxeles de cada casilla de la imagen. 16 en el pack Pixel 16x.")]
    [Min(1)]
    [SerializeField] private int tileSize = 16;
    [Tooltip("Iconos sueltos opcionales. Tienen prioridad sobre la imagen; se buscan por nombre (SPR_KeyW, Key_Shift...).")]
    [SerializeField] private Sprite[] icons;
    [Tooltip("Tecla vacía para las teclas sin icono. Vacío = la tecla vacía de la imagen.")]
    [SerializeField] private Sprite blankKey;

    // Dónde está cada tecla en la imagen: columna, fila (desde arriba) y casillas de ancho y alto
    private static readonly Dictionary<string, RectInt> Atlas = BuildAtlas();
    private static readonly RectInt BlankTile = new RectInt(29, 6, 1, 1);

    // Nombre del control en el Input System -> nombre del icono suelto
    private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
    {
        { "leftshift", "shift" }, { "rightshift", "shift" },
        { "leftctrl", "ctrl" }, { "rightctrl", "ctrl" },
        { "leftalt", "alt" }, { "rightalt", "alt" },
        { "leftbutton", "lclick" }, { "rightbutton", "rclick" }, { "middlebutton", "mclick" },
        { "escape", "esc" },
    };

    private readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private Sprite atlasBlank;

    public Sprite BlankKey
    {
        get
        {
            if (blankKey != null) return blankKey;
            if (atlasBlank == null && keyAtlas != null) atlasBlank = CreateSprite(BlankTile);
            return atlasBlank;
        }
    }

    /// <summary>Icono para un control ("w", "space", "leftShift", "leftButton"...). Null si no hay.</summary>
    public Sprite Find(string controlName)
    {
        if (string.IsNullOrEmpty(controlName)) return null;
        string key = Normalize(controlName);

        // 1) Iconos sueltos por nombre
        if (icons != null)
        {
            string iconKey = Aliases.TryGetValue(key, out string alias) ? alias : key;
            foreach (var icon in icons)
            {
                if (icon != null && IconKey(icon.name) == iconKey)
                    return icon;
            }
        }

        // 2) La imagen de Kenney
        if (keyAtlas == null || !Atlas.TryGetValue(key, out RectInt tile)) return null;

        if (!cache.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            sprite = CreateSprite(tile);
            cache[key] = sprite;
        }
        return sprite;
    }

    private Sprite CreateSprite(RectInt tile)
    {
        var rect = new Rect(
            tile.x * tileSize,
            keyAtlas.height - (tile.y + tile.height) * tileSize,
            tile.width * tileSize,
            tile.height * tileSize);

        var sprite = Sprite.Create(keyAtlas, rect, new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "Key_Atlas";
        return sprite;
    }

    // "SPR_KeyW" -> "w", "Key_Shift" -> "shift"
    private static string IconKey(string spriteName)
    {
        string n = Normalize(spriteName);
        if (n.StartsWith("spr")) n = n.Substring(3);
        if (n.StartsWith("key")) n = n.Substring(3);
        return n;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static Dictionary<string, RectInt> BuildAtlas()
    {
        var map = new Dictionary<string, RectInt>();
        void Add(string name, int col, int row, int w = 1, int h = 1) => map[name] = new RectInt(col, row, w, h);
        void Row(string keys, int firstCol, int row)
        {
            for (int i = 0; i < keys.Length; i++)
                Add(keys[i].ToString(), firstCol + i, row);
        }

        // Fila 0: Esc, F1-F12, º
        Add("escape", 17, 0);
        for (int i = 1; i <= 12; i++) Add("f" + i, 17 + i, 0);
        Add("backquote", 30, 0);

        // Fila 1: números, símbolos y retroceso
        Row("123456789", 17, 1);
        Add("0", 26, 1);
        for (int i = 0; i <= 9; i++) Add("numpad" + i, i == 0 ? 26 : 16 + i, 1);
        Add("minus", 27, 1);
        Add("numpadminus", 27, 1);
        Add("numpadplus", 28, 1);
        Add("equals", 29, 1);
        Add("numpadequals", 29, 1);
        Add("backspace", 32, 1, 2);

        // Fila 2: QWERTY, corchetes, barra invertida e Intro
        Row("qwertyuiop", 17, 2);
        Add("leftbracket", 27, 2);
        Add("rightbracket", 28, 2);
        Add("backslash", 31, 2);
        Add("enter", 32, 2, 2, 2);
        Add("numpadenter", 32, 2, 2, 2);

        // Fila 3: ASDF...
        Row("asdfghjkl", 18, 3);
        Add("quote", 27, 3);
        Add("semicolon", 30, 3);
        Add("numpadmultiply", 31, 3);

        // Fila 4: ZXCV..., Windows, barra y flechas
        Add("leftmeta", 18, 4);
        Add("rightmeta", 18, 4);
        Row("zxcvbnm", 19, 4);
        Add("slash", 29, 4);
        Add("numpaddivide", 29, 4);
        Add("uparrow", 30, 4);
        Add("rightarrow", 31, 4);
        Add("downarrow", 32, 4);
        Add("leftarrow", 33, 4);

        // Fila 5: Alt, Tab, Supr, Fin, Bloq Num, punto
        Add("leftalt", 17, 5, 2);
        Add("rightalt", 17, 5, 2);
        Add("altgr", 17, 5, 2);
        Add("tab", 19, 5, 2);
        Add("delete", 21, 5, 2);
        Add("end", 23, 5, 2);
        Add("numlock", 25, 5, 2);
        Add("period", 27, 5);
        Add("numpadperiod", 27, 5);

        // Fila 6: Ctrl, Bloq Mayús, Inicio, RePág, AvPág, coma, espacio
        Add("leftctrl", 17, 6, 2);
        Add("rightctrl", 17, 6, 2);
        Add("capslock", 19, 6, 2);
        Add("home", 21, 6, 2);
        Add("pageup", 23, 6, 2);
        Add("pagedown", 25, 6, 2);
        Add("comma", 27, 6);
        Add("space", 31, 6, 3);

        // Fila 7: Shift, Insert, Impr Pant, Bloq Despl, Pausa
        Add("leftshift", 17, 7, 2);
        Add("rightshift", 17, 7, 2);
        Add("insert", 19, 7, 2);
        Add("printscreen", 21, 7, 2);
        Add("scrolllock", 23, 7, 2);
        Add("pause", 25, 7, 2);

        // Ratón
        Add("leftbutton", 9, 2);
        Add("rightbutton", 10, 2);
        Add("middlebutton", 11, 2);

        return map;
    }
}