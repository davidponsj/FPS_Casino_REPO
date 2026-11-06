using UnityEngine;

/// <summary>
/// Tiñe una explosión de un color al crearla: las partículas que le digas (fogonazo, chispas, trozos...) y sus luces
/// pasan a ese color, con algo de variación hacia el blanco para que no quede plana. Lo que no esté en la lista (el humo,
/// por ejemplo) se queda como está, así sigue pareciendo una explosión de verdad.
///
/// Va en la raíz del prefab de la explosión (PF_GrenadeExplosion). Lo usa FruitGrenadeColor: cada granada de fruta tiñe
/// la misma explosión de su color, así que hay UNA explosión para las 6 frutas.
/// </summary>
public class ExplosionTint : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Qué se tiñe")]
    [Tooltip("Partículas que toman el color de la fruta (fogonazo, chispas, trozos, onda...). Las que no pongas (el humo) no cambian.")]
    [SerializeField] private ParticleSystem[] tintedParticles;
    [Tooltip("Luces de la explosión que toman el color de la fruta. Vacío = ninguna.")]
    [SerializeField] private Light[] tintedLights;

    [Header("Aspecto")]
    [Tooltip("Cuánto se aclaran algunas partículas hacia el blanco, al azar. 0.4 = mezcla de color de fruta y su versión clara: más viva.")]
    [Range(0f, 1f)]
    [SerializeField] private float whiteMix = 0.4f;
    [Tooltip("Brillo de las partículas con el Bloom (si su material tiene Emission). 2 = brillan sin quemar.")]
    [Min(0f)]
    [SerializeField] private float glow = 2f;

    /// <summary>Pone la explosión del color dado y la arranca de nuevo, para que la primera ráfaga ya salga de ese color.</summary>
    public void Apply(Color color)
    {
        var block = new MaterialPropertyBlock();
        Color light = Color.Lerp(color, Color.white, whiteMix);

        foreach (ParticleSystem particles in tintedParticles)
        {
            if (particles == null) continue;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color, light); // al azar entre el color y su versión clara
            particles.Play(true);

            // El brillo (emisión del material) también del color de la fruta, solo en este renderer.
            if (particles.TryGetComponent(out ParticleSystemRenderer renderer))
            {
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, color * glow);
                renderer.SetPropertyBlock(block);
            }
        }

        if (tintedLights != null)
            foreach (Light explosionLight in tintedLights)
                if (explosionLight != null) explosionLight.color = color;
    }
}
