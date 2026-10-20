using UnityEngine;
using UnityEngine.Events;

public class CameraFocusTarget : MonoBehaviour
{
    [Tooltip("Posición y rotación donde se colocará la cámara al hacer zoom")]
    [SerializeField] private Transform viewPoint;

    [Header("Eventos")]
    [Tooltip("Se llama cuando la cámara termina de llegar")]
    [SerializeField] private UnityEvent onArrived;
    [Tooltip("Se llama cuando la cámara empieza a volver")]
    [SerializeField] private UnityEvent onLeaving;

    public Transform ViewPoint => viewPoint;

    public void NotifyArrived() => onArrived.Invoke();
    public void NotifyLeaving() => onLeaving.Invoke();
}