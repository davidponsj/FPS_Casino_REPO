using UnityEngine;

public class CameraFocusTarget : MonoBehaviour
{
    [Tooltip("Posición y rotación donde se colocará la cámara al hacer zoom")]
    [SerializeField] private Transform viewPoint;

    public Transform ViewPoint => viewPoint;
}