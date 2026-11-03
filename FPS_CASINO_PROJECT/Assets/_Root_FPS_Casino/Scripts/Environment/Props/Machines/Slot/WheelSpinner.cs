using UnityEngine;

public class WheelSpinner : MonoBehaviour
{
    [Tooltip("Grados por segundo. Positivo = sentido horario visto desde arriba")]
    [SerializeField] private float speed = 30f;

    public float Speed => speed;

    private void Update()
    {
        transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
    }
}