using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

/// <summary>
/// Tests de PlayerControls.inputactions: las acciones de las botellas existen y llevan su tecla.
/// </summary>
public class PlayerControlsTests
{
    private const string AssetPath = "Assets/_Root_FPS_Casino/Scripts/Characters/Player/PlayerControls.inputactions";

    [TestCase("Drink", "<Keyboard>/f")]
    [TestCase("Throw", "<Keyboard>/q")]
    public void AccionDeBotella_TieneSuTecla(string actionName, string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
        Assert.IsNotNull(asset, "No se encuentra PlayerControls.inputactions");

        InputAction action = asset.FindAction("Player/" + actionName);
        Assert.IsNotNull(action, $"Falta la acción {actionName}");
        Assert.AreEqual(path, action.bindings[0].path);
    }
}
