using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Inspector de la tragaperras con un botón por fruta para ver cada parada sin darle a Play.
[CustomEditor(typeof(SlotMachine))]
public class SlotMachineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (Application.isPlaying) return;

        var machine = (SlotMachine)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Probar paradas (sin Play)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pulsa una fruta para ver cómo paran los rodillos. Si no queda centrada, pon su marca " +
            "en el centro de la fruta que ves (Ctrl + Shift) y vuelve a pulsar el botón.",
            MessageType.Info);

        string[] names = machine.SymbolNames;
        for (int i = 0; i < names.Length; i++)
        {
            if (GUILayout.Button(names[i]))
            {
                machine.PreviewSymbol(i);
                SceneView.RepaintAll();
            }
        }

        GUI.enabled = machine.IsPreviewing;
        if (GUILayout.Button("Volver a reposo"))
        {
            machine.ResetPreview();
            SceneView.RepaintAll();
        }
        GUI.enabled = true;
    }
}

// Seguridad: antes de dar a Play, guardar la escena o recompilar, los rodillos vuelven a su reposo.
[InitializeOnLoad]
internal static class SlotPreviewSafety
{
    static SlotPreviewSafety()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change == PlayModeStateChange.ExitingEditMode) SlotMachine.ResetAllPreviews();
        };
        EditorSceneManager.sceneSaving += (scene, path) => SlotMachine.ResetAllPreviews();
        AssemblyReloadEvents.beforeAssemblyReload += SlotMachine.ResetAllPreviews;
    }
}
