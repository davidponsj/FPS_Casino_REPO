using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Fogonazo simple hecho con una Light, sin necesidad de sprites/texturas. Colócala como
/// hija de MuzzlePoint (o en el propio MuzzlePoint) y llama a Flash() desde el arma al disparar.
/// </summary>
[RequireComponent(typeof(Light))]
public class MuzzleFlash : MonoBehaviour
{
    [Header("Fogonazo")]
    [Tooltip("Segundos que la luz está encendida en cada disparo. 0.05 = un destello corto.")]
    [FormerlySerializedAs("flashDuration")]
    [SerializeField] private float flashSeconds = 0.05f;
    [Tooltip("Intensidad de la luz durante el destello. 6 = se nota en paredes cercanas; más alto ilumina más lejos.")]
    [SerializeField] private float flashIntensity = 6f;

    private Light flashLight;

    private void Awake()
    {
        flashLight = GetComponent<Light>();
        flashLight.enabled = false;
    }

    public void Flash()
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashLight.intensity = flashIntensity;
        flashLight.enabled = true;
        yield return new WaitForSeconds(flashSeconds);
        flashLight.enabled = false;
    }
}