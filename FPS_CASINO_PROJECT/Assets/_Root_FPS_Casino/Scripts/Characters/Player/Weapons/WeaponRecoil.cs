using UnityEngine;

/// <summary>
/// Retroceso del arma en la mano, hecho por código: con cada disparo el arma da un golpe hacia atrás y hacia arriba,
/// con un poco de giro al azar, y vuelve sola a su sitio como un muelle. Con un arma automática los golpes se acumulan
/// (hasta un tope) y el arma "vibra" mientras disparas, que es lo que el clip de disparo del FBX no llega a transmitir.
///
/// Va en la raíz del prefab del arma (PF_Subfusil...), junto a WeaponBase, y escucha su OnFired. Se suma a la animación
/// del FBX (WeaponAnimator mueve las piezas del modelo; esto mueve el arma entera), así que pueden usarse a la vez.
/// Los movimientos se miden desde la cámara (atrás = hacia ti, arriba = hacia el techo de la pantalla), así que valen
/// igual para todas las armas aunque cada prefab esté girado y escalado distinto dentro del WeaponHolder.
/// </summary>
[RequireComponent(typeof(WeaponBase))]
public class WeaponRecoil : MonoBehaviour
{
    [Header("Golpe de cada disparo")]
    [Tooltip("Metros que va el arma hacia atrás (hacia la cámara) con cada disparo. 0.03 = subfusil; 0.08 = escopeta.")]
    [Min(0f)]
    [SerializeField] private float kickBackMeters = 0.03f;
    [Tooltip("Metros que sube el arma con cada disparo. 0.005 = casi nada; da sensación de que salta.")]
    [Min(0f)]
    [SerializeField] private float kickUpMeters = 0.005f;
    [Tooltip("Grados que se levanta el cañón con cada disparo. 2 = subfusil; 8 = escopeta.")]
    [Min(0f)]
    [SerializeField] private float kickPitchDegrees = 2f;
    [Tooltip("Grados que se va el cañón a izquierda o derecha al azar con cada disparo. 1 = tiembla un poco.")]
    [Min(0f)]
    [SerializeField] private float kickYawDegrees = 1f;
    [Tooltip("Grados que se ladea el arma al azar con cada disparo. 2 = se nota el golpe en la muñeca.")]
    [Min(0f)]
    [SerializeField] private float kickRollDegrees = 2f;
    [Tooltip("Variación al azar de cada golpe. 0.2 = cada disparo entre un 80% y un 120%; así no parece un robot.")]
    [Range(0f, 1f)]
    [SerializeField] private float randomness = 0.2f;

    [Header("Tope (armas automáticas)")]
    [Tooltip("Máximo que puede irse hacia atrás sumando golpes seguidos. 0.06 = el subfusil no se te mete en la cara.")]
    [Min(0f)]
    [SerializeField] private float maxBackMeters = 0.06f;
    [Tooltip("Máximo que puede levantarse el cañón sumando golpes seguidos. 6 = no apunta al techo.")]
    [Min(0f)]
    [SerializeField] private float maxPitchDegrees = 6f;

    [Header("Muelle")]
    [Tooltip("Rapidez con la que el arma llega al golpe. Alto = seco, de golpe. 35 = casi instantáneo, como un disparo.")]
    [Min(0.1f)]
    [SerializeField] private float snappiness = 35f;
    [Tooltip("Rapidez con la que vuelve a su sitio. 10 = vuelve en unos 0.3 s. Bájalo en la escopeta para que el golpe dure más.")]
    [Min(0.1f)]
    [SerializeField] private float returnSpeed = 10f;

    [Header("Depuración")]
    [Tooltip("Muestra junto al arma cuánto se ha ido hacia atrás y cuánto se ha levantado en este momento.")]
    [SerializeField] private bool showDebug;

    private WeaponBase weapon;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    // Hacia dónde va el golpe (sube con cada disparo y vuelve a 0) y dónde está el arma ahora (lo persigue).
    // x = metros hacia atrás, y = metros hacia arriba; ángulos: pitch (cañón arriba), yaw (lado), roll (ladeo).
    private Vector2 targetOffset, currentOffset;
    private Vector3 targetAngles, currentAngles;

    private void Awake()
    {
        weapon = GetComponent<WeaponBase>();
        // La pose de reposo es donde el WeaponManager deja el arma al crearla (la del prefab).
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
    }

    private void OnEnable() => weapon.OnFired += HandleFired;

    private void OnDisable()
    {
        weapon.OnFired -= HandleFired;
        // Al guardar el arma, vuelve a su pose sin golpe pendiente.
        targetOffset = currentOffset = Vector2.zero;
        targetAngles = currentAngles = Vector3.zero;
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
    }

    private void HandleFired()
    {
        float strength = 1f + Random.Range(-randomness, randomness);

        targetOffset.x = Mathf.Min(targetOffset.x + kickBackMeters * strength, maxBackMeters);
        targetOffset.y += kickUpMeters * strength;
        targetAngles.x = Mathf.Min(targetAngles.x + kickPitchDegrees * strength, maxPitchDegrees);
        targetAngles.y += Random.Range(-kickYawDegrees, kickYawDegrees);
        targetAngles.z += Random.Range(-kickRollDegrees, kickRollDegrees);
    }

    // LateUpdate: después de que la cámara se haya movido en este frame, así el arma no va con un frame de retraso.
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // juego en pausa

        // El golpe vuelve a 0 (el muelle) y el arma lo persigue muy rápido (el "golpe seco").
        float back = 1f - Mathf.Exp(-returnSpeed * dt);
        float snap = 1f - Mathf.Exp(-snappiness * dt);
        targetOffset = Vector2.Lerp(targetOffset, Vector2.zero, back);
        targetAngles = Vector3.Lerp(targetAngles, Vector3.zero, back);
        currentOffset = Vector2.Lerp(currentOffset, targetOffset, snap);
        currentAngles = Vector3.Lerp(currentAngles, targetAngles, snap);

        ApplyPose();
    }

    /// <summary>
    /// Coloca el arma: su pose de reposo más el golpe, medido en los ejes de la cámara. Así "atrás" es siempre hacia
    /// el jugador aunque el prefab esté girado 90° y escalado dentro del WeaponHolder.
    /// </summary>
    private void ApplyPose()
    {
        Transform parent = transform.parent;
        Camera cam = weapon.PlayerCamera != null ? weapon.PlayerCamera : Camera.main;
        if (parent == null || cam == null) return;

        Transform view = cam.transform;
        Vector3 restPosition = parent.TransformPoint(baseLocalPosition);
        Quaternion restRotation = parent.rotation * baseLocalRotation;

        transform.position = restPosition - view.forward * currentOffset.x + view.up * currentOffset.y;

        // Cañón arriba = giro negativo sobre el eje derecho de la cámara.
        Quaternion kick = Quaternion.AngleAxis(-currentAngles.x, view.right)
                        * Quaternion.AngleAxis(currentAngles.y, view.up)
                        * Quaternion.AngleAxis(currentAngles.z, view.forward);
        transform.rotation = kick * restRotation;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        DebugLabels.At(transform.position, 4, $"Retroceso · {currentOffset.x * 100f:0.0} cm atrás · {currentAngles.x:0.0}° arriba", DebugLabels.Weapon);
    }
#endif
}
