using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

// Herramienta de editor: monta el panel de audio del menú de pausa con un clic.
// Solo crea objetos DENTRO de PauseMenu → PNL_Pause, copiando el estilo de lo que ya hay (botones, títulos):
// - BTN_Audio en GRP_Buttons (entre Controls y Main Menu).
// - GRP_Audio junto a GRP_Controls, con título AUDIO, filas MUSIC y SFX (slider + porcentaje) y botón RETURN.
// - Lo conecta todo al componente PauseMenu. Se puede deshacer con Ctrl + Z.
public static class PauseAudioPanelBuilder
{
    private static readonly Color SliderBack = new Color(0.25f, 0.18f, 0.12f);
    private static readonly Color SliderFill = new Color(0.949f, 0.71f, 0.227f);   // dorado de los botones (F2B53A)
    private static readonly Color HandleColor = new Color(1f, 0.945f, 0.839f);    // crema de la ventana (FFF1D6)

    [MenuItem("Tools/Pausa/Crear panel de audio")]
    private static void Build()
    {
        var menu = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogWarning("[Pausa] No hay ningún PauseMenu en la escena abierta.");
            return;
        }

        var so = new SerializedObject(menu);
        var audioViewProp = so.FindProperty("audioView");
        if (audioViewProp == null)
        {
            Debug.LogWarning("[Pausa] Actualiza antes PauseMenu.cs (le faltan los campos de audio).");
            return;
        }
        if (audioViewProp.objectReferenceValue != null)
        {
            Debug.LogWarning("[Pausa] El panel de audio ya existe (Audio View está asignado). No se crea otra vez.");
            return;
        }

        var controlsButton = so.FindProperty("controlsButton").objectReferenceValue as Button;
        var controlsView = so.FindProperty("controlsView").objectReferenceValue as GameObject;
        var returnButton = so.FindProperty("returnButton").objectReferenceValue as Button;
        var resetButton = so.FindProperty("resetButton").objectReferenceValue as Button;
        if (controlsButton == null || controlsView == null || returnButton == null)
        {
            Debug.LogWarning("[Pausa] Faltan referencias en PauseMenu (Controls Button, Controls View o Return Button).");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Crear panel de audio");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) Botón AUDIO: copia de CONTROLS, justo debajo, y el grupo de botones crece para que quepan 4
        var buttonsGroup = (RectTransform)controlsButton.transform.parent;
        Undo.RecordObject(buttonsGroup, "Crear panel de audio");
        float buttonHeight = ((RectTransform)controlsButton.transform).rect.height;
        var layout = buttonsGroup.GetComponent<VerticalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 30f;
        // Crece media fila hacia abajo: los 4 botones quedan un poco más bajitos y la ventana no se desborda
        float grow = (buttonHeight + spacing) * 0.5f;
        buttonsGroup.sizeDelta += new Vector2(0f, grow);
        buttonsGroup.anchoredPosition -= new Vector2(0f, grow * 0.5f);

        var audioButton = Duplicate(controlsButton.gameObject, buttonsGroup, "BTN_Audio");
        audioButton.transform.SetSiblingIndex(controlsButton.transform.GetSiblingIndex() + 1);
        SetText(audioButton, "AUDIO");

        // 2) Pantalla GRP_Audio, del mismo tamaño que GRP_Controls
        var controlsRect = (RectTransform)controlsView.transform;
        var view = new GameObject("GRP_Audio", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(view, "Crear panel de audio");
        var viewRect = (RectTransform)view.transform;
        viewRect.SetParent(controlsRect.parent, false);
        viewRect.SetSiblingIndex(controlsRect.GetSiblingIndex() + 1);
        CopyRect(controlsRect, viewRect);
        view.layer = controlsView.layer;

        // Título: copia del título de Controls
        TMP_Text controlsTitle = FindTitle(controlsView);
        TMP_FontAsset font = controlsTitle != null ? controlsTitle.font : null;
        if (controlsTitle != null)
        {
            var title = Duplicate(controlsTitle.gameObject, viewRect, "TXT_AudioTitle");
            title.GetComponent<TMP_Text>().text = "AUDIO";
        }

        // Botones de abajo: copia del grupo de Controls, sin Reset
        var controlButtons = returnButton.transform.parent.gameObject;
        var audioButtons = Duplicate(controlButtons, viewRect, "GRP_AudioButtons");
        Button audioReturn = null;
        foreach (Button b in audioButtons.GetComponentsInChildren<Button>(true))
        {
            if (resetButton != null && b.name == resetButton.name) Object.DestroyImmediate(b.gameObject);
            else if (b.name == returnButton.name) { audioReturn = b; b.name = "BTN_AudioReturn"; }
        }

        // Con un solo botón, el grupo se estrecha para que RETURN tenga el ancho de un botón normal
        var audioButtonsRect = (RectTransform)audioButtons.transform;
        audioButtonsRect.sizeDelta = new Vector2(audioButtonsRect.sizeDelta.x * 0.5f, audioButtonsRect.sizeDelta.y);

        // Filas MUSIC y SFX
        AudioMixer mixer = FindMixer();
        CreateRow(viewRect, "Music", "MUSIC", VolumeSettings.Channel.Music, 90f, font, mixer);
        CreateRow(viewRect, "SFX", "SFX", VolumeSettings.Channel.SFX, -40f, font, mixer);

        // 3) Conectar con PauseMenu y dejar visible la pausa normal
        so.FindProperty("audioView").objectReferenceValue = view;
        so.FindProperty("audioButton").objectReferenceValue = audioButton.GetComponent<Button>();
        so.FindProperty("audioReturnButton").objectReferenceValue = audioReturn;
        if (so.FindProperty("mixer").objectReferenceValue == null)
            so.FindProperty("mixer").objectReferenceValue = mixer;
        so.ApplyModifiedProperties();

        view.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        Selection.activeGameObject = view;

        if (mixer == null)
            Debug.LogWarning("[Pausa] Panel creado, pero no se ha encontrado AM_Main: asígnalo a mano en PauseMenu y en los dos sliders.");
        else
            Debug.Log("[Pausa] Panel de audio creado dentro de PNL_Pause. Activa GRP_Audio para verlo en el editor.");
    }

    // Fila: texto a la izquierda, slider en medio y porcentaje a la derecha
    private static void CreateRow(RectTransform parent, string id, string label, VolumeSettings.Channel channel,
                                  float y, TMP_FontAsset font, AudioMixer mixer)
    {
        var row = NewRect("ROW_" + id, parent);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(1000f, 90f);
        row.anchoredPosition = new Vector2(0f, y);

        var text = NewText("TXT_" + id, row, label, font, 60f, TextAlignmentOptions.MidlineLeft);
        Place(text.rectTransform, 0f, 240f, 90f);

        var value = NewText("TXT_" + id + "Value", row, "100%", font, 52f, TextAlignmentOptions.MidlineRight);
        Place(value.rectTransform, 840f, 160f, 90f);

        var slider = CreateSlider("SLD_" + id, row);
        Place((RectTransform)slider.transform, 270f, 540f, 34f);

        var volume = slider.gameObject.AddComponent<VolumeSlider>();
        var vso = new SerializedObject(volume);
        vso.FindProperty("channel").enumValueIndex = (int)channel;
        vso.FindProperty("mixer").objectReferenceValue = mixer;
        vso.FindProperty("valueText").objectReferenceValue = value;
        vso.ApplyModifiedPropertiesWithoutUndo();
    }

    // Slider cartoon: barra oscura con borde negro, relleno dorado y tirador crema
    private static Slider CreateSlider(string name, RectTransform parent)
    {
        var resources = new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
        };

        var go = DefaultControls.CreateSlider(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetLayer(go, parent.gameObject.layer);

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        var background = go.transform.Find("Background").GetComponent<Image>();
        background.sprite = resources.standard;
        background.type = Image.Type.Sliced;
        background.color = SliderBack;
        AddOutline(background.gameObject, 4f);

        var fill = slider.fillRect.GetComponent<Image>();
        fill.sprite = resources.standard;
        fill.type = Image.Type.Sliced;
        fill.color = SliderFill;

        var handle = slider.handleRect.GetComponent<Image>();
        handle.sprite = resources.standard;
        handle.type = Image.Type.Sliced;
        handle.color = HandleColor;
        slider.handleRect.sizeDelta = new Vector2(34f, 30f);
        AddOutline(handle.gameObject, 4f);
        var shadow = handle.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -6f);

        return slider;
    }

    private static void AddOutline(GameObject go, float size)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(size, -size);
    }

    private static TMP_Text NewText(string name, RectTransform parent, string text, TMP_FontAsset font, float size,
                                    TextAlignmentOptions alignment)
    {
        var rect = NewRect(name, parent);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.black;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    // Coloca un hijo de la fila: x y ancho desde el borde izquierdo, centrado en vertical
    private static void Place(RectTransform rect, float x, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static GameObject Duplicate(GameObject source, Transform parent, string name)
    {
        var copy = Object.Instantiate(source, parent, false);
        copy.name = name;
        copy.SetActive(true);
        Undo.RegisterCreatedObjectUndo(copy, "Crear panel de audio");
        return copy;
    }

    private static void SetText(GameObject go, string text)
    {
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = text;
    }

    // El título de Controls es el texto que es hijo directo de GRP_Controls
    private static TMP_Text FindTitle(GameObject controlsView)
    {
        foreach (Transform child in controlsView.transform)
        {
            var tmp = child.GetComponent<TMP_Text>();
            if (tmp != null) return tmp;
        }
        return null;
    }

    private static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
        to.localScale = Vector3.one;
    }

    private static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
    }

    private static AudioMixer FindMixer()
    {
        foreach (string guid in AssetDatabase.FindAssets("AM_Main t:AudioMixer"))
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(guid));
            if (mixer != null) return mixer;
        }
        return null;
    }
}
