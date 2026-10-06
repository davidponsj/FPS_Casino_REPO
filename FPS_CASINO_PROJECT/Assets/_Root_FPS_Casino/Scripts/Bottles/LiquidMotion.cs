using UnityEngine;

public class LiquidMotion : MonoBehaviour
{
    [Header("Bottle")]
    public Transform bottle;

    [Header("Tilt")]
    [Range(0f, 30f)]
    public float maxTilt = 20f;

    [Header("Inertia")]
    [Range(1f, 20f)]
    public float smoothSpeed = 5f;

    [Range(0f, 20f)]
    public float inertia = 8f;

    [Header("Oscillation")]
    [Range(0f, 10f)]
    public float oscillation = 2f;

    [Range(0f, 10f)]
    public float damping = 4f;

    private Vector3 currentTilt;
    private Vector3 velocity;

    private Quaternion initialRotation;

    void Start()
    {
        initialRotation = transform.localRotation;
    }

    void LateUpdate()
    {
        if (bottle == null)
            return;

        // Gravedad expresada en el espacio local de la botella.
        Vector3 gravityUp =
            bottle.InverseTransformDirection(Vector3.up);

        // Calculamos cuánto debería inclinarse el líquido.
        Vector3 targetUp = Vector3.RotateTowards(
            Vector3.up,
            gravityUp,
            maxTilt * Mathf.Deg2Rad,
            0f
        );

        Quaternion targetRotation =
            Quaternion.FromToRotation(
                Vector3.up,
                targetUp
            );

        // Diferencia entre la orientación actual y la deseada.
        Vector3 targetEuler =
            targetRotation.eulerAngles;

        targetEuler.x = NormalizeAngle(targetEuler.x);
        targetEuler.y = NormalizeAngle(targetEuler.y);
        targetEuler.z = NormalizeAngle(targetEuler.z);

        // Movimiento con inercia.
        currentTilt = Vector3.SmoothDamp(
            currentTilt,
            targetEuler,
            ref velocity,
            1f / smoothSpeed
        );

        // Pequeña oscilación amortiguada.
        float speed = velocity.magnitude;

        float wave =
            Mathf.Sin(Time.time * inertia) *
            speed *
            oscillation *
            0.02f;

        wave *= Mathf.Exp(-damping * Time.deltaTime);

        Quaternion finalRotation =
            initialRotation *
            Quaternion.Euler(
                currentTilt.x + wave,
                0f,
                currentTilt.z - wave
            );

        transform.localRotation = finalRotation;
    }

    float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }
}