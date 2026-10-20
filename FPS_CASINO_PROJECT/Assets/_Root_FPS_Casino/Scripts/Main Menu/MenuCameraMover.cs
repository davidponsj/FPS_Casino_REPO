using System.Collections;
using UnityEngine;

public class MenuCameraMover : MonoBehaviour
{
    [SerializeField] private Camera menuCamera;
    [SerializeField] private float moveDuration = 1.5f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private Coroutine routine;
    private CameraFocusTarget currentFocus;

    public bool IsMoving { get; private set; }
    public bool IsZoomed { get; private set; }

    private void Awake()
    {
        if (menuCamera == null) menuCamera = Camera.main;

        startPosition = menuCamera.transform.position;
        startRotation = menuCamera.transform.rotation;
    }

    public void MoveTo(CameraFocusTarget focus)
    {
        if (focus == null || focus.ViewPoint == null || IsMoving) return;

        currentFocus = focus;
        StartMove(focus.ViewPoint.position, focus.ViewPoint.rotation, true);
    }

    public void ReturnToStart()
    {
        if (IsMoving || !IsZoomed) return;

        if (currentFocus != null) currentFocus.NotifyLeaving();
        currentFocus = null;

        StartMove(startPosition, startRotation, false);
    }

    private void StartMove(Vector3 pos, Quaternion rot, bool zoomed)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(MoveRoutine(pos, rot, zoomed));
    }

    private IEnumerator MoveRoutine(Vector3 endPos, Quaternion endRot, bool zoomed)
    {
        IsMoving = true;

        Transform cam = menuCamera.transform;
        Vector3 fromPos = cam.position;
        Quaternion fromRot = cam.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            cam.position = Vector3.Lerp(fromPos, endPos, k);
            cam.rotation = Quaternion.Slerp(fromRot, endRot, k);
            yield return null;
        }

        cam.position = endPos;
        cam.rotation = endRot;

        IsZoomed = zoomed;
        IsMoving = false;

        if (zoomed && currentFocus != null) currentFocus.NotifyArrived();
    }
}