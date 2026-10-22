using System.Collections;
using UnityEngine;

/// <summary>
/// Fogonazo simple hecho con una Light, sin necesidad de sprites/texturas. Colócala como
/// hija de MuzzlePoint (o en el propio MuzzlePoint) y llama a Flash() desde el arma al disparar.
/// </summary>
[RequireComponent(typeof(Light))]
public class MuzzleFlash : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.05f;
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
        yield return new WaitForSeconds(flashDuration);
        flashLight.enabled = false;
    }
}