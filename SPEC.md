# Spec: Limpieza y reorganización del proyecto (FPS Casino)

> Sustituye a la spec de la IA Hunter (terminada; sigue en git, commit 3f74066).

## Objetivo
Dejar el proyecto **limpio, coherente y fácil de entender para alguien de fuera**, sin cambiar cómo se juega:
- Scripts más simples: quitar código muerto, juntar scripts duplicados y arreglar los bugs que salgan por el camino.
- Una sola convención de nombres para todo (scripts, prefabs, assets).
- Cada archivo en la carpeta de su sistema, **dentro de la estructura de carpetas que ya hay** (la del profesor).

Usuario: el equipo (5 personas) y cualquiera que abra el repo por primera vez.
Ventana: los compañeros ya han hecho su PR y **no tocan nada** hasta que David avise → se pueden tocar escenas, prefabs y ProjectSettings sin conflictos.

**No es objetivo:** funcionalidades nuevas (vida y ataque de los enemigos van después), cambiar el game feel ni los valores de diseño.

## Decisiones tomadas
1. **Carpetas:** la estructura actual es la que pide el profesor. **No se borra ninguna carpeta, aunque esté vacía, y no se cambia la estructura** (`Design/Scenes`, `Design/Testing`, `Animation`, `Audio`, `Managers`...). Solo se mueven archivos a la carpeta que les corresponde, aprovechando las que ya existen (p. ej. `Scripts/Managers/` para `RoundManager`). Lo único que cambia en las carpetas es que **se corrigen las faltas de ortografía**: `ScripteableObjects` → `ScriptableObjects`, `PhisicMaterials` → `PhysicMaterials`, `Adressables` → `Addressables`, `Design/Lightning` → `Design/Lighting`, `Art/Sprites/Inputkeys` → `InputKeys`.
2. **Codificación:** cada `.cs` se queda con la suya (Windows-1252 o UTF-8). Al editar un archivo se conserva su codificación; si las tildes dan problemas en ese archivo, se quitan las tildes de sus comentarios.
3. **Idioma:** identificadores y nombres de archivo en **inglés**; comentarios, tooltips y textos del juego en **español**. Excepción: **Lanzacartas** se queda en español (es la pistola inicial). Solo se unifica cómo se escribe: `Lanzacartas` (hoy conviven `Lanzacartas` y `LanzaCartas`).
4. **Enemigos:** solo habrá un tipo → `EnemyBase` y `HunterAI` se juntan en **`EnemyAI`**. De momento siguen **muriendo de un golpe** (`EnemyHealth` no cambia de comportamiento).
5. **Al juntar scripts, el resultado lleva un nombre nuevo que diga lo que hace** (`EnemyAI`, `HitscanWeapon`). Además se renombran los scripts cuyo nombre no se entiende sin abrirlos (tabla "Scripts que cambian").
6. **Todo lo de test se queda donde está y con su nombre:** `Prefabs/Characters/Test/`, `Prefabs/Environment/Props/Test/` (`PF_RouletteTest`, `PF_SlotMachineTest`, `PF_TicketTest`), `PF_BartenderTest`, `Art/3D/Materials/Test/` (incluidos los 3 `New Material`, que los usan escenas y prefabs) y `TestPointsWallet`.
7. **`TicketPickup`** se borra (el ticket ya no se recoge del suelo). Se le quita el componente a `PF_TicketTest`, que **se queda**: los 6 tickets lo usan como modelo del ticket que sale de la ruleta.
8. **Escenas de los compañeros** (`SCN_Ruben`, `SCN_Saez`, `SCN_Jordan`, `SCN_Pons`, `SCN_Hugo_Test`): no se tocan ni se borran. `TestPointsWallet` (lo usa `SCN_Ruben`) solo deja de implementar las interfaces que se borran.
9. `Pistol_Wall` y `PF_LanzaCartasWall` son armas distintas: se quedan las dos.
10. **Depuración visual en la Scene, no en la consola.** Los `Debug.Log` llenan la consola y tapan los errores de verdad. Por eso el estado de cada sistema se **dibuja encima del objeto**: textos con `Handles.Label` y formas con `Gizmos`/`Handles`, el mismo sistema que ya usan `EnemyBase` (etiqueta del estado), `AttackSlotManager`, `CrowdSolver` y `RoundManager`.
    - Cada script relevante tiene un apartado **"Depuración"** en el Inspector con la casilla **"Mostrar depuración"** (`[SerializeField] private bool showDebug`), **apagada por defecto**. En los scripts que ya tienen `drawGizmos`, ese campo se renombra con `FormerlySerializedAs`.
    - Se ve en la vista Scene mientras juegas. En la vista Game también, si activas el botón **Gizmos**. Todo va dentro de `#if UNITY_EDITOR`: no llega a la build ni gasta nada en el juego.
    - Los textos llevan **el dato con su nombre**, en español y con unidades ("Ronda 3 · vivos 5/24 · en reserva 12 · próximo spawn 1,5 s"), para que alguien que no conoce el proyecto entienda qué pasa sin abrir el código.
    - **Consola:** solo `LogWarning`/`LogError` cuando falta una referencia o algo falla (siempre activos). Los `Debug.Log` sueltos que hay hoy se borran o pasan a ser texto en la Scene (B3).

    Qué dibuja cada script (con los nombres nuevos):

    | Script | Qué se ve en la Scene |
    |---|---|
    | `RoundManager` | Texto sobre el jugador: ronda, vivos/máx., en reserva (faltan por salir), segundos entre spawns, cuenta atrás del próximo spawn o del descanso entre rondas. Anillos de distancia mín./máx. de spawn y un marcador en los últimos puntos de spawn. |
    | `EnemyAI` | Etiqueta sobre la cabeza: estado actual, segundos en ese estado, distancia al jugador. Línea hasta su destino. (Ya existe la etiqueta del estado: se amplía.) |
    | `EnemyMovement` | Velocidad actual/base y multiplicador activo (p. ej. "charco ×0,5"). Camino del NavMesh. Marca roja si está atascado. |
    | `EnemyHealth` | Vida actual/máxima sobre el enemigo. |
    | `HordeAttackSlots` | Los puestos alrededor del jugador (libre / ocupado / reservado), la cola y el texto "atacando 3/4 · en cola 5". (Ya dibuja los anillos.) |
    | `HordeCollision` | Radio de separación de cada enemigo y del jugador. (Ya existe.) |
    | `PlayerController` | Texto junto al jugador: estado de movimiento (andando, sprint, slide, wallrun, en el aire), velocidad horizontal, momentum, coyote time restante. Flecha de la velocidad y la pared detectada en el wallrun. |
    | `PlayerHealth`, `PlayerPoints`, `PlayerBlackout`, `PlayerBuffs` | Un solo bloque de texto sobre el jugador: vida, puntos (actuales/total), borrachera y efecto activo con sus segundos. Cada script añade su línea si su casilla está activa. |
    | `PlayerInteractor` | El rayo de interacción (verde si toca algo con lo que interactuar) y el nombre del objeto. |
    | `WeaponBase` / `HitscanWeapon` | Cono de dispersión actual, cargador/reserva y "recargando 0,6 s". Los últimos impactos marcados unos segundos. |
    | `GrenadeProjectile` | Radio de explosión (ya existe) y la trayectoria. |
    | `WallWeaponPickup` | Arma y coste sobre la pared. |
    | `RouletteMachine` | Coste, "bloqueada: tienes ticket / botella" o "libre", último resultado. (Ya dibuja los segmentos.) |
    | `SlotMachine` | Apuesta elegida, último resultado y premio. |
    | `RouletteTicketDispenser`, `TicketInventory`, `Bartender`, `BottleHolder` | Qué ticket o botella hay ahora (o "vacío"). |
    | `BottlePuddle` | Radio del charco, segundos que le quedan y enemigos dentro. |

    Sin depuración: HUDs, menú, `MenuArrow`, `TicketData`, `BottleEffect`, interfaces y los efectos que solo se ven (`MuzzleFlash`, `BulletTracerEffect`, `BulletImpactEffect`, `BottleShatterEffect`, `LiquidWobble`, `SlotLeverAnimator`, `HoverOutline`). Sus datos ya se ven en el juego.
11. **Inspector entendible para alguien nuevo:** todos los campos que se ven en el Inspector:
    - Tienen un nombre en inglés que dice qué son y en qué unidad van (`reloadTime` → `reloadSeconds`, `cost` → `spinCost`).
    - Llevan un `[Tooltip]` en español con la unidad y un ejemplo ("Segundos que tarda en recargar. 1.4 = recarga normal").
    - Están agrupados con `[Header]` en español ("Munición", "Daño", "Depuración"...).
    - Si tienen límites lógicos, llevan `[Min]` o `[Range]`.

    - Se revisan **todos** los campos, no solo los que tienen mal nombre. La prueba: alguien que acaba de entrar en el proyecto tiene que poder ajustar el valor sin abrir el código. Ejemplos de lo que hay hoy: `RouletteMachine.cost` → `spinCost`, `minDuration` → `minSpinSeconds`; `RoundManager.minSpawnDelay`, `secondsBetweenRounds` y `minSpawnDistance` no tienen tooltip; `LiquidWobble.inertia` no dice qué hace un valor alto.
    - En cada tarea de Inspector se apunta en `tasks/plan.md` la lista `campo viejo → campo nuevo` de cada script, para que David la revise antes del commit.

    Al renombrar un campo se pone `[FormerlySerializedAs("nombreViejo")]`: así no se pierde ningún valor ya ajustado en prefabs y escenas, tampoco en las de los compañeros. Esos atributos se quedan en el código, porque quitarlos obligaría a re-guardar las escenas de los compañeros.
12. Sin namespaces ni .asmdef. `ExternalPacks/` no se toca. Los valores del Inspector no cambian: al renombrar un campo se usa `[FormerlySerializedAs]`.

## Tech stack
Unity **6000.3.24f1**, URP 17.3.0, Input System 1.20.0, AI Navigation 2.0.14, Test Framework 1.6.0. Sin paquetes nuevos.

## Comandos
```
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe"
PROJ="C:/Users/David/Documents/.David/Unity/UnityRepositories/FPS_Casino_REPO/FPS_CASINO_PROJECT"

# Compilar (Unity CERRADO): no debe salir ningún "error CS"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJ" -logFile - | grep -E "error CS|Compilation"

# Tests EditMode
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -runTests -testPlatform EditMode -testResults "$PROJ/../tasks/test-results.xml" -logFile -

# Referencias rotas: GUIDs usados en escenas/prefabs/assets que no existen en ningún .meta (debe salir vacío)
# (el script se escribe en el bloque `tooling`)

git branch --show-current   # debe ser dev-davidmallorca_V2
```

## Problemas encontrados (leídos los 60 scripts)

### Bugs
| # | Dónde | Problema |
|---|---|---|
| B1 | `IMovementModifiable` / `EnemyMovement` | Ningún enemigo implementa `IMovementModifiable`: **los charcos no ralentizan a nadie**. |
| B3 | `PlayerInteractor` y otros | `Debug.Log` cada vez que cambia el texto del prompt (llena la consola). Los `Debug.Log` sueltos se borran o pasan a ser texto en la Scene (decisión 10). |
| B4 | `SlotMachine` | Consulta los puntos en cada frame (`Update`) para pintar su texto, en vez de escuchar `PlayerPoints.OnPointsChanged`. |
| B5 | `QuitGameTarget` | `Application.Quit()` está comentado: el botón "Salir" del menú no hace nada. |

### Código duplicado o sobrante
| # | Qué | Propuesta |
|---|---|---|
| D1 | `PistolWeapon` y `SniperWeapon` son idénticos; `TommyGunWeapon` y `ShotgunWeapon` solo cambian "automática" y "nº de perdigones" | Un único **`HitscanWeapon`** con `automatic` y `pelletsPerShot`. 4 scripts → 1. |
| D2 | `IPointsService` + `IPointsReceiver` + campo `pointsSource` (MonoBehaviour sin tipo) en ruleta y tragaperras | Las máquinas cogen el `PlayerPoints` del jugador que interactúa (como ya hace la tragaperras con `PlayerBuffs`). Se borran las 2 interfaces. |
| D3 | `BottleEffectLibrary` (tabla ticket → efecto) + `TicketData.bottleId` (sin usar) | `TicketData` lleva directamente su `BottleEffect`. Se borran la tabla y `SO_BottleLibrary`. |
| D4 | Bloqueo de la ruleta repartido entre `RouletteMachine`, `TicketInventory.Lock/Unlock`, `Bartender` y `BottleHolder` (el barman desbloquea y vuelve a bloquear en dos líneas) | Una sola regla legible: "la ruleta está bloqueada mientras tengas ticket o botella". `TicketInventory` guarda un solo ticket (hoy es una lista que nunca pasa de 1). |
| D5 | `TicketPickup` (el ticket ya no se recoge del suelo) | Se borra (decisión 7). |
| D6 | Elección aleatoria por pesos copiada en `RouletteMachine` y `SlotMachine` | Un helper `WeightedRandom.Pick` en `Utilities/`. |
| D7 | `AmmoUI`, `ReloadRadialUI` y `DynamicCrosshair` reciben el arma con `SetWeapon` desde `WeaponManager`, que tiene que conocerlos a los tres | `WeaponManager.OnWeaponChanged`; cada UI se suscribe sola. |
| D8 | Miembros sin usar: `WeaponBase.audioSource` (+`[RequireComponent(AudioSource)]`), `RefillAmmo`, `OnDryFire`, `WeaponManager.HasWeaponEquipped`, `LiquidWobble.FillAmount`, `BottlePuddle.Ticket`, `PlayerHealth.DamageTakenMultiplier`, parte de la API de `EnemyMovement` (`Stuck`, `BaseSpeed`, `CurrentSpeed`...) | Borrar lo que no use nadie. Se comprueba con grep antes de cada borrado. |
| D9 | Comentarios que hablan de cosas que ya no existen ("las cinco IAs", `PatrollerAI`, "las máquinas de Álvaro", "cuando montéis cambio de arma") | Corregirlos. |
| D10 | `BottleHolder` lee F/Q/T directamente del teclado; el resto del jugador usa acciones del Player Input | Acciones `Drink` y `Throw` en `PlayerControls.inputactions`. La tecla T de prueba pasa a ser solo `[ContextMenu]`. |
| D11 | `EnemyBase`: base abstracta para un solo enemigo | `EnemyBase` + `HunterAI` → **`EnemyAI`** (decisión 4). `HordeAttackSlots` pasa a usar `EnemyAI` y `RoundManager` busca al jugador por su cuenta. |

### Scripts que cambian de nombre, se juntan o se borran
| Ahora | Después | Por qué |
|---|---|---|
| `EnemyBase` + `HunterAI` | **`EnemyAI`** | Se juntan (D11). Es "la IA del enemigo". |
| `PistolWeapon` + `SniperWeapon` + `TommyGunWeapon` + `ShotgunWeapon` | **`HitscanWeapon`** | Se juntan (D1). Todas son armas de bala instantánea (hitscan); cambian solo sus valores. |
| `AttackSlotManager` | **`HordeAttackSlots`** | Reparte los sitios alrededor del jugador y decide cuántos enemigos pueden pegarse a la vez. |
| `CrowdSolver` | **`HordeCollision`** | Hace que los enemigos no se atraviesen entre ellos ni empujen al jugador. |
| `TicketSpawner` | **`RouletteTicketDispenser`** | Saca el ticket ganador por la ruleta y deja que lo cojas desde ella. |
| `MenuTriangle` | **`MenuArrow`** | Es la flecha que marca la opción elegida en la pantalla del cajero. |
| `AmmoUI`, `HealthBarUI`, `BlackoutBarUI`, `ReloadRadialUI` | **`AmmoHUD`, `HealthBarHUD`, `BlackoutBarHUD`, `ReloadHUD`** | Un solo sufijo para todo lo que se pinta en pantalla (ya existen `PointsHUD`, `RoundHUD`, `TicketHUD` y `BottleEffectHUD`). |
| `IPointsService`, `IPointsReceiver` | *(se borran)* | D2. |
| `BottleEffectLibrary` | *(se borra)* | D3. |
| `TicketPickup` | *(se borra)* | D5. |

Los otros ~40 scripts se quedan con su nombre: ya dicen lo que hacen (`PlayerHealth`, `SlotMachine`, `BottleHolder`...). En todos se revisa el `<summary>` para que alguien de fuera entienda qué hace, dónde va y con qué se conecta.

### Organización (sin cambiar la estructura de carpetas)
- **Archivos fuera de su carpeta** (la carpeta correcta ya existe):
  - `RouletteMachine.cs` → `Machines/Roulette/` (hoy está vacía).
  - `RoundManager.cs` → `Scripts/Managers/` (vacía).
  - `IDamageable.cs` (en `Weapons/`, pero lo usan enemigos, jugador y botellas) → `Utilities/`, junto a `IInteractable`.
  - `TicketHUD`, `BottleEffectHUD` y `DynamicCrosshair` (son UI) → `Scripts/UI/`.
- **Nombres incoherentes (fuera de lo de test):**
  - Assets en español: `SM_Ruleta`, `T_Ruleta_*`, `RT_Ruleta_M`, `M_Ruleta`, `SO_BottleAmarilla`..., `Ticket_Amarillo`... (además sin `SO_`).
  - Modelos: `SM_Weapon_LanzaCartasV2_.fbx` y `Bottle.fbx` (sin `SM_`).
  - `TM_ImputKeys.png` (falta de ortografía y prefijo raro).
  - `PF_LanzaCartasWall` → `PF_Lanzacartas_Wall`.
  - `M_Liquid.mat` está en `Shaders/`.

## Estructura objetivo
Las mismas carpetas de ahora (ninguna se borra); solo cambia qué hay dentro.
```
Scripts/
  Characters/
    Bartender.cs
    Enemies/          EnemyAI, EnemyHealth, EnemyMovement, StateMachine
      Hunter/         HordeAttackSlots, HordeCollision
    Player/           PlayerController, PlayerHealth, PlayerPoints, PlayerInteractor, PlayerBuffs, PlayerBlackout, PlayerControls.inputactions
      Bottles/        BottleEffect, BottleHolder, ThrownBottle, BottlePuddle, BottleShatterEffect, LiquidWobble
      Weapons/        WeaponBase, HitscanWeapon, GrenadeLauncherWeapon, GrenadeProjectile, WeaponManager,
                      WallWeaponPickup, MuzzleFlash, BulletTracerEffect, BulletImpactEffect
  Environment/Props/Machines/
    Roulette/         RouletteMachine
    Slot/             SlotMachine, SlotBetButton, SlotLeverAnimator
    Ticket/           TicketData, TicketInventory, RouletteTicketDispenser
    TestPointsWallet  (de test: se queda aquí)
  Managers/           RoundManager
  ScriptableObjects/  BottleTypes/ (SO_BottleEffect_Yellow...)  Tickets/ (SO_Ticket_Yellow...)
  UI/                 AmmoHUD, ReloadHUD, DynamicCrosshair, HealthBarHUD, BlackoutBarHUD, PointsHUD, RoundHUD, TicketHUD, BottleEffectHUD
    MainMenu/         ATMScreen, MenuArrow, MenuCameraMover, MenuHoverManager, HoverOutline, CameraFocusTarget, QuitGameTarget
  Utilities/          WeightedRandom
    Interfaces/       IInteractable, IDamageable, IMovementModifiable (todas las interfaces: la "I" es de Interface)

Prefabs/              (sin cambios de sitio; lo de Test se queda igual)
  Characters/Weapons/Guns/WallGuns/  PF_LanzaCartasWall → PF_Lanzacartas_Wall

Art/3D/
  Models/             SM_Bottle, SM_ATM, SM_Roulette, SM_Lanzacartas
  Textures/           Lanzacartas/ (T_Lanzacartas_*), Roulette/ (T_Roulette_*)
  Materials/          M_Roulette, M_Liquid (sale de Shaders/) ...   Test/ sin cambios
Art/Sprites/InputKeys/  SPR_Key*, T_InputKeys
```
Si alguna carpeta se queda vacía al mover archivos, se mantiene.

## Convenciones

**Prefijos de assets**

| Tipo | Prefijo | Ejemplo |
|---|---|---|
| Prefab | `PF_` | `PF_Shotgun`, `PF_Shotgun_Wall` |
| Escena | `SCN_` | `SCN_Gameplay` |
| Modelo | `SM_` | `SM_Roulette` |
| Material | `M_` | `M_BulletHole` |
| Textura | `T_` | `T_Roulette_A` |
| Sprite | `SPR_` | `SPR_KeyE` |
| ScriptableObject (.asset) | `SO_` | `SO_Ticket_Yellow`, `SO_BottleEffect_Yellow` |
| Shader Graph | `SG_` | `SG_Liquid` |
| Physics Material | `PM_` | `PM_GrenadeBounce` |

Las variantes van al final con `_` (`_Wall`, `_A`/`_N`/`_M` en texturas). Nada de `Test`, `V2_` ni `New` en assets que se usan de verdad.

**Código** (el estilo que ya tiene el proyecto, ej. `PlayerPoints.cs`):
```csharp
/// <summary>
/// Qué hace el componente, en 2-4 líneas en español. Dónde va y con qué se conecta.
/// </summary>
public class PlayerPoints : MonoBehaviour
{
    [Tooltip("Puntos con los que empiezas. No cuentan para el ranking.")]
    [SerializeField] private int startingPoints = 0;

    public event Action OnPointsChanged;

    public int CurrentPoints => currentPoints;
    ...
}
```
- Clase = nombre del archivo, en inglés y PascalCase. Campos `camelCase` privados con `[SerializeField]` y `[Tooltip]` en español.
- Un `<summary>` por clase. Los comentarios explican el **porqué**, no repiten lo que dice el código.
- Eventos de C# (`event Action`) para avisar; la UI se suscribe en `OnEnable` y se desuscribe en `OnDisable`.
- Depuración: `OnDrawGizmos` con `if (!showDebug) return;` dentro de `#if UNITY_EDITOR`, textos con `Handles.Label` (decisión 10). Sin `Debug.Log` de información; `LogWarning` siempre que falte una referencia.
- Campos del Inspector: nombre con unidad, `[Tooltip]` en español con ejemplo, `[Header]` por grupos (decisión 11).
- Pruebas desde el Inspector con `[ContextMenu("TEST: ...")]`, no con teclas fijas.
- Se mantienen las reglas de la IA: nada de `transform.position` en los enemigos (se usa `EnemyMovement`) y sin memoria nueva por frame.

## Cómo se mueven y renombran archivos sin romper nada
- **Con Unity cerrado**, el archivo y su `.meta` siempre juntos (`git mv a b && git mv a.meta b.meta`). El GUID va en el `.meta`, así que las referencias de escenas y prefabs se mantienen.
- Renombrar una clase MonoBehaviour = renombrar el archivo también. Campo serializado renombrado = `[FormerlySerializedAs("viejo")]`.
- Juntar scripts:
  - **D1:** se conserva el `.meta` de `PistolWeapon` (pasa a ser `HitscanWeapon`). En los prefabs de las otras tres armas se cambia el GUID de `m_Script` editando el YAML. Los campos con el mismo nombre conservan su valor.
  - **D11:** igual, conservando el `.meta` de `HunterAI` (pasa a ser `EnemyAI`).
- Quitar un componente de un prefab (`TicketPickup` de `PF_TicketTest`) = editar su YAML para eliminar ese bloque y su entrada en `m_Component`.
- Después de cada bloque: script de referencias → abrir Unity → 0 errores en consola → probar → commit.

## Estrategia de pruebas
- **EditMode** (Test Framework, en `Assets/_Root_FPS_Casino/Scripts/Tests/Editor/`, **sin `.asmdef`**: un asmdef de tests no puede ver los scripts del juego, que van en Assembly-CSharp. Si NUnit no está disponible ahí, se usan self-checks con `Debug.Assert`): `WeightedRandom.Pick` y `RoundManager.EnemiesForRound` (hoy es un `Debug.Assert`; pasa a ser un test). Sin tests para lo que es puro Inspector o física.
- **Script de referencias**: busca GUIDs usados en `.unity`, `.prefab` y `.asset` que no estén en ningún `.meta`. Tiene que salir vacío después de cada bloque.
- **Prueba manual por bloque** (checklist en `tasks/todo.md`), en `SCN_Gameplay`, `SCN_Mallorca` y `SCN_MainMenu`: moverse/slide/wallrun, coger arma de la pared, disparar con las 5 armas, recargar, ruleta → ticket → barman → beber/lanzar botella, charco que ralentiza, tragaperras, rondas y HUD, y menú principal (incluido "Salir").

## Límites
- **Siempre:** comprobar la rama antes de cada commit; un commit por bloque, con un mensaje corto en español; mover el `.meta` con su archivo; compilar y pasar el script de referencias antes del commit; conservar la codificación de cada `.cs`.
- **Preguntar antes:** cualquier `git push`; borrar cualquier asset; cualquier cambio que se note jugando.
- **Nunca:** borrar carpetas (aunque estén vacías) ni cambiar la estructura de carpetas (salvo corregir las faltas de la decisión 1); tocar o borrar las escenas de los compañeros; commits en `main`/`dev`, `push --force`, `reset --hard` o `branch -D`; tocar `ExternalPacks/`; mover assets con Unity abierto; reescribir `PlayerController` o la IA sin proponerlo antes.

## Mapa de bloques (orden de trabajo)
| Id | Qué | Depende de |
|---|---|---|
| `tooling` | Script de referencias rotas, probar los comandos de compilar y de tests (sin asmdef) | — |
| `folders` | Corregir las faltas de los nombres de carpeta (decisión 1) | tooling |
| `move-scripts` | Mover los scripts a su carpeta y renombrar los de la tabla que no se juntan (sin cambiar lo que hacen) | tooling |
| `rename-assets` | Renombrar assets de Art y ScriptableObjects según las convenciones (lo de Test no se toca) | tooling |
| `code-core` | `WeightedRandom`, puntos (D2), comentarios (D9), depuración (B3) | move-scripts |
| `code-weapons` | `HitscanWeapon` (D1), `OnWeaponChanged` (D7), código muerto de armas (D8) | code-core |
| `code-casino` | Tickets, ruleta, tragaperras y barman (D3, D4, D5, D6, B4) | code-core, rename-assets |
| `code-bottles` | Input Drink/Throw (D10), charco que ralentiza de verdad (B1) | code-casino |
| `code-enemies` | `EnemyBase` + `HunterAI` → `EnemyAI` (D11), limpiar `EnemyMovement`, `IMovementModifiable` en el enemigo | code-bottles |
| `code-ui-menu` | HUDs y menú (B5) | code-weapons |
| `docs` | Actualizar CLAUDE.local.md (rutas nuevas, quitar `EnemyBase`/`PistolWeapon`...) y pedirte que regeneres graphify | todo |

En cada bloque `code-*`, todos los scripts del bloque reciben además su depuración visual (decisión 10) y la revisión de los campos del Inspector (decisión 11).

Se trabaja en este orden, con un commit por bloque. El plan detallado (tareas de ≤5 archivos) va en `tasks/plan.md` y `tasks/todo.md` cuando apruebes esta spec.

## Criterios de éxito
- [ ] Unity compila con **0 errores y 0 warnings nuevos**; el script de referencias sale **vacío** (ningún "Missing Script" ni referencia perdida).
- [ ] Tests EditMode en verde.
- [ ] La checklist manual pasa entera en `SCN_Gameplay`, `SCN_Mallorca` y `SCN_MainMenu`, y se juega igual que antes (más los arreglos B1 y B5).
- [ ] Ninguna carpeta borrada; las escenas de los compañeros, sin cambios en git.
- [ ] Nombres de archivos y assets en inglés (salvo Lanzacartas) y con su prefijo. No quedan `V2_` ni nombres en español fuera de lo de test. Lo de test, sin cambios.
- [ ] Cada script en la carpeta de su sistema según la estructura objetivo.
- [ ] Todos los campos del Inspector con nombre claro, `[Tooltip]` y `[Header]`; ningún valor ajustado se ha perdido (prefabs y escenas muestran los mismos números).
- [ ] Los scripts de la tabla de la decisión 10 tienen su casilla "Mostrar depuración" y se entiende lo que dibujan sin abrir el código. La consola está limpia al jugar (solo avisos de referencias que falten).
- [ ] Todas las clases con su `<summary>`; los comentarios coinciden con el código; ningún `.cs` con tildes rotas.
- [ ] Menos scripts que ahora (64 → 57: se borran 8 y se añade `WeightedRandom`, sin contar los tests) y ningún miembro público sin uso.
- [ ] CLAUDE.local.md describe la estructura nueva.

## Preguntas abiertas
Ninguna por ahora.
