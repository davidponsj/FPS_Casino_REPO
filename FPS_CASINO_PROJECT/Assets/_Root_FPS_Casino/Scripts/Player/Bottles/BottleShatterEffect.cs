using UnityEngine;

/// <summary>
/// Efecto de la botella al romperse: cristales que saltan y salpicadura del líquido.
/// Va en la raíz del prefab de partículas. Los cristales se reproducen solos (Play On Awake);
/// las partículas del líquido (gotas, salpicón...) se reproducen desde aquí para poder ponerles
/// antes el color de la botella. El objeto se borra solo al acabar.
/// </summary>
public class BottleShatterEffect : MonoBehaviour
{
    [Tooltip("Partículas que toman el color del líquido. Desactiva su Play On Awake: este script las lanza con el color correcto.")]
    [SerializeField] private ParticleSystem[] liquidParticles;
    [Tooltip("Segundos hasta borrar el efecto (algo más que la vida de las partículas más largas).")]
    [SerializeField] private float lifetime = 3f;

    public void Play(Color liquidColor)
    {
        foreach (ParticleSystem particles in liquidParticles)
        {
            if (particles == null) continue;

            // Respetamos el alpha que tenga cada sistema (las gotas opacas, el salpicón más suave...).
            ParticleSystem.MainModule main = particles.main;
            Color color = liquidColor;
            color.a = main.startColor.mode == ParticleSystemGradientMode.Color ? main.startColor.color.a : 1f;
            if (color.a <= 0.01f) color.a = 1f;
            main.startColor = color;

            particles.Play();
        }

        Destroy(gameObject, lifetime);
    }
}