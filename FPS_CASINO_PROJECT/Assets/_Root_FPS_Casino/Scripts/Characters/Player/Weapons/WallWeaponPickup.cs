using System.Globalization;
using UnityEngine;

/// <summary>
/// Punto fijo en la pared con un arma que se compra con puntos, como el wall-buy de COD.
/// Al comprarla, el WeaponManager del jugador equipa una instancia nueva de weaponPrefab — este
/// objeto (el modelo en la pared) nunca se toca, así que puedes volver a comprarla las veces que quieras.
/// Si ya llevas esta arma, la pared rellena su munición por una parte del precio (la mitad, como en COD);
/// si la munición ya está llena, no cobra nada.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallWeaponPickup : MonoBehaviour, IInteractable
{
    [Header("Arma")]
    [Tooltip("Prefab del arma que se equipa al cogerla (PF_Lanzacartas...). El modelo de la pared es solo decorado.")]
    [SerializeField] private WeaponBase weaponPrefab;
    [Tooltip("Nombre que sale en el prompt: \"Comprar <nombre> [E] · 500\".")]
    [SerializeField] private string weaponDisplayName = "Arma";

    [Header("Precio")]
    [Tooltip("Puntos que cuesta comprarla. 0 = gratis. 500 = Lanzacartas, 1500 = escopeta (como en CoD).")]
    [Min(0)]
    [SerializeField] private int price = 500;
    [Tooltip("Parte del precio que cuesta rellenar la munición si ya llevas esta arma. 0.5 = la mitad, como en CoD.")]
    [Range(0f, 1f)]
    [SerializeField] private float ammoPriceFraction = 0.5f;

    [Header("Depuración")]
    [Tooltip("Muestra en la vista Scene, sobre el arma de pared, qué arma da, su precio y si le falta el prefab.")]
    [SerializeField] private bool showDebug;

    // Miles con punto ("1.500") sea cual sea el idioma del sistema.
    private static readonly NumberFormatInfo PointsFormat = new NumberFormatInfo { NumberGroupSeparator = "." };

    private PlayerPoints points;
    private WeaponManager manager;
    private bool playerSearched;

    /// <summary>Puntos que cuesta rellenar la munición: la parte "fraction" del precio, redondeada.</summary>
    public static int AmmoPrice(int price, float fraction) => Mathf.RoundToInt(price * fraction);

    public string InteractionPrompt
    {
        get
        {
            if (!FindPlayer()) return $"Coger {weaponDisplayName} [E]";

            int cost = GetCost(out bool owned);
            if (!owned && cost == 0) return $"Coger {weaponDisplayName} [E]";
            if (owned && manager.CurrentWeapon.IsAmmoFull) return "Munición llena";

            string prompt = owned
                ? $"Munición {weaponDisplayName} [E] · {Format(cost)}"
                : $"Comprar {weaponDisplayName} [E] · {Format(cost)}";
            if (points.CurrentPoints < cost)
                prompt += $" · te faltan {Format(cost - points.CurrentPoints)}";
            return prompt;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (weaponPrefab == null || !FindPlayer()) return;

        int cost = GetCost(out bool owned);
        if (owned && manager.CurrentWeapon.IsAmmoFull) return;
        if (!points.TrySpend(cost)) return;

        if (owned) manager.CurrentWeapon.RefillAmmo();
        else manager.EquipWeapon(weaponPrefab);
    }

    /// <summary>Lo que cuesta ahora: el arma entera, o solo la munición si ya llevas esta arma.</summary>
    private int GetCost(out bool owned)
    {
        owned = manager.CurrentWeapon != null && manager.CurrentWeaponPrefab == weaponPrefab;
        return owned ? AmmoPrice(price, ammoPriceFraction) : price;
    }

    /// <summary>Busca al Player por tag la primera vez y guarda sus puntos y su WeaponManager.</summary>
    private bool FindPlayer()
    {
        if (!playerSearched)
        {
            playerSearched = true;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                points = player.GetComponentInChildren<PlayerPoints>();
                manager = player.GetComponentInChildren<WeaponManager>();
            }
            if (points == null || manager == null)
                Debug.LogWarning($"{name}: no encuentro un objeto con tag Player que tenga PlayerPoints y WeaponManager.", this);
        }
        return points != null && manager != null;
    }

    private static string Format(int value) => value.ToString("N0", PointsFormat);

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
        DebugLabels.At(transform.position + Vector3.up * 0.5f, 0,
            $"{weaponDisplayName} → {weapon} · {price} pts (munición {AmmoPrice(price, ammoPriceFraction)} pts)", DebugLabels.Weapon);
    }
#endif
}
