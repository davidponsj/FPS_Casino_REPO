using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Borde que aparece en un objeto del menú al pasar el ratón por encima (lo enciende MenuHoverManager).
/// Crea una copia de cada malla, algo más grande y con el material del borde, oculta hasta el hover.
/// </summary>
public class HoverOutline : MonoBehaviour
{
    [Tooltip("Material con el que se pinta el borde.")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Grosor del borde. 1.05 = 5% más grande")]
    [Min(1f)]
    [SerializeField] private float outlineScale = 1.05f;

    [Header("Texto al pasar el ratón")]
    [Tooltip("Nombre que sale en pantalla con el ratón encima. Vacío = sin texto.")]
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