using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Slider de volumen (Music o SFX). Al aparecer se pone en el volumen guardado y, al moverlo, lo guarda y lo aplica
/// al mixer (VolumeSettings), así que vale en todo el juego. Si tiene texto, enseña el porcentaje.
/// Va en el objeto del Slider. Sirve para la pausa y también para los sliders del ATM.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    [Tooltip("Canal que controla este slider.")]
    [SerializeField] private VolumeSettings.Channel channel = VolumeSettings.Channel.Music;
    [Tooltip("Audio Mixer del juego (AM_Main).")]
    [SerializeField] private AudioMixer mixer;
    [Tooltip("Texto donde sale el porcentaje (opcional).")]
    [SerializeField] private TMP_Text valueText;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(OnChanged);
    }

    private void OnEnable()
    {
        slider.SetValueWithoutNotify(VolumeSettings.Get(channel));
        RefreshText(slider.value);
    }

    private void OnChanged(float value)
    {
        VolumeSettings.Set(mixer, channel, value);
        RefreshText(value);
    }

    private void RefreshText(float value)
    {
        if (valueText != null) valueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
