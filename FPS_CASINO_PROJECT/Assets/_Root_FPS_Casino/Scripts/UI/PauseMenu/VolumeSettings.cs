using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Volumen de la música y de los efectos. Se guarda en PlayerPrefs (como KeyBindings con las teclas) y se aplica al
/// Audio Mixer (AM_Main) con sus parámetros expuestos MusicVolume y SFXVolume. Lo usan VolumeSlider (los sliders de
/// la pausa, y los del ATM si se conectan) y PauseMenu, que lo aplica al empezar la partida.
/// </summary>
public static class VolumeSettings
{
    public enum Channel { Music, SFX }

    private const float MinDb = -80f;

    /// <summary>Volumen guardado del canal, de 0 (silencio) a 1 (máximo).</summary>
    public static float Get(Channel channel) => PlayerPrefs.GetFloat(PrefsKey(channel), 1f);

    /// <summary>Guarda el volumen (0 a 1) y lo aplica al mixer.</summary>
    public static void Set(AudioMixer mixer, Channel channel, float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefsKey(channel), value);
        PlayerPrefs.Save();
        ApplyChannel(mixer, channel, value);
    }

    /// <summary>Aplica al mixer los volúmenes guardados de los dos canales.</summary>
    public static void ApplySaved(AudioMixer mixer)
    {
        ApplyChannel(mixer, Channel.Music, Get(Channel.Music));
        ApplyChannel(mixer, Channel.SFX, Get(Channel.SFX));
    }

    private static void ApplyChannel(AudioMixer mixer, Channel channel, float value)
    {
        if (mixer == null) return;

        // El oído no es lineal: el slider se pasa a decibelios para que la mitad suene a "la mitad"
        float db = value > 0.0001f ? Mathf.Log10(value) * 20f : MinDb;
        if (!mixer.SetFloat(ParameterName(channel), db))
            Debug.LogWarning($"[VolumeSettings] El mixer {mixer.name} no tiene expuesto el parámetro {ParameterName(channel)}.");
    }

    private static string PrefsKey(Channel channel) => channel == Channel.Music ? "MusicVolume" : "SFXVolume";

    private static string ParameterName(Channel channel) => channel == Channel.Music ? "MusicVolume" : "SFXVolume";
}
