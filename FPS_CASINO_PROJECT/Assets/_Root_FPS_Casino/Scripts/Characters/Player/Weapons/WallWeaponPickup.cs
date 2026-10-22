using UnityEngine;

/// <summary>
/// Punto fijo en la pared con un arma. Al interactuar, el WeaponManager del jugador equipa
/// una instancia nueva de weaponPrefab — este objeto (el modelo en la pared) nunca se toca,
/// así que puedes volver a coger la misma arma las veces que quieras, como el wall-buy de COD.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallWeaponPickup : MonoBehaviour, IInteractable
{
    [Header("Arma")]
    [Tooltip("Prefab del arma que se equipa al cogerla (PF_Lanzacartas...). El modelo de la pared es solo decorado.")]
    [SerializeField] private WeaponBase weaponPrefab;
    [Tooltip("Nombre que sale en el prompt: \"Coger <nombre> [E]\".")]
    [SerializeField] private string weaponDisplayName = "Arma";

    [Header("Depuración")]
    [Tooltip("Muestra en la vista Scene, sobre el arma de pared, qué arma da y si le falta el prefab.")]
    [SerializeField] private bool showDebug;

    public string InteractionPrompt => $"Coger {weaponDisplayName} [E]";

    public void Interact(GameObject interactor)
    {
        if (weaponPrefab == null) return;

        WeaponManager manager = interactor.GetComponentInParent<WeaponManager>();
        if (manager == null) manager = interactor.GetComponent<WeaponManager>();

        manager?.EquipWeapon(weaponPrefab);
    }

    private void Reset()
    {
        // El Collider tiene que ser un trigger: así el jugador puede caminar a través/cerca
        // del arma de pared sin chocar, y el raycast de interacción sigue detectándolo
        // porque PlayerInteractor usa QueryTriggerInteraction.Collide.
        GetComponent<Collider>().isTrigger = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        string weapon = weaponPrefab != null ? weaponPrefab.name : "FALTA EL PREFAB";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"{weaponDisplayName} → {weapon} (gratis)");
    }
#endif
}