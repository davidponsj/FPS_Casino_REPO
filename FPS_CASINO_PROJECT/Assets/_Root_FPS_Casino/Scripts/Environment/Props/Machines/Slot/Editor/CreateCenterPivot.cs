using UnityEditor;
using UnityEngine;

// Herramienta de editor: crea un pivote vacío en el centro del objeto seleccionado y lo mete dentro,
// para que gire sobre su centro aunque el modelo traiga el pivote en otro sitio.
public static class CreateCenterPivot
{
    [MenuItem("Tools/Crear pivote en el centro")]
    private static void Create()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            Debug.LogWarning("[Pivote] Selecciona primero el objeto.");
            return;
        }

        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning($"[Pivote] {target.name} no tiene modelo.");
            return;
        }

        // Vacío en el centro del modelo, con la misma orientación para que los ejes no cambien
        var pivot = new GameObject(target.name + "_Pivot");
        Undo.RegisterCreatedObjectUndo(pivot, "Crear pivote");
        pivot.transform.SetParent(target.transform.parent, false);
        pivot.transform.position = renderer.bounds.center;
        pivot.transform.rotation = target.transform.rotation;

        Undo.SetTransformParent(target.transform, pivot.transform, "Crear pivote");
        Selection.activeGameObject = pivot;
        Debug.Log($"[Pivote] Creado {pivot.name} en el centro de {target.name}.");
    }
}