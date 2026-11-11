using UnityEngine;

/// <summary>
/// Las cartas que salen disparadas del Lanzacartas. Son SOLO visuales: el daño lo sigue haciendo el raycast
/// de WeaponBase en el mismo frame del disparo. La carta sale del MuzzlePoint, viaja girando como un frisbee
/// hasta el punto de impacto (el mismo que el trazador) y desaparece al llegar.
///
/// Por qué visual y no un proyectil con física: el raycast ya acierta exactamente donde apuntas (sale de la
/// cámara), y eso es lo que hace que un arma se sienta precisa y "arcade". Un proyectil físico tendría
/// caída, tiempo de vuelo que hay que anticipar y choques raros con la horda. Aquí la carta va lo bastante
/// rápida para que no se note que el golpe llega un pelín antes que ella.
///
/// Pool: al empezar se crean Pool Size cartas y se reutilizan siempre (nada de Instantiate/Destroy al
/// disparar). Si están todas en el aire, se recicla la más antigua. Todo el movimiento lo hace este script
/// en un solo Update con arrays, sin un script por carta.
///
/// Las cartas viven en un objeto propio en la raíz de la escena, NO dentro del arma: el arma está bajo el
/// WeaponHolder escalado a 0,1 y se mueve con la cámara, y una carta en vuelo tiene que ir por el mundo.
/// Va en la raíz del prefab del arma (junto al HitscanWeapon).
/// </summary>
public class FlyingCardLauncher : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Arma que avisa de cada disparo. Vacío = la busca en este objeto o en sus padres.")]
    [SerializeField] private WeaponBase weapon;
    [Tooltip("Modelo de la carta que sale volando (el mismo FBX que las del cargador sirve).")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Pool")]
    [Tooltip("Cartas que pueden estar en el aire a la vez. Con 4 disparos por segundo, 6 sobra.")]
    [Min(1)]
    [SerializeField] private int poolSize = 6;

    [Header("Vuelo")]
    [Tooltip("Velocidad en metros por segundo. 50 = se ve volar y llega a 10 m en 0,2 s. Más lenta se nota que el daño llega antes que la carta.")]
    [Min(1f)]
    [SerializeField] private float speedMetersPerSecond = 50f;
    [Tooltip("Vueltas de la carta sobre sí misma, en grados por segundo. 1440 = 4 vueltas por segundo.")]
    [SerializeField] private float spinDegreesPerSecond = 1440f;
    [Tooltip("Inclinación de la carta respecto al horizonte, en grados. 0 = plana como un frisbee; 20 = un poco ladeada, queda más dinámica.")]
    [Range(-90f, 90f)]
    [SerializeField] private float tiltDegrees = 15f;
    [Tooltip("Tamaño de la carta en vuelo (escala en el mundo). Ajústalo a ojo: la del cargador está escalada por el WeaponHolder y esta no.")]
    [SerializeField] private Vector3 flyingScale = Vector3.one;
    [Tooltip("Corrige la orientación del modelo si viene girado del programa 3D. La carta tiene que quedar plana con la cara mirando hacia arriba (eje Y).")]
    [SerializeField] private Vector3 modelRotationOffset;

    private Transform poolRoot;
    private Transform[] cards;
    private Vector3[] starts;
    private Vector3[] directions;
    private float[] distances;
    private float[] travelled;
    private float[] spinAngles;
    private bool[] inFlight;
    private int nextCard;

    private void Awake()
    {
        if (weapon == null)
            weapon = GetComponentInParent<WeaponBase>();

        BuildPool();
    }

    private void BuildPool()
    {
        cards = new Transform[poolSize];
        starts = new Vector3[poolSize];
        directions = new Vector3[poolSize];
        distances = new float[poolSize];
        travelled = new float[poolSize];
        spinAngles = new float[poolSize];
        inFlight = new bool[poolSize];

        if (cardPrefab == null) return;

        poolRoot = new GameObject($"Cartas voladoras ({name})").transform;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject card = Instantiate(cardPrefab, poolRoot);
            card.name = $"Carta voladora {i + 1}";
            card.transform.localScale = flyingScale;
            CardMagazineVisual.MakeVisualOnly(card);
            card.SetActive(false);
            cards[i] = card.transform;
        }
    }

    private void OnEnable()
    {
        if (weapon != null) weapon.OnShotPath += Launch;
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.OnShotPath -= Launch;

        // Si guardas el arma, las cartas en el aire se quedarían congeladas (este Update ya no corre).
        for (int i = 0; i < poolSize; i++)
            Land(i);
    }

    private void OnDestroy()
    {
        // El WeaponManager destruye el arma al cambiarla: con ella se van sus cartas.
        if (poolRoot != null)
            Destroy(poolRoot.gameObject);
    }

    private void Launch(Vector3 from, Vector3 to)
    {
        if (cardPrefab == null) return;

        int i = nextCard;
        nextCard = (nextCard + 1) % poolSize; // en orden: si están todas en el aire, se recicla la más antigua

        Vector3 path = to - from;
        float distance = path.magnitude;
        if (distance < 0.01f) return; // pegado a la pared: no hay vuelo que enseñar

        starts[i] = from;
        directions[i] = path / distance;
        distances[i] = distance;
        travelled[i] = 0f;
        spinAngles[i] = Random.Range(0f, 360f); // cada carta empieza girada distinto: no parecen clones
        inFlight[i] = true;

        cards[i].gameObject.SetActive(true);
        PlaceCard(i);
    }

    private void Update()
    {
        float step = speedMetersPerSecond * Time.deltaTime;
        float spin = spinDegreesPerSecond * Time.deltaTime;

        for (int i = 0; i < poolSize; i++)
        {
            if (!inFlight[i]) continue;

            travelled[i] += step;
            spinAngles[i] += spin;

            if (travelled[i] >= distances[i])
            {
                Land(i);
                continue;
            }

            PlaceCard(i);
        }
    }

    /// <summary>
    /// Posición en la línea del disparo y rotación: mira hacia donde va, ladeada Tilt Degrees y girando
    /// sobre su propio eje vertical (como un frisbee).
    /// </summary>
    private void PlaceCard(int i)
    {
        Quaternion facing = Quaternion.LookRotation(directions[i], Vector3.up);
        Quaternion rotation = facing
            * Quaternion.Euler(0f, 0f, tiltDegrees)
            * Quaternion.Euler(0f, spinAngles[i], 0f)
            * Quaternion.Euler(modelRotationOffset);

        cards[i].SetPositionAndRotation(starts[i] + directions[i] * travelled[i], rotation);
    }

    private void Land(int i)
    {
        inFlight[i] = false;
        if (cards != null && cards[i] != null)
            cards[i].gameObject.SetActive(false);
    }
}
