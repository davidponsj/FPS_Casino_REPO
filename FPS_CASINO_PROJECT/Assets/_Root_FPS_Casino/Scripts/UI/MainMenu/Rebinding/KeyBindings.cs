using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Teclas que el jugador cambia en el panel de Controles del ATM. Se guardan en PlayerPrefs y se
/// aplican solas a cualquier PlayerInput al cargar cada escena, así que el Player no necesita
/// ningún componente extra. Lo usa RebindKey.
/// </summary>
public static class KeyBindings
{
    private const string PrefsKey = "KeyBindings";

    private static readonly HashSet<InputActionAsset> loadedAssets = new HashSet<InputActionAsset>();
    private static InputActionRebindingExtensions.RebindingOperation operation;
    private static int blockUntilFrame = -1;

    /// <summary>True mientras se espera a que el jugador pulse la tecla nueva.</summary>
    public static bool IsListening => operation != null;

    /// <summary>El resto del menú debe ignorar teclado y ratón: se está escuchando o se acaba de terminar.</summary>
    public static bool BlockMenuInput => IsListening || Time.frameCount <= blockUntilFrame;

    /// <summary>Alguna tecla ha cambiado (para refrescar los iconos).</summary>
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loadedAssets.Clear();
        operation = null;
        blockUntilFrame = -1;
        OnChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyToPlayers();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToPlayers();

    // Aplica las teclas guardadas al jugador de la escena (el PlayerInput de PF_Player)
    private static void ApplyToPlayers()
    {
        foreach (var playerInput in UnityEngine.Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
        {
            if (playerInput.actions != null)
                Load(playerInput.actions);
        }
    }

    /// <summary>Carga las teclas guardadas en este asset, solo la primera vez.</summary>
    public static void EnsureLoaded(InputActionAsset asset)
    {
        if (asset != null && !loadedAssets.Contains(asset))
            Load(asset);
    }

    public static void Load(InputActionAsset asset)
    {
        asset.RemoveAllBindingOverrides();

        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
            asset.LoadBindingOverridesFromJson(json);

        loadedAssets.Add(asset);
    }

    /// <summary>Vuelve a las teclas de PlayerControls y borra lo guardado.</summary>
    public static void ResetToDefaults(InputActionAsset asset)
    {
        asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>Índice de la tecla de una acción. compositePart es para Move (up, down, left, right).</summary>
    public static int FindBindingIndex(InputAction action, string compositePart)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (string.IsNullOrEmpty(compositePart))
            {
                if (!binding.isComposite && !binding.isPartOfComposite) return i;
            }
            else if (binding.isPartOfComposite &&
                     string.Equals(binding.name, compositePart, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Espera a que el jugador pulse una tecla o botón del ratón. Esc cancela.</summary>
    public static void StartRebind(InputAction action, int bindingIndex, Action onFinished = null)
    {
        if (IsListening || action == null || bindingIndex < 0) return;

        string oldPath = action.bindings[bindingIndex].effectivePath;
        bool wasEnabled = action.enabled;
        action.Disable();

        operation = action.PerformInteractiveRebinding(bindingIndex)
            .WithExpectedControlType("Button")
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Pointer>/press")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op =>
            {
                SwapDuplicate(action, bindingIndex, oldPath);
                Save(action.actionMap.asset);
                Finish(action, wasEnabled, onFinished);
            })
            .OnCancel(op => Finish(action, wasEnabled, onFinished))
            .Start();
    }

    public static void CancelRebind()
    {
        operation?.Cancel();
    }

    private static void Finish(InputAction action, bool wasEnabled, Action onFinished)
    {
        operation?.Dispose();
        operation = null;
        blockUntilFrame = Time.frameCount + 1;

        if (wasEnabled) action.Enable();

        onFinished?.Invoke();
        OnChanged?.Invoke();
    }

    // Si la tecla nueva ya la usaba otra acción, se intercambian: esa se queda con la tecla vieja
    private static void SwapDuplicate(InputAction action, int bindingIndex, string oldPath)
    {
        string newPath = action.bindings[bindingIndex].effectivePath;
        if (string.Equals(newPath, oldPath, StringComparison.OrdinalIgnoreCase)) return;

        foreach (var other in action.actionMap.actions)
        {
            for (int i = 0; i < other.bindings.Count; i++)
            {
                if (other == action && i == bindingIndex) continue;

                var binding = other.bindings[i];
                if (binding.isComposite) continue;

                if (string.Equals(binding.effectivePath, newPath, StringComparison.OrdinalIgnoreCase))
                    other.ApplyBindingOverride(i, oldPath);
            }
        }
    }

    private static void Save(InputActionAsset asset)
    {
        PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }
}