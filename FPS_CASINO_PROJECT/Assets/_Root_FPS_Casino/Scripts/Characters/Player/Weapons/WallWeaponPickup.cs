using UnityEngine;

/// <summary>
/// Punto fijo en la pared con un arma. Al interactuar pagas weaponCost y el WeaponManager del jugador
/// equipa una instancia nueva de weaponPrefab — este objeto (el modelo en la pared) nunca se toca,
/// así que puedes volver a comprar la misma arma las veces que quieras, como el wall-buy de COD.
/// Si ya llevas esa arma, solo pagas la munición (ammoCostFraction del precio) y te la rellena.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallWeaponPickup : MonoBehaviour, IInteractable
{
    [Header("Arma")]
    [Tooltip("Prefab del arma que se equipa al cogerla (PF_Lanzacartas...). El modelo de la pared es solo decorado.")]
    [SerializeField] private WeaponBase weaponPrefab;
    [Tooltip("Nombre que sale en el prompt: \"Buy <nombre> [E]\".")]
    [SerializeField] private string weaponDisplayName = "Arma";

    [Header("Precio")]
    [Tooltip("Puntos que cuesta comprar el arma. 500 = arma barata, 2000 = arma de final; 0 = gratis.")]
    [Min(0)]
    [SerializeField] private int weaponCost = 500;
    [Tooltip("Parte del precio que cuesta rellenar la munición si ya llevas esta arma. 0.5 = la mitad, como en CoD.")]
    [Range(0f, 1f)]
    [SerializeField] private float ammoCostFraction = 0.5f;

    [Header("Depuración")]
    [Tooltip("Muestra en la vista Scene, sobre el arma de pared, qué arma da, su precio y si le falta el prefab.")]
    [SerializeField] private bool showDebug;

    private WeaponManager playerWeapons; // para el prompt, que no recibe al jugador

    /// <summary>Nombre del arma (lo usa WallWeaponDisplay para la placa).</summary>
    public string DisplayName => weaponDisplayName;

    public int AmmoCost => Mathf.RoundToInt(weaponCost * ammoCostFraction);

    public string InteractionPrompt
    {
        get
        {
            if (playerWeapons == null) playerWeapons = FindFirstObjectByType<WeaponManager>();
            return IsOwnedBy(playerWeapons)
                ? $"Buy {weaponDisplayName} ammo ({AmmoCost} pts) [E]"
                : $"Buy {weaponDisplayName} ({weaponCost} pts) [E]";
        }
    }

    private bool IsOwnedBy(WeaponManager manager) =>
        manager != null && weaponPrefab != null && manager.EquippedPrefab == weaponPrefab;

    public void Interact(GameObject interactor)
    {
        if (weaponPrefab == null) return;

        WeaponManager manager = interactor.GetComponentInParent<WeaponManager>();
        if (manager == null) manager = interactor.GetComponent<WeaponManager>();
        if (manager == null) return;

        int price = IsOwnedBy(manager) ? AmmoCost : weaponCost;
        PlayerPoints points = interactor.GetComponentInParent<PlayerPoints>();
        if (points == null)
            Debug.LogWarning("[Arma de pared] El jugador no tiene PlayerPoints: el arma será gratis.", this);
        else if (price > 0 && !points.TrySpend(price))
            return;

        // Equipar de nuevo la misma arma la deja con la munición llena: así se rellena.
        manager.EquipWeapon(weaponPrefab);
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
        DebugLabels.At(transform.position + Vector3.up * 0.5f, 0, $"{weaponDisplayName} → {weapon} · {weaponCost} pts (munición {AmmoCost} pts)", DebugLabels.Weapon);
    }
#endif
}
