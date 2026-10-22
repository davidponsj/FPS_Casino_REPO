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
    [SerializeField] private WeaponBase weaponPrefab;
    [SerializeField] private string weaponDisplayName = "Arma";

    public string InteractionPrompt => $"Pick {weaponDisplayName} [E]";

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
}