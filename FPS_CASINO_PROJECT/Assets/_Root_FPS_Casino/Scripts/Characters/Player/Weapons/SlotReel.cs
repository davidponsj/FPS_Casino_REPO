using System;
using UnityEngine;

/// <summary>
/// Rodillo de tragaperras del lanzagranadas. Cada vez que se le pide (Spin) elige una fruta al azar y gira el rodillo
/// DESDE DONDE ESTÁ hasta esa fruta: da alguna vuelta entera, frena, se pasa un poco y encaja (el "clac"), y al parar
/// destella del color de la fruta. La fruta que queda mirando hacia delante es la que está "cargada": su granada es la
/// que sale en el siguiente disparo. Puede repetir fruta: entonces da las vueltas completas y vuelve a parar en ella.
///
/// El giro lo hace el código (no hay que animar las 36 combinaciones de "de esta fruta a esta otra"): cada fruta tiene
/// su ángulo en el rodillo y se calcula cuánto girar para llegar a ella.
///
/// Gira la MALLA del rodillo alrededor del eje de su padre (el hueso del rodillo). Así las animaciones del FBX pueden
/// seguir moviendo el hueso (en la recarga el rodillo se inclina para cargar) y el giro de la fruta va encima.
/// Se aplica DESPUÉS de WeaponAnimator (DefaultExecutionOrder), así que siempre manda en el giro de la malla.
///
/// Va en la raíz del prefab del lanzagranadas, junto a GrenadeLauncherWeapon (que es quien le pide girar).
/// </summary>
[DefaultExecutionOrder(100)]
public class SlotReel : MonoBehaviour
{
    [Serializable]
    public class Fruit
    {
        [Tooltip("Nombre de la fruta, solo para reconocerla en el Inspector y en la depuración (p. ej. Limon).")]
        public string name = "Limon";
        [Tooltip("Grados que hay que girar el rodillo desde el reposo para que esta fruta mire hacia delante. Es el valor de rotación Y del hueso del rodillo en Blender con la fruta centrada.")]
        public float angle;
        [Tooltip("Granada que sale cuando el rodillo para en esta fruta (PF_Grenade_Limon...).")]
        public GrenadeProjectile grenadePrefab;
        [Tooltip("Color del destello al parar en esta fruta. Amarillo para el limón, rojo para la cereza...")]
        public Color flashColor = Color.yellow;
    }

    [Header("Rodillo")]
    [Tooltip("La malla del rodillo, la pieza que se ve girar (Cylinder.001, hija del hueso Bone.003). Gira alrededor del eje de su padre.")]
    [SerializeField] private Transform reel;
    [Tooltip("Eje de giro, en los ejes locales del PADRE de la malla (el hueso del rodillo). (0, 1, 0) = su eje Y, que es el que gira en Blender.")]
    [SerializeField] private Vector3 parentAxis = Vector3.up;
    [Tooltip("Sentido de giro. Si al parar sale otra fruta en vez de la que toca, cambia a 1 o a -1. Blender y Unity cuentan los ángulos al revés, por eso suele ser -1.")]
    [SerializeField] private int direction = -1;

    [Header("Frutas")]
    [Tooltip("Las frutas del rodillo, cada una con su ángulo. El orden de la lista da igual.")]
    [SerializeField] private Fruit[] fruits = new Fruit[6];

    [Header("Giro")]
    [Tooltip("Segundos que tarda en girar hasta la fruta (sin contar el encaje). 0.45 = rápido y dinámico.")]
    [Min(0.05f)]
    [SerializeField] private float spinSeconds = 0.45f;
    [Tooltip("Vueltas enteras extra que da antes de parar. 1 = se ve que gira; 0 = va directo a la fruta.")]
    [Min(0)]
    [SerializeField] private int extraTurns = 1;
    [Tooltip("Grados que se pasa al frenar antes de encajar (el \"clac\"). 8 = se nota sin exagerar. 0 = para en seco.")]
    [Min(0f)]
    [SerializeField] private float overshootDegrees = 8f;
    [Tooltip("Segundos que tarda en volver de esa pasada y encajar en la fruta. 0.12 = encaje rápido.")]
    [Min(0f)]
    [SerializeField] private float settleSeconds = 0.12f;

    [Header("Destello al parar (opcionales)")]
    [Tooltip("Luz (Point Light) junto al rodillo. Se enciende del color de la fruta al parar y se apaga sola. Vacío = sin luz.")]
    [SerializeField] private Light flashLight;
    [Tooltip("Intensidad de la luz en el destello. 4 = se ve bien en la mano sin deslumbrar.")]
    [Min(0f)]
    [SerializeField] private float flashIntensity = 4f;
    [Tooltip("Segundos que tarda en apagarse el destello. 0.25 = un fogonazo.")]
    [Min(0.01f)]
    [SerializeField] private float flashSeconds = 0.25f;
    [Tooltip("Partículas junto al rodillo (chispas, brillos). Al parar sueltan una ráfaga del color de la fruta. Vacío = sin partículas.")]
    [SerializeField] private ParticleSystem flashParticles;
    [Tooltip("Partículas de la ráfaga del destello. 12 = un chisporroteo.")]
    [Min(0)]
    [SerializeField] private int flashParticleCount = 12;

    [Header("Calibración")]
    [Tooltip("-1 = normal (fruta al azar). 0, 1, 2... = sale SIEMPRE esa fruta de la lista y, en Play, con el rodillo parado, " +
             "al cambiar su Angle en el Inspector el rodillo se mueve en directo. Sirve para dejar cada fruta centrada a ojo. " +
             "Ojo: lo que cambies en Play se pierde al salir; apunta el número.")]
    [SerializeField] private int calibrateFruit = -1;

    [Header("Depuración")]
    [Tooltip("Muestra junto al arma la fruta cargada, su ángulo y si el rodillo está girando.")]
    [SerializeField] private bool showDebug;

    /// <summary>Se dispara cuando el rodillo encaja en una fruta (ya está cargada). Para sonidos o efectos extra.</summary>
    public event Action<Fruit> OnFruitLanded;

    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private float currentAngle;        // grados girados desde el reposo (en una vuelta: 0-360)
    private float startAngle;
    private float targetAngle;
    private float spinStartTime = -1f; // -1 = parado
    private bool landed = true;
    private float flashStartTime = -1f;

    /// <summary>True mientras gira o encaja. El arma no dispara hasta que pare.</summary>
    public bool IsSpinning => spinStartTime >= 0f;
    /// <summary>Fruta cargada (la que mira hacia delante, o a la que va el giro en curso). Null hasta el primer giro.</summary>
    public Fruit CurrentFruit { get; private set; }

    private void Awake()
    {
        if (reel != null)
        {
            // Pose de la malla en reposo, respecto a su hueso. El giro se aplica siempre a partir de aquí.
            restLocalPosition = reel.localPosition;
            restLocalRotation = reel.localRotation;
        }
        if (flashLight != null) flashLight.intensity = 0f;
    }

    /// <summary>Elige una fruta al azar (puede repetir) y gira el rodillo desde donde está hasta ella.</summary>
    public void Spin()
    {
        if (fruits == null || fruits.Length == 0) return;

        bool calibrating = calibrateFruit >= 0 && calibrateFruit < fruits.Length;
        CurrentFruit = fruits[calibrating ? calibrateFruit : UnityEngine.Random.Range(0, fruits.Length)];

        // Cuánto hay que avanzar desde aquí para llegar a esa fruta, siempre hacia delante.
        float forward = Mathf.Repeat(CurrentFruit.angle - currentAngle, 360f);
        if (forward < 1f) forward += 360f; // misma fruta: vuelta completa, que se vea que ha girado

        startAngle = currentAngle;
        targetAngle = currentAngle + forward + extraTurns * 360f;
        spinStartTime = Time.time;
        landed = false;
    }

    // LateUpdate y orden 100: después de que WeaponAnimator ponga la pose de sus clips, así el giro va encima.
    private void LateUpdate()
    {
        UpdateSpin();

        // Calibración: parado, el rodillo sigue en directo el Angle de la fruta (para ajustarlo a ojo en Play).
        if (!IsSpinning && calibrateFruit >= 0 && CurrentFruit != null)
            currentAngle = Mathf.Repeat(CurrentFruit.angle, 360f);

        ApplyRotation();
        UpdateFlash();
    }

    /// <summary>
    /// Fase 1: frena desde rápido hasta pasarse Overshoot Degrees de la fruta (ease out).
    /// Fase 2: vuelve a la fruta y encaja (ease in-out). Al acabar la fase 1 destella.
    /// </summary>
    private void UpdateSpin()
    {
        if (!IsSpinning) return;

        float age = Time.time - spinStartTime;
        float overshootTarget = targetAngle + overshootDegrees;

        if (age < spinSeconds)
        {
            float t = age / spinSeconds;
            float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
            currentAngle = Mathf.LerpUnclamped(startAngle, overshootTarget, easeOut);
            return;
        }

        if (!landed)
        {
            landed = true;
            Flash();
            OnFruitLanded?.Invoke(CurrentFruit);
        }

        float settle = age - spinSeconds;
        if (settle < settleSeconds)
        {
            currentAngle = Mathf.Lerp(overshootTarget, targetAngle, Mathf.SmoothStep(0f, 1f, settle / settleSeconds));
            return;
        }

        // Parado: se guarda el ángulo "dentro de una vuelta" para que no crezca sin fin.
        currentAngle = Mathf.Repeat(targetAngle, 360f);
        spinStartTime = -1f;
    }

    /// <summary>Gira la malla alrededor del eje de su padre (el hueso), que pasa por el origen del hueso.</summary>
    private void ApplyRotation()
    {
        if (reel == null) return;
        Quaternion spin = Quaternion.AngleAxis(currentAngle * direction, parentAxis);
        reel.localPosition = spin * restLocalPosition;
        reel.localRotation = spin * restLocalRotation;
    }

    // ---------------- DESTELLO ----------------

    private void Flash()
    {
        if (CurrentFruit == null) return;
        flashStartTime = Time.time;

        if (flashLight != null)
        {
            flashLight.color = CurrentFruit.flashColor;
            flashLight.intensity = flashIntensity;
        }

        if (flashParticles != null && flashParticleCount > 0)
        {
            var emit = new ParticleSystem.EmitParams { startColor = CurrentFruit.flashColor, applyShapeToPosition = true };
            flashParticles.Emit(emit, flashParticleCount);
        }
    }

    private void UpdateFlash()
    {
        if (flashLight == null || flashStartTime < 0f) return;

        float fade01 = (Time.time - flashStartTime) / flashSeconds;
        if (fade01 >= 1f)
        {
            flashLight.intensity = 0f;
            flashStartTime = -1f;
            return;
        }
        flashLight.intensity = flashIntensity * (1f - fade01) * (1f - fade01); // se apaga rápido al principio
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        string fruit = CurrentFruit != null ? $"{CurrentFruit.name} ({CurrentFruit.angle:0}°)" : "—";
        string state = IsSpinning ? "girando" : "cargada";
        Vector3 anchor = reel != null ? reel.position : transform.position;
        DebugLabels.At(anchor, 5, $"Rodillo · {fruit} · {state} · ángulo {currentAngle:0}°", DebugLabels.Casino);
    }
#endif
}
