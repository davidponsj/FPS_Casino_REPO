using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Impacto de bala. Va en la raíz del prefab BulletImpact, que tiene como hijos varias capas de
/// partículas (destello, chispas, polvo, esquirlas) y un agujero de bala (decal).
///
/// Las partículas se reproducen solas (Play On Awake) y duran menos de un segundo. Este script se
/// encarga de lo demás:
/// - Gira y escala el agujero al azar, para que no todos los impactos se vean iguales.
/// - Hace que el agujero se desvanezca al final de su vida.
/// - Borra el efecto ENTERO (raíz incluida) al terminar, para no dejar objetos vacíos en la escena.
///
/// El arma lo crea mirando hacia fuera de la superficie (su eje Z = normal del impacto).
/// </summary>
public class BulletImpactEffect : MonoBehaviour
{
    [Header("Agujero de bala (decal)")]
    [Tooltip("Quad hijo con el material del agujero. Opcional: sin él, el efecto solo son partículas.")]
    [SerializeField] private Renderer decalRenderer;
    [Tooltip("Color del agujero. La transparencia (A) marca lo oscuro que se ve: 0.85 = casi opaco.")]
    [SerializeField] private Color decalColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    [Tooltip("Tamaño mínimo (X) y máximo (Y) del agujero, en metros. Cada impacto elige uno al azar entre los dos.")]
    [FormerlySerializedAs("decalSizeRange")]
    [SerializeField] private Vector2 decalSizeMeters = new Vector2(0.05f, 0.09f);
    [Tooltip("Segundos que dura el agujero (y por tanto el efecto entero). 8 = se queda un rato en la pared.")]
    [FormerlySerializedAs("decalLifetime")]
    [SerializeField] private float decalSeconds = 8f;
    [Tooltip("Segundos que tarda en desvanecerse al final de su vida. 0 = desaparece de golpe.")]
    [FormerlySerializedAs("decalFadeTime")]
    [SerializeField] private float decalFadeSeconds = 2f;

    [Header("Sin agujero")]
    [Tooltip("Si no hay agujero, segundos hasta borrar el efecto (algo más que la partícula más larga). 1.5 = seguro.")]
    [FormerlySerializedAs("particlesLifetime")]
    [SerializeField] private float particlesSeconds = 1.5f;

    // URP usa "_BaseColor" como color principal tanto en Lit como en Unlit.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private MaterialPropertyBlock propertyBlock;
    private float age;
    private float lifetime;

    private void Awake()
    {
        lifetime = decalRenderer != null ? decalSeconds : particlesSeconds;

        if (decalRenderer == null) return;

        // El Quad de Unity se ve por su cara -Z: lo giramos 180° para que mire hacia fuera de la
        // superficie, y le damos un giro aleatorio sobre sí mismo para que ningún agujero sea igual.
        Transform decal = decalRenderer.transform;
        decal.localRotation = Quaternion.Euler(0f, 180f, Random.Range(0f, 360f));
        float size = Random.Range(decalSizeMeters.x, decalSizeMeters.y);
        decal.localScale = new Vector3(size, size, size);

        propertyBlock = new MaterialPropertyBlock();
        SetDecalAlpha(1f);
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (decalRenderer != null && decalFadeSeconds > 0f)
        {
            float remaining = lifetime - age;
            if (remaining < decalFadeSeconds)
                SetDecalAlpha(Mathf.Clamp01(remaining / decalFadeSeconds));
        }

        if (age >= lifetime)
            Destroy(gameObject);
    }

    private void SetDecalAlpha(float alpha01)
    {
        Color color = decalColor;
        color.a *= alpha01;

        // MaterialPropertyBlock: cada agujero se desvanece por su cuenta sin copiar el material.
        decalRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        decalRenderer.SetPropertyBlock(propertyBlock);
    }
}