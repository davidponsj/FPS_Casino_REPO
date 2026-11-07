using UnityEngine;

/// <summary>
/// Color de una granada de fruta: pinta su estela (TrailRenderer) del color de la fruta y, al explotar, se lo pasa a la
/// explosión (ExplosionTint) para que salga teñida: naranja con cosas naranjas, sandía con cosas verdes...
///
/// Va en la raíz de PF_Grenade (junto a GrenadeProjectile y el TrailRenderer). En cada variante de fruta solo se cambia
/// Fruit Color. El brillo de la estela usa la emisión del material, así que el color HDR (Intensity) hace que brille
/// con el Bloom sin tener que crear un material por fruta.
/// </summary>
public class FruitGrenadeColor : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Color")]
    [Tooltip("Color de la fruta. Se usa en la estela y en la explosión. Ej.: naranja FF8A1A, sandía 4CFF6A.")]
    [SerializeField] private Color fruitColor = new Color(1f, 0.54f, 0.1f);
    [Tooltip("Brillo de la estela con el Bloom. 2 = brilla sin quemar; 0 = sin brillo extra.")]
    [Min(0f)]
    [SerializeField] private float trailGlow = 2f;

    [Header("Referencias")]
    [Tooltip("Estela de la granada. Vacío = la busca en este objeto o en sus hijos.")]
    [SerializeField] private TrailRenderer trail;

    public Color FruitColor => fruitColor;

    private void Awake()
    {
        if (trail == null) trail = GetComponentInChildren<TrailRenderer>();
        PaintTrail();
    }

    /// <summary>La estela empieza del color de la fruta y se desvanece hacia la cola.</summary>
    private void PaintTrail()
    {
        if (trail == null) return;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(fruitColor, Color.white, 0.35f), 0f), new GradientColorKey(fruitColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        // Brillo con un MaterialPropertyBlock: cambia el color de emisión solo de esta estela, sin crear materiales.
        var block = new MaterialPropertyBlock();
        trail.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, fruitColor * trailGlow);
        trail.SetPropertyBlock(block);
    }

    /// <summary>Lo llama GrenadeProjectile al crear la explosión: si la explosión sabe teñirse, se tiñe del color de la fruta.</summary>
    public void TintExplosion(GameObject explosion)
    {
        if (explosion != null && explosion.TryGetComponent(out ExplosionTint tint))
            tint.Apply(fruitColor);
    }
}
