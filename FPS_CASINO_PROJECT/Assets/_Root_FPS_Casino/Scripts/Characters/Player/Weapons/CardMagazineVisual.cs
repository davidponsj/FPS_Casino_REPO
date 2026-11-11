using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Las cartas que se ven DENTRO del Lanzacartas (en el cargador de cristal). Cada disparo esconde una carta
/// y la recarga las vuelve a poner, una a una a la vez que avanza la recarga.
///
/// Por qué no escucha eventos de disparo/recarga: cada frame mira la munición REAL del arma (CurrentAmmo,
/// ReloadProgress01) y enseña las cartas que tocan. Así nunca se puede desincronizar (equipar el arma a
/// mitad, la botella de munición infinita, recargas canceladas...): lo que ves es siempre lo que tienes.
/// Solo toca los GameObjects cuando el número cambia, así que no cuesta nada el resto de frames.
///
/// Dos formas de montar las cartas:
/// - Automática: rellena Card Prefab y Stack Origin, y al empezar se crea una carta por bala del cargador,
///   apiladas desde Stack Origin con un paso de Stack Step entre cada una.
/// - A mano: coloca tú las cartas dentro del prefab del arma y arrástralas a Card Slots.
///
/// Regla del arma: estas cartas son parte del arma, así que se les quitan colliders y sombras al crearlas.
/// Va en la raíz del prefab del arma (junto al HitscanWeapon).
/// </summary>
public class CardMagazineVisual : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arma de la que se lee la munición. Vacío = la busca en este objeto o en sus padres.")]
    [SerializeField] private WeaponBase weapon;

    [Header("Cartas colocadas a mano (opcional)")]
    [Tooltip("Cartas ya colocadas dentro del arma. El ÚLTIMO de la lista es el primero que sale al disparar. Si lo dejas vacío, se generan solas con los campos de abajo.")]
    [SerializeField] private Transform[] cardSlots;

    [Header("Generación automática")]
    [Tooltip("Modelo de la carta (el FBX o un prefab con él). Se crea una copia por cada bala del cargador.")]
    [SerializeField] private GameObject cardPrefab;
    [Tooltip("Hueco vacío dentro del arma donde empieza la pila de cartas (la del fondo). Su rotación y escala son las de todas las cartas.")]
    [SerializeField] private Transform stackOrigin;
    [Tooltip("Separación entre una carta y la siguiente, en el espacio LOCAL de Stack Origin. Una baraja apilada: algo como (0, 0.02, 0).")]
    [SerializeField] private Vector3 stackStep = new Vector3(0f, 0.02f, 0f);

    [Header("Recarga")]
    [Tooltip("Marcado: las cartas reaparecen una a una mientras dura la recarga. Desmarcado: aparecen todas de golpe al terminar.")]
    [SerializeField] private bool refillDuringReload = true;

    private GameObject[] cards;
    private int visibleCount = -1; // -1 = aún sin aplicar, para forzar la primera actualización

    private void Awake()
    {
        if (weapon == null)
            weapon = GetComponentInParent<WeaponBase>();

        BuildCards();
    }

    private void BuildCards()
    {
        if (cardSlots != null && cardSlots.Length > 0)
        {
            cards = new GameObject[cardSlots.Length];
            for (int i = 0; i < cardSlots.Length; i++)
            {
                cards[i] = cardSlots[i] != null ? cardSlots[i].gameObject : null;
                if (cards[i] != null) MakeVisualOnly(cards[i]);
            }
            return;
        }

        if (cardPrefab == null || stackOrigin == null || weapon == null)
        {
            cards = new GameObject[0];
            return;
        }

        // Una carta por bala. Se crean una sola vez al cargar el arma: luego solo se encienden y apagan.
        cards = new GameObject[weapon.MagazineSize];
        for (int i = 0; i < cards.Length; i++)
        {
            GameObject card = Instantiate(cardPrefab, stackOrigin);
            card.name = $"Carta {i + 1}";
            card.transform.localPosition = stackStep * i;
            card.transform.localRotation = Quaternion.identity;
            MakeVisualOnly(card);
            cards[i] = card;
        }
    }

    private void Update()
    {
        if (weapon == null || cards.Length == 0) return;

        int target = CardsForAmmo(GetDisplayedAmmo());
        if (target == visibleCount) return;

        visibleCount = target;
        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null)
                cards[i].SetActive(i < visibleCount);
    }

    /// <summary>
    /// Balas que hay que enseñar. Durante la recarga, el cargador real no sube hasta el final (así funciona
    /// WeaponBase), así que aquí se adelanta visualmente según el progreso de la recarga.
    /// </summary>
    private int GetDisplayedAmmo()
    {
        int ammo = weapon.CurrentAmmo;
        if (!weapon.IsReloading || !refillDuringReload) return ammo;

        // Las que acabarán dentro al terminar: hasta llenar el cargador, o lo que quede en la reserva.
        int finalAmmo = Mathf.Min(weapon.MagazineSize, ammo + weapon.ReserveAmmo);
        return ammo + Mathf.FloorToInt((finalAmmo - ammo) * weapon.ReloadProgress01);
    }

    /// <summary>Si pones menos cartas que balas, cada carta vale por varias balas (y se redondea hacia arriba: la última carta se va con la última bala).</summary>
    private int CardsForAmmo(int ammo)
    {
        int magazine = Mathf.Max(1, weapon.MagazineSize);
        if (cards.Length == magazine) return Mathf.Clamp(ammo, 0, cards.Length);
        return Mathf.Clamp(Mathf.CeilToInt(ammo * cards.Length / (float)magazine), 0, cards.Length);
    }

    /// <summary>
    /// Deja la carta como un adorno: sin colliders (no debe chocar con nada ni con el raycast del arma)
    /// y sin sombras (el truco anti-clipping del arma: proyectaría sombras gigantes y raras).
    /// </summary>
    public static void MakeVisualOnly(GameObject card)
    {
        foreach (Collider col in card.GetComponentsInChildren<Collider>(true))
            Destroy(col);

        foreach (Renderer rend in card.GetComponentsInChildren<Renderer>(true))
        {
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }
}
