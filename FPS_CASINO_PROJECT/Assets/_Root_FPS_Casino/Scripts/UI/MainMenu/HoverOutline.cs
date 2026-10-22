using System.Collections.Generic;
using UnityEngine;

public class HoverOutline : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Grosor del borde. 1.05 = 5% más grande")]
    [SerializeField] private float outlineScale = 1.05f;

    [Header("Texto al pasar el ratón")]
    [SerializeField] private string hoverLabel = "";

    public string HoverLabel => hoverLabel;

    private readonly List<GameObject> outlines = new List<GameObject>();

    private void Awake()
    {
        if (outlineMaterial == null)
        {
            Debug.LogWarning("[HoverOutline] Falta asignar el material del borde.", this);
            return;
        }

        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;

            var go = new GameObject("Outline");
            go.transform.SetParent(filter.transform, false);
            go.transform.localScale = Vector3.one * outlineScale;

            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = outlineMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            go.SetActive(false);
            outlines.Add(go);
        }
    }

    public void SetHighlighted(bool value)
    {
        foreach (var o in outlines)
            if (o != null) o.SetActive(value);
    }
}