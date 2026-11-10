using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Borde que aparece en un objeto del menú al pasar el ratón por encima (lo enciende MenuHoverManager).
/// Crea una copia de cada malla, algo más grande y con el material del borde, oculta hasta el hover.
/// Con Outline Thickness el borde tiene el mismo grosor por todos los lados, aunque la pieza sea alargada.
/// </summary>
public class HoverOutline : MonoBehaviour
{
    [Tooltip("Material con el que se pinta el borde.")]
    [SerializeField] private Material outlineMaterial;
    [Tooltip("Grosor del borde en unidades de la escena, igual por todos los lados. 0 = usar Outline Scale.")]
    [Min(0f)]
    [SerializeField] private float outlineThickness = 0f;
    [Tooltip("Solo si Outline Thickness es 0: tamaño de la copia. 1.05 = 5% más grande.")]
    [Min(1f)]
    [SerializeField] private float outlineScale = 1.05f;

    [Header("Texto al pasar el ratón")]
    [Tooltip("Nombre que sale en pantalla con el ratón encima. Vacío = sin texto.")]
    [SerializeField] private string hoverLabel = "";

    [Header("Extra al pasar el ratón")]
    [Tooltip("Objetos que se encienden con el borde y se apagan al quitar el ratón (p. ej. el marco blanco de la puerta). Vacío = ninguno.")]
    [SerializeField] private GameObject[] alsoShowOnHover = new GameObject[0];

    public string HoverLabel => hoverLabel;

    private readonly List<GameObject> outlines = new List<GameObject>();

    private void Awake()
    {
        foreach (var extra in alsoShowOnHover)
            if (extra != null) extra.SetActive(false);


        if (outlineMaterial == null)
        {
            Debug.LogWarning("[HoverOutline] Falta asignar el material del borde.", this);
            return;
        }

        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;

            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 scale = outlineThickness > 0f
                ? ThicknessToScale(bounds.size, filter.transform.lossyScale)
                : Vector3.one * outlineScale;

            var go = new GameObject("Outline");
            go.transform.SetParent(filter.transform, false);
            go.transform.localScale = scale;

            // Escala desde el centro de la pieza y no desde su pivot,
            // para que el borde sobresalga igual por todos los lados
            go.transform.localPosition = Vector3.Scale(bounds.center, Vector3.one - scale);

            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = outlineMaterial;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            go.SetActive(false);
            outlines.Add(go);
        }
    }

    // Cuánto agrandar cada eje para sobresalir "outlineThickness" por cada lado
    private Vector3 ThicknessToScale(Vector3 meshSize, Vector3 lossyScale)
    {
        return new Vector3(
            AxisScale(meshSize.x, lossyScale.x),
            AxisScale(meshSize.y, lossyScale.y),
            AxisScale(meshSize.z, lossyScale.z));
    }

    private float AxisScale(float meshSize, float lossy)
    {
        float worldSize = meshSize * Mathf.Abs(lossy);
        return worldSize > 0.0001f ? 1f + 2f * outlineThickness / worldSize : 1f;
    }

    public void SetHighlighted(bool value)
    {
        foreach (var o in outlines)
            if (o != null) o.SetActive(value);

        foreach (var extra in alsoShowOnHover)
            if (extra != null) extra.SetActive(value);
    }
}