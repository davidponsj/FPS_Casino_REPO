# Plan: Limpieza y reorganización (FPS Casino)

Spec: `SPEC.md` (raíz del repo). Rama: `dev-davidmallorca_V2`.
Estado: **spec cerrada con David el 2026-10-22. Plan pendiente de su aprobación. No se ha tocado nada del proyecto.**
Para empezar: David escribe `/build` (o `/build auto`). Se trabaja bloque a bloque, con un commit por bloque (preguntando antes), y nunca con Unity abierto mientras se mueven archivos.
(El plan anterior, el de la IA Hunter, está en git: commit 3f74066.)

## Cómo retomar
1. `git branch --show-current` → `dev-davidmallorca_V2`. `git status` limpio (salvo SPEC.md y tasks/, que están sin commitear).
2. Leer `SPEC.md` (decisiones 1-12 y tabla "Scripts que cambian").
3. Ir a la primera casilla sin marcar de `tasks/todo.md`.

## Datos ya comprobados (no hace falta repetir)
- **Scripts en Windows-1252** (conservar codificación; si las tildes dan guerra, quitarlas): PlayerController, EnemyBase, WeaponBase, PistolWeapon, IDamageable, MuzzleFlash, BulletTracerEffect, DynamicCrosshair, AmmoUI, ReloadRadialUI.
  - **Con BOM UTF-8:** EnemyMovement, AttackSlotManager, CrowdSolver, HunterAI, StateMachine.
  - **El resto:** UTF-8 sin BOM.
  - **Cómo editar los cp1252:** `iconv -f WINDOWS-1252 -t UTF-8` a un temporal, editar y volver con `iconv -f UTF-8 -t WINDOWS-1252`.
- **Uso de cada script** (GUID buscado en `.unity`, `.prefab` y `.asset`). Lo importante:
  - `PistolWeapon` → PF_Lanzacartas. `SniperWeapon` → Sniper.prefab. `TommyGunWeapon` → TommyGun.prefab. `ShotgunWeapon` → Shotgun.prefab (las tres en `Prefabs/Characters/Test/`).
  - `HunterAI` → PF_EnemyHunter. `EnemyBase` es abstracta: no está en ningún prefab.
  - `AttackSlotManager` y `CrowdSolver` → PF_Player.
  - `TestPointsWallet` → SCN_Ruben (no tocar la escena).
  - `TicketPickup` → PF_TicketTest. Ese prefab es el `worldPrefab` de los 6 Ticket_*.asset: **no se borra**, solo se le quita el componente.
  - `BottleEffectLibrary` → SO_BottleLibrary.asset. `BottleHolder.effectLibrary` lo apunta (en PF_Player y en escenas).
  - `MenuTriangle` → no está en ningún asset: se crea por código.
  - Escenas de compañeros con scripts nuestros: SCN_Ruben (BottleHolder, PlayerInteractor, TestPointsWallet, TicketHUD, TicketInventory) y SCN_Saez (BottleHolder, PlayerHealth, PlayerPoints, DynamicCrosshair, AmmoUI, ReloadRadialUI). **No se editan.** Todo cambio tiene que funcionar sin tocarlas: `FormerlySerializedAs`, GUIDs conservados y nada de campos nuevos que haya que asignar a mano en escena.
- `New Material*.mat` (Materials/Test) los usan escenas y prefabs → se quedan.
- Unity 6000.3.24f1 en `/c/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe`.

## Riesgos y cómo se cubren
| Riesgo | Mitigación |
|---|---|
| "Missing Script" o referencias perdidas al mover o juntar | Mover siempre con el `.meta`; script `check_refs` tras cada bloque; Unity cerrado al mover. |
| Valores del Inspector perdidos al renombrar campos | `[FormerlySerializedAs]` en cada campo renombrado. Comprobar 2-3 valores en el Inspector tras cada bloque. |
| `FormerlySerializedAs` puede no migrar los **overrides de instancias de prefab** en escenas (se guardan por `propertyPath` con el nombre del campo). Pasa, por ejemplo, con los HUDs de WeaponManager en SCN_Gameplay, SCN_Mallorca y SCN_Saez. | Antes de renombrar un campo, `grep "propertyPath: <campo>"` en los `.unity`. Si aparece: en nuestras escenas se actualiza el `propertyPath` a mano en el YAML; en las de compañeros se comprueba en Unity que el valor sigue. Si no sigue, se deja el nombre viejo del campo y se apunta. |
| Escenas de compañeros rotas | Ningún campo nuevo obligatorio en escena. Si una UI necesita a otro componente, lo busca sola (`FindAnyObjectByType`) cuando no está asignado. |
| Tests: un `.asmdef` de tests no puede ver Assembly-CSharp (los scripts del juego no tienen asmdef) | En `tooling` se prueba poner los tests sin asmdef en una carpeta `Editor/` (van a Assembly-CSharp-Editor). Si NUnit no está disponible ahí, se mantiene el `Debug.Assert` de RoundManager y se añade otro igual para `WeightedRandom`; se avisa a David. |
| Input de botellas (D10): las acciones nuevas se conectan con Unity Events en el Player Input de PF_Player | Se añaden las acciones al `.inputactions` y los métodos `OnDrink`/`OnThrow`. **La conexión en el Inspector la hace David** (2 clics por acción, instrucciones en la tarea). Hasta entonces se mantiene la lectura directa del teclado como respaldo. |
| Cambiar de nombre una carpeta solo en mayúsculas (`Inputkeys` → `InputKeys`) en Windows | Dos pasos: `git mv Inputkeys tmp && git mv tmp InputKeys`, y lo mismo con el `.meta`. |
| La spec dice "60 → 52 scripts", pero hoy hay **64** `.cs` en `Scripts/` | Se borran 8 (EnemyBase, 3 armas, 2 interfaces, BottleEffectLibrary, TicketPickup) y se añade WeightedRandom: **64 → 57** (+ los tests). Corregido en la spec. |
| La spec pone los tests con `.asmdef`, pero la decisión 12 dice "sin .asmdef" y un asmdef de tests no ve Assembly-CSharp | Se usa la vía sin asmdef (carpeta `Editor/`), como dice T0.3. Corregido en la spec. |

## Bloques y tareas
Reglas:
- Cada tarea toca como mucho **5 archivos de código**. Los YAML de prefabs y assets que solo cambian por un GUID no cuentan, ni los `git mv` sin cambios de código.
- Cada tarea deja el proyecto **compilando**.
- Cada bloque termina en un **checkpoint**: verificación V, checklist manual y commit (preguntando antes).
- Toda tarea de "Inspector + depuración" sigue la tabla de la decisión 10 (qué dibuja cada script en la Scene) de la spec y la decisión 11. Antes del commit se apunta aquí, bajo la tarea, la lista `campo viejo → campo nuevo` de cada script para que David la revise.
- Los bloques de riesgo alto (D1 en `code-weapons` y D11 en `code-enemies`, que cambian GUIDs de scripts en prefabs) llevan además una comprobación de valores del Inspector antes y después.

**Verificación estándar (V):**
1. `bash tasks/check_refs.sh` no da nada nuevo respecto a la línea base.
2. `bash tasks/compile.sh` sin `error CS` y `bash tasks/test.sh` en verde (Unity cerrado).
3. Abrir Unity: 0 errores en la consola.
4. Checklist manual del bloque (todo.md).

### Grafo de dependencias
```
tooling ─┬─ folders
         ├─ move-scripts ── code-core ─┬─ code-weapons ── code-ui-menu ─┐
         └─ rename-assets ─────────────┴─ code-casino ── code-bottles ── code-enemies ─┴─ docs
```
`folders`, `move-scripts` y `rename-assets` solo dependen de `tooling`; se hacen en ese orden para que los commits salgan limpios.
Nota: B1 (el charco ralentiza) va en `code-enemies` y no en `code-bottles` como dice el mapa de la spec, porque el cambio se hace en `EnemyMovement`.

### 0. tooling (depende de: nada)
- **T0.1 Script de referencias rotas** (S).
  - Qué: `tasks/check_refs.sh` lista los GUIDs usados en `Assets/**/*.unity|prefab|asset|mat|controller` que no aparecen en ningún `.meta` de `Assets/`, `Packages/` ni `Library/PackageCache`.
  - Hecho cuando:
    - El script corre en menos de 1 minuto.
    - Su salida se guarda en `tasks/check_refs_baseline.txt`.
    - Las roturas previas que salgan se apuntan aquí; no son nuestras.
  - **Hecho (2026-10-22).** Tarda unos 7 s. Su prueba es `bash tasks/check_refs_test.sh`, que debe decir OK. Comprobación en cada V: `bash tasks/check_refs.sh | diff - tasks/check_refs_baseline.txt` (sin salida = nada nuevo).
  - Línea base: 13 GUIDs que ya faltaban, todos inofensivos:
    - 7 shaders de depuración de probe volumes en `Settings/PC_Renderer.asset` que esta versión de URP ya no trae.
    - 4 componentes viejos en `Settings/DefaultVolumeProfile.asset`.
    - 1 script del pack en 2 materiales de `ExternalPacks/LowPolyWeapons_LITE`.
    - Ninguno está en archivos que vayamos a tocar.
- **T0.2 Compilación en batch** (XS).
  - Hecho cuando: el comando de la spec da 0 `error CS` con el proyecto tal cual y se apunta aquí cuánto tarda.
  - **Hecho (2026-10-22).**
    - Comando: `bash tasks/compile.sh`. Tarda unos 20 s y sale con código ≠ 0 si no compila. Avisa si Unity tiene el proyecto abierto.
    - Probado con un script roto temporal: detecta el `error CS` y sale con código 1.
    - Línea base: 0 errores, 1 warning (`RouletteMachine.outerRadius` sin usar, CS0414; se revisa en T6.4).
    - Los warnings solo salen cuando Unity recompila, es decir, cuando ha cambiado algún `.cs`.
- **T0.3 Tests EditMode** (S).
  - Qué: `Scripts/Tests/Editor/RoundManagerTests.cs` (sin asmdef) prueba `EnemiesForRound` con los valores del `Debug.Assert` actual.
  - Hecho cuando:
    - `-runTests` da verde, o
    - si NUnit no está disponible, se decide con David seguir con self-checks y se apunta aquí.
  - **Hecho (2026-10-22).** NUnit funciona sin asmdef en `Scripts/Tests/Editor/`.
    - Comando: `bash tasks/test.sh`. Unos 30 s; muestra el resumen y los tests que fallan, y sale con código 2 si falla alguno.
    - Probado en rojo con un valor falso (ronda 1 = 7): falló solo ese caso. Con el valor real: 9/9 en verde.
    - Se quitó el `Debug.Assert` de `RoundManager.Awake`: lo sustituye el test.
    - `tasks/test-results.xml` está ignorado en `.git/info/exclude` (no se toca el `.gitignore` del equipo).
- **Checkpoint 0:** baseline guardada, comandos de compilar y de tests probados. **Revisión con David** antes de mover nada.

### 1. folders (depende de: tooling)
- **T1.1 Corregir faltas en nombres de carpetas** (XS, solo `git mv`).
  - Qué: `ScripteableObjects` → `ScriptableObjects`, `PhisicMaterials` → `PhysicMaterials`, `Assets/Adressables` → `Addressables`, `Design/Lightning` → `Lighting`, `Art/Sprites/Inputkeys` → `InputKeys`, cada una con su `.meta`.
  - Hecho cuando:
    - Las 5 carpetas tienen el nombre nuevo.
    - `check_refs` no da nada nuevo.
    - No se ha borrado ninguna carpeta.
  - **Hecho (2026-10-22).** 55 renombres sin cambios de contenido; los GUID se conservan.
    - `Adressables` y `Design/Lightning` están vacías y git no las versiona: se renombraron con `mv` y solo su `.meta` va en git. Los compañeros recibirán el `.meta` nuevo y Unity creará la carpeta.
    - La carpeta vacía con el nombre viejo puede quedarse en sus discos: Unity le crea un `.meta` nuevo y avisa. Se borra a mano.
- **Checkpoint 1:** V + commit "Corrige nombres de carpetas".

### 2. move-scripts (depende de: tooling)
- **T2.1 Mover scripts a su carpeta** (S, solo `git mv`).
  - Qué: `RouletteMachine` → `Machines/Roulette/`; `RoundManager` → `Scripts/Managers/`; `IDamageable` → `Utilities/`; `TicketHUD`, `BottleEffectHUD` y `DynamicCrosshair` → `Scripts/UI/`.
  - Hecho cuando: los 6 están en su sitio con el `.meta` y el GUID sin cambiar.
- **T2.2 Renombrar scripts de la horda, el ticket y el menú** (M).
  - Qué: `AttackSlotManager` → `HordeAttackSlots`, `CrowdSolver` → `HordeCollision`, `TicketSpawner` → `RouletteTicketDispenser`, `MenuTriangle` → `MenuArrow`. Se renombran archivo y clase, y se actualiza quien los use (grep).
  - Hecho cuando:
    - grep de los nombres viejos en `Assets/**/*.cs` da 0 resultados.
    - Compila.
    - PF_Player sigue teniendo los dos componentes de la horda.
- **T2.3 Renombrar los HUDs** (M).
  - Qué: `AmmoUI` → `AmmoHUD`, `HealthBarUI` → `HealthBarHUD`, `BlackoutBarUI` → `BlackoutBarHUD`, `ReloadRadialUI` → `ReloadHUD`. Los campos de `WeaponManager` que los referencian llevan `FormerlySerializedAs`. `AmmoUI` y `ReloadRadialUI` están en cp1252.
  - Hecho cuando:
    - Compila.
    - Las referencias de WeaponManager en PF_Player siguen asignadas.
    - Los 4 HUDs se ven en SCN_Gameplay.
  - **Hecho (2026-10-22).**
    - Solo cambia el tipo de los campos de WeaponManager; sus nombres (`ammoUI`, `reloadRadialUI`) se quedan, porque T5.3 los borra.
    - En PF_Player esos campos están vacíos: los asignan como override `SCN_Gameplay`, `SCN_Mallorca` y `SCN_Saez`, y siguen funcionando.
    - Los 4 HUDs en SCN_Gameplay: pendiente de que David lo mire en Unity (checkpoint 2).
- **Checkpoint 2:** V + commit "Mueve y renombra scripts".

### 3. rename-assets (depende de: tooling)
- **T3.1 Ruleta** (S, assets).
  - Qué: `SM_Ruleta` → `SM_Roulette`, `M_Ruleta` → `M_Roulette`, `Textures/Ruleta` → `Roulette`, `T_Ruleta_A/N` → `T_Roulette_A/N`, `RT_Ruleta_M` → `T_Roulette_M`.
  - Hecho cuando: la ruleta se ve igual en SCN_Gameplay y no queda ningún asset con "Ruleta" fuera de Test.
  - **Hecho (2026-10-22).**
    - Ningún prefab ni escena usa `SM_Roulette` ni `M_Roulette`: la ruleta de prueba lleva sus propios materiales. Las 3 texturas solo las usa `M_Roulette`.
    - Se actualizó el `m_Name` interno del material.
    - Los `.mat` van por Git LFS, y se pueden editar como texto igualmente.
- **T3.2 Lanzacartas y botella** (S, assets).
  - Qué: `SM_Weapon_LanzaCartasV2_.fbx` → `SM_Lanzacartas.fbx`, `Textures/LanzaCartas` → `Lanzacartas`, `PF_LanzaCartasWall` → `PF_Lanzacartas_Wall`, `Bottle.fbx` → `SM_Bottle.fbx`.
  - Hecho cuando: el Lanzacartas en mano y en la pared, y la botella, se ven igual; no quedan `LanzaCartas` ni `V2_`.
- **T3.3 ScriptableObjects, M_Liquid y T_InputKeys** (S, assets).
  - Qué: `SO_Bottle<Color>` → `SO_BottleEffect_<Color>` y `Ticket_<Color>` → `SO_Ticket_<Color>`, con los colores en inglés. `M_Liquid.mat` → `Art/3D/Materials/`. `TM_ImputKeys.png` → `T_InputKeys.png`.
  - Hecho cuando:
    - Los 12 SO tienen el nombre nuevo.
    - SO_BottleLibrary sigue apuntando a todos.
    - El líquido de la botella se ve igual.
- **Checkpoint 3:** V (sobre todo los modelos y materiales) + commit "Renombra assets a la convención".

### 4. code-core (depende de: move-scripts)
- **T4.1 WeightedRandom** (S).
  - Qué: `Utilities/WeightedRandom.cs` con `Pick(IReadOnlyList<T>, Func<T,float> weight)`, más su test (o self-check). RouletteMachine y SlotMachine pasan a usarlo.
  - Hecho cuando:
    - Test en verde, con pesos 0, un solo elemento y una distribución aproximada en 10 000 tiradas.
    - No queda código de pesos duplicado en las máquinas.
- **T4.2 Puntos (D2, B4)** (M).
  - Qué: las máquinas cogen el `PlayerPoints` del jugador que interactúa. El texto de la tragaperras escucha `OnPointsChanged`. Se borran `IPointsService` e `IPointsReceiver`. `TestPointsWallet` queda como MonoBehaviour suelto.
  - Hecho cuando:
    - Compila.
    - La tragaperras y la ruleta cobran y pagan igual que antes.
    - La tragaperras no tiene `Update` para los puntos.
    - SCN_Ruben no da Missing Script.
  - **Hecho (2026-10-22).**
    - Las máquinas cobran y pagan con el `PlayerPoints` del jugador que interactúa. Si no lo tiene, la tirada es gratis y sale un aviso.
    - El texto de la tragaperras busca el `PlayerPoints` en `Start` y escucha `OnPointsChanged`.
    - Se borró el campo `pointsSource`. En SCN_Gameplay, SCN_Mallorca y SCN_Ruben queda su override en el YAML sin uso; Unity lo limpia al guardar la escena.
    - En SCN_Ruben las máquinas usan ahora los puntos del jugador (10 000, de PF_Player) en vez de los 1000 de `TestPointsWallet`.
    - Sin test automático: es interacción con corrutinas. Lo cubren la compilación y la prueba manual.
- **T4.3 Player sin el controller (D9, B3, decisiones 10 y 11)** (M).
  - Qué: PlayerHealth, PlayerPoints, PlayerInteractor, PlayerBuffs y PlayerBlackout reciben `<summary>`, casilla "Mostrar depuración" y repaso del Inspector.
  - Hecho cuando:
    - Ningún `Debug.Log` de información; con la casilla activa, sobre el jugador se ven vida, puntos, borrachera y efecto.
    - Todos los campos con `[Tooltip]` y `[Header]`.
    - Los valores de PF_Player no cambian.
  - **Hecho (2026-10-22).**
    - **Renombres `campo viejo → nuevo`: ninguno.** Los 9 campos ya decían lo que son; se añadieron `[Header]`, `[Tooltip]` y `[Min]`.
    - Depuración: cada script dibuja su línea sobre el jugador, a alturas distintas (vida 2,6 · puntos 2,45 · borrachera 2,3 · efectos 2,15). PlayerInteractor dibuja el rayo y lo que miras.
    - Fuera: el `Debug.Log` del prompt (B3), el de la muerte y el del blackout, y `DamageTakenMultiplier` (D8: siempre valía 1 y nadie lo cambiaba).
- **T4.4 PlayerController: Inspector y depuración** (S, cp1252).
  - Qué: tooltips y headers que falten, y depuración en la Scene (estado de movimiento, velocidad, momentum, coyote time, pared del wallrun). Sin tocar la lógica.
  - Hecho cuando: el diff solo toca atributos y el dibujo de depuración, y el movimiento se siente igual.
- **Checkpoint 4:** V + commit "Limpia puntos y scripts del jugador".

### 5. code-weapons (depende de: code-core) — riesgo alto
- **Antes de empezar:** apuntar los valores de Inspector de PF_Lanzacartas, Sniper, TommyGun y Shotgun (daño, cadencia, munición, dispersión, perdigones).
- **T5.1 HitscanWeapon (D1)** (S, cp1252).
  - Qué: `PistolWeapon.cs` → `HitscanWeapon.cs`, conservando el `.meta`. Campos `automatic` y `pelletsPerShot` (`FormerlySerializedAs("pelletCount")`). La lógica de disparo automático se trae de TommyGun.
  - Hecho cuando: compila y el Lanzacartas dispara igual.
- **T5.2 Los otros 3 prefabs pasan a HitscanWeapon** (S).
  - Qué: cambiar el GUID de `m_Script` en los 3 prefabs; `automatic: 1` en TommyGun. Borrar Sniper/TommyGun/ShotgunWeapon (.cs + .meta).
  - Hecho cuando:
    - Los 4 prefabs muestran los valores apuntados.
    - La Tommy dispara en automático y la escopeta con perdigones.
    - `check_refs` no da nada nuevo.
  - **Hecho (2026-10-22).**
    - Los valores de los 4 prefabs son idénticos a los de `tasks/weapon_values_before.txt`. Solo cambian `pelletCount` → `pelletsPerShot: 6` (también en el YAML) y el `automatic: 1` nuevo de TommyGun.
    - Los 3 scripts viejos solo los usaba su propio prefab de `Test/`.
    - Pendiente de la prueba manual (checkpoint 5): disparo automático de la Tommy y perdigones de la escopeta.
- **T5.3 Código muerto y OnWeaponChanged (D7, D8)** (M).
  - Qué: se tocan WeaponBase, WeaponManager, AmmoHUD, ReloadHUD y DynamicCrosshair. Cada miembro se borra solo tras comprobar con grep que no se usa. WeaponManager deja de conocer a los HUDs.
  - Hecho cuando:
    - Munición, recarga y retícula se actualizan al cambiar de arma, en SCN_Gameplay y en SCN_Saez (sin editar SCN_Saez).
  - **Hecho (2026-10-22).**
    - Borrados (grep sin usos): `audioSource` y su `RequireComponent(AudioSource)`, `OnDryFire`, `RefillAmmo` y `HasWeaponEquipped`. Los prefabs de armas conservan su componente AudioSource, que ya no es obligatorio.
    - Los 3 HUDs buscan el WeaponManager en `Start`, se suscriben a `OnWeaponChanged` y cogen el arma actual. Su campo `weapon` ya no sale en el Inspector; ninguna escena lo asignaba.
    - Los overrides `ammoUI`, `reloadRadialUI` y `crosshair` de SCN_Gameplay, SCN_Mallorca y SCN_Saez quedan sin uso en el YAML; Unity los limpia al guardar la escena.
    - Pendiente de la prueba manual (checkpoint 5): coger armas de la pared y ver la munición, la recarga y la retícula.
- **T5.4 Inspector y depuración de las armas** (M).
  - Qué: WeaponBase, GrenadeLauncherWeapon, GrenadeProjectile y WallWeaponPickup (prompt "Coger {arma} [E]").
  - Hecho cuando: se ven el cono de dispersión, la munición y el coste de las armas de pared, y los valores de los prefabs no cambian.
  - **Hecho (2026-10-22).**
    - **Renombres** (todos con `FormerlySerializedAs`; ningún override en escenas los usaba):
      - `WeaponBase.reloadTime` → `reloadSeconds`
      - `WeaponBase.burstResetTime` → `burstResetSeconds`
      - `GrenadeProjectile.fuseTime` → `fuseSeconds`
      - `GrenadeProjectile.minTimeBetweenBounces` → `minSecondsBetweenBounces`
    - Los YAML de los prefabs siguen con la clave vieja: Unity la lee por `FormerlySerializedAs` y la reescribe al guardar el prefab.
    - Prompt del arma de pared en español: "Coger {arma} [E]".
    - Depuración:
      - WeaponBase: cono de dispersión a 5 m, munición, recarga y últimos 8 impactos (búfer fijo, solo en el editor).
      - Granada: radio, rebotes y segundos para explotar.
      - Arma de pared: qué arma da.
    - GrenadeLauncherWeapon ya tenía tooltips en todo: sin cambios.
- **T5.5 Inspector de los efectos** (S).
  - Qué: MuzzleFlash, BulletTracerEffect y BulletImpactEffect (los dos primeros en cp1252).
  - Hecho cuando: los efectos se ven igual y las tildes no se rompen.
  - **Hecho (2026-10-23).**
    - **Renombres** (todos con `FormerlySerializedAs`; ningún override en escenas ni prefabs los usaba):
      - `MuzzleFlash.flashDuration` → `flashSeconds`
      - `BulletTracerEffect.duration` → `visibleSeconds`
      - `BulletTracerEffect.width` → `widthMeters`
      - `BulletImpactEffect.decalSizeRange` → `decalSizeMeters`
      - `BulletImpactEffect.decalLifetime` → `decalSeconds`
      - `BulletImpactEffect.decalFadeTime` → `decalFadeSeconds`
      - `BulletImpactEffect.particlesLifetime` → `particlesSeconds`
    - Tooltips con unidad y ejemplo en todos los campos y `[Header]` en los dos que no tenían. Sin depuración (decisión 10).
    - MuzzleFlash y BulletTracerEffect siguen en Windows-1252.
- **Checkpoint 5:** V + las 5 armas como antes + commit "Junta las armas hitscan en HitscanWeapon".

### 6. code-casino (depende de: code-core, rename-assets)
- **T6.1 TicketData lleva su BottleEffect (D3)** (M).
  - Qué: rellenar los 6 SO_Ticket según SO_BottleLibrary (leerla antes de borrarla). BottleHolder y ThrownBottle usan `ticket.bottleEffect`. Borrar BottleEffectLibrary; **preguntar** antes de borrar SO_BottleLibrary.
  - Hecho cuando: cada color de ticket da la misma botella que antes.
  - **Hecho (2026-10-23).** SO_BottleLibrary borrado con el sí de David.
    - `TicketData.bottleId` (string vacío en los 6) → `bottleEffect` (BottleEffect). Cada SO_Ticket_X apunta a SO_BottleEffect_X, igual que en la tabla.
    - BottleHolder lee `ticket.bottleEffect` y avisa con `LogWarning` si falta. ThrownBottle no cambia (recibe el efecto del BottleHolder).
    - Test `TicketDataTests`: cada ticket tiene la botella de su color.
    - PF_Player conserva la línea `effectLibrary:` en el YAML hasta que Unity guarde el prefab; no molesta.
    - check_refs marca el GUID de SO_BottleLibrary en PF_Player (la línea `effectLibrary:` vieja) hasta que Unity guarde el prefab.
- **T6.2 Una sola regla de bloqueo de la ruleta (D4)** (M).
  - Qué: se tocan TicketInventory, RouletteMachine, RouletteTicketDispenser, Bartender y BottleHolder.
  - Hecho cuando:
    - La ruleta está bloqueada exactamente si hay ticket esperando, ticket en el inventario o botella en la mano.
    - No quedan Lock/Unlock.
  - **Hecho (2026-10-23).**
    - `RouletteMachine.IsBlocked` = ticket esperando en el dispensador || ticket en el inventario || botella en la mano. Se calcula cada vez: no hay estado que se pueda quedar desincronizado.
    - TicketInventory guarda un solo `Ticket` (antes una lista). `Consume()` + `Tickets[0]` → `Take()`, que devuelve el ticket. Fuera `IsLocked`, `Lock`, `Unlock` y `ResetState` (sin usar).
    - Bartender: `bottleHolder.Give(inv.Take())`. BottleHolder ya no desbloquea nada: soltar la botella basta.
    - Test `TicketInventoryTests` (un solo ticket, Take, null).
    - Los mensajes de la ruleta no cambian (mismo orden: girando → coger ticket → gasta tu ticket → usa tu botella → jugar).
- **T6.3 Fuera TicketPickup (D5)** (XS + YAML).
  - Hecho cuando:
    - PF_TicketTest no tiene el componente.
    - El `.cs` está borrado.
    - El ticket sigue saliendo de la ruleta.
  - **Hecho (2026-10-23).** Quitado el componente de PF_TicketTest (ninguna escena lo instanciaba) y borrado `TicketPickup.cs`. El dispensador ya desactivaba el collider del ticket, así que no cambia nada en juego.
    - De paso: quitada de PF_Player la línea `effectLibrary:` que apuntaba a SO_BottleLibrary (T6.1). check_refs vuelve a la línea base.
- **T6.4 Inspector y depuración de la ruleta y el barman** (S): RouletteMachine, RouletteTicketDispenser y Bartender.
  - Pendiente de T2.2: `RouletteMachine.ticketSpawner` → `ticketDispenser` (`FormerlySerializedAs`). Revisar el warning de `outerRadius`.
  - **Hecho (2026-10-23).**
    - **Renombres** (todos con `FormerlySerializedAs`; en escenas solo hay overrides de `bottleHolder`, que no cambia):
      - RouletteMachine: `ticketSpawner` → `ticketDispenser`, `cost` → `spinCost`, `pocketRadius` → `ballRadiusMeters`, `ballHeight` → `ballHeightMeters`, `minDuration` → `minSpinSeconds`, `maxDuration` → `maxSpinSeconds`.
      - RouletteTicketDispenser: `slideDistance` → `slideDistanceMeters`, `slideDuration` → `slideSeconds`.
    - Borrado `RouletteMachine.outerRadius` (no se usaba: era el warning CS0414). PF_RouletteTest guarda aún la línea; Unity la quita al guardar.
    - Tooltips, `[Header]` y `[Min]` en todos los campos; `<summary>` en los tres scripts.
    - Fuera los `Debug.Log` de la ruleta y el barman. Avisos (`LogWarning`) si falta la bola, el dispensador o el TicketInventory.
    - Depuración ("Mostrar depuración"):
      - RouletteMachine: coste, estado (girando / bloqueada por qué / libre) y último resultado.
      - RouletteTicketDispenser: ticket esperando o "vacío".
      - Bartender: qué lleva el jugador.
      - TicketInventory: ticket actual o "vacío" (de la misma fila de la decisión 10).
- **T6.5 Inspector y depuración de la tragaperras** (S): SlotMachine, SlotBetButton y SlotLeverAnimator.
  - **Hecho (2026-10-26).**
    - **Renombres** (con `FormerlySerializedAs`; ningún override en escenas ni prefabs):
      - SlotMachine: `baseDuration` → `firstReelSeconds`, `extraDurationPerReel` → `extraSecondsPerReel`, `resultDisplayTime` → `resultSeconds`.
      - SlotLeverAnimator: `pullAngle` → `pullDegrees`, `pullTime` → `pullSeconds`, `returnTime` → `returnSeconds`.
    - Tooltips, `[Min]` y `<summary>` en los tres scripts.
    - Fuera los `Debug.Log` (el prompt y `resultText` ya lo dicen). Casilla "Depuración" en SlotMachine: apuesta y último resultado sobre la máquina.
- **Checkpoint 6:** V + ciclo ruleta → ticket → barman → botella → ruleta, tragaperras con y sin puntos + commit.

### 7. code-bottles (depende de: code-casino)
- **T7.1 Input Drink y Throw (D10)** (S).
  - Qué: las acciones en `PlayerControls.inputactions` y `BottleHolder.OnDrink/OnThrow`. Se mantiene el teclado directo como respaldo hasta que David conecte los eventos.
  - Hecho cuando: con los eventos conectados, F bebe y Q lanza. Después se quita el respaldo y la T queda como `[ContextMenu]`.
  - **Parte 1 hecha (2026-10-26).** Acciones `Drink` (F) y `Throw` (Q) en el mapa Player; `BottleHolder.OnDrink/OnThrow` (solo con `performed` y si llevas botella, así no bebe dos veces con el respaldo). Test `PlayerControlsTests` comprueba las dos acciones y su tecla.
  - **Falta (David):** en PF_Player, componente Player Input → Events → Player → `Drink`: + → arrastrar el objeto que tiene BottleHolder → `BottleHolder > OnDrink` (Dynamic CallbackContext). Igual con `Throw` → `OnThrow`. Después: quitar el respaldo de teclado (`drinkKey`, `throwKey` y la T) en el siguiente `/build`.
  - **Parte 2 hecha (2026-10-26).** David conectó Drink → `OnDrink` y Throw → `OnThrow` en PF_Player. Borrados `drinkKey`, `throwKey`, la lectura directa del teclado y la tecla T (la botella de prueba queda en el `[ContextMenu]`).
    - **Avisar a Rubén:** en SCN_Ruben hay un segundo BottleHolder (`BottleHand`) al que apuntan su barman y su ruleta, pero F/Q van al BottleHolder de PF_Player. Sin el respaldo, en su escena no se puede beber ni lanzar. Arreglo (1 min): apuntar `bottleHolder` del barman y la ruleta al BottleHolder de PF_Player, o borrar `BottleHand`. Decidido con David.
- **T7.2 Código muerto, Inspector y depuración de botellas** (M).
  - Qué: BottleHolder, ThrownBottle, BottlePuddle, BottleShatterEffect y LiquidWobble.
  - Hecho cuando: se ven la botella actual y el radio y los segundos del charco, y `FillAmount` y `Ticket` están borrados tras el grep.
  - **Hecho (2026-10-26).**
    - **Renombres** (con `FormerlySerializedAs`; ningún override en escenas ni prefabs):
      - BottleHolder: `throwForce` → `throwSpeed`, `throwUpAngle` → `throwUpDegrees`, `throwSpin` → `throwSpinRadPerSecond`. `drinkKey`/`throwKey` se dejan tal cual: se borran al cerrar T7.1.
      - ThrownBottle: `maxLifetime` → `maxLifetimeSeconds`, `puddleGroundSearch` → `puddleGroundSearchMeters`.
      - BottlePuddle: `defaultLifetime` → `defaultLifetimeSeconds`, `growTime` → `growSeconds`, `shrinkTime` → `shrinkSeconds`.
      - BottleShatterEffect: `lifetime` → `lifetimeSeconds`. LiquidWobble: ningún renombre (`fillAmount` tiene override en PF_Player).
    - Borrados `LiquidWobble.FillAmount` y `BottlePuddle.Ticket` (y el parámetro `ticket` de `BottlePuddle.Init`).
    - Fuera los 3 `Debug.Log` de BottleHolder; aviso si falta `bottleModel`.
    - Depuración: BottleHolder (botella de la mano y efecto activo con segundos) y BottlePuddle (disco del radio, segundos que quedan, enemigos dentro).
- **Checkpoint 7:** V + beber y lanzar las 6 botellas + commit.

### 8. code-enemies (depende de: code-bottles) — riesgo alto
- **Antes de empezar:** apuntar los valores de Inspector de PF_EnemyHunter (y de PF_EnemyBase).
  - **Hecho (2026-10-26)** en `tasks/enemy_values_before.txt`: HunterAI (16 campos), EnemyMovement (13) y los overrides de la variante (`baseSpeed: 7.22`, `m_StoppingDistance: 1.5`). Solo SCN_Mallorca instancia PF_EnemyHunter, sin overrides de la IA.
- **T8.1 EnemyAI (D11)** (M).
  - Qué: `HunterAI.cs` → `EnemyAI.cs` (conserva el `.meta`) con EnemyBase dentro. `FindPlayer` sale a EnemyAI estático o a RoundManager. HordeAttackSlots usa EnemyAI. Borrar EnemyBase (cp1252).
  - Hecho cuando: PF_EnemyHunter tiene los valores apuntados, y la horda persigue, rodea y hace cola igual que antes en SCN_Mallorca.
  - **Hecho (2026-10-26).** `Hunter/HunterAI.cs` → `Enemies/EnemyAI.cs` con el mismo `.meta` (mismo GUID) y EnemyBase dentro; EnemyBase borrado. `FindPlayer` queda privado en EnemyAI; RoundManager busca el tag Player por su cuenta. HordeAttackSlots usa EnemyAI. Quitado `SetBaseBodyColor` (sin uso; era para los perfiles del Patroller).
    - Valores de PF_EnemyHunter comparados con `tasks/enemy_values_before.txt`: idénticos. Test nuevo `EnemyAITests` (FlatDistance).
    - **Falta probar en Unity (Pausa 2):** la horda persigue, rodea y hace cola igual que antes.
- **T8.2 El charco ralentiza (B1) y código muerto de EnemyMovement** (S).
  - Hecho cuando: un enemigo en el charco va más lento y vuelve a su velocidad al salir, y no hay memoria nueva por frame.
  - **Hecho (2026-10-26).** EnemyMovement implementa IMovementModifiable: un multiplicador por charco en un Dictionary (solo cambia al entrar o salir, nada por frame) y manda el más lento; `SlowMultiplier` lo expone. Test `EnemyMovementTests` (3 casos).
    - Borrado sin uso: `Agent`, `BaseSpeed`, `SpeedMultiplier`, `CurrentSpeed`, `HasReachedDestination` y el evento `Stuck` (el repath al atascarse se queda).
    - **Falta probar en Unity (Pausa 2):** un enemigo en el charco va más lento y recupera su velocidad al salir.
- **T8.3 Inspector y depuración de EnemyAI, EnemyMovement y EnemyHealth** (S).
  - **Hecho (2026-10-26).**
    - **Renombres** (con `FormerlySerializedAs`; sin overrides en escenas; el override `baseSpeed` de PF_EnemyHunter no se toca):
      - EnemyAI: `reevaluateInterval` → `reevaluateSeconds`, `stateMinDuration` → `stateMinSeconds`.
      - EnemyMovement: `repathInterval` → `repathSeconds`, `stuckCheckInterval` → `stuckCheckSeconds`.
    - Tooltips con unidad y ejemplo y `[Min]` en todos los campos.
    - Depuración: EnemyAI (estado · segundos · metros al jugador + línea amarilla al destino; antes la etiqueta salía siempre en Play), EnemyMovement (velocidad real/máx., "charco ×0,5", ruta en cian, esfera roja si se atasca), EnemyHealth ("muere al primer impacto").
- **T8.4 Inspector y depuración de RoundManager, HordeAttackSlots y HordeCollision; comentarios de StateMachine** (M).
  - **Hecho (2026-10-26).**
    - **Renombres** (con `FormerlySerializedAs`; sin overrides en escenas):
      - HordeAttackSlots: `minHoldTime` → `minHoldSeconds`, `refreshInterval` → `refreshSeconds`, `drawGizmos` → `showDebug`.
      - HordeCollision: `drawGizmos` → `showDebug`. (En PF_Player los dos estaban a 1: siguen encendidos.)
      - RoundManager: ningún renombre; tooltips con unidad en los campos que no tenían.
    - Depuración: RoundManager pasa de `OnDrawGizmosSelected` a la casilla (ronda · vivos/máx. · faltan por salir · spawn cada X s o cuenta atrás del descanso, anillos y últimos spawns); HordeAttackSlots añade "atacando 3/6 · con puesto 5" (no "en cola": la cola la decide cada EnemyAI y no se cuenta en ningún sitio); HordeCollision dibuja también el radio de cada enemigo.
    - StateMachine: comentario actualizado en T8.1 (ya no nombra HunterAI ni PatrollerAI).
- **Checkpoint 8:** V + 3 rondas en SCN_Mallorca y SCN_Gameplay + commit "Junta EnemyBase y HunterAI en EnemyAI".

### 9. code-ui-menu (depende de: code-weapons)
- **T9.1 HUDs, parte 1** (M): AmmoHUD, ReloadHUD, DynamicCrosshair, HealthBarHUD y BlackoutBarHUD. Solo `<summary>` e Inspector, sin depuración.
  - **Hecho (2026-10-26).** Tooltips con unidad, `[Header]` y `[Min]` donde faltaban. Ningún renombre: los HUDs tienen overrides de instancia en escenas (también en SCN_Saez) y `FormerlySerializedAs` puede no migrarlos. AmmoHUD, ReloadHUD y DynamicCrosshair siguen en Windows-1252.
- **T9.2 HUDs, parte 2** (S): PointsHUD, RoundHUD, TicketHUD, BottleEffectHUD y MenuArrow.
- **T9.3 Botón Salir (B5) y menú, parte 1** (S): QuitGameTarget, ATMScreen y MenuCameraMover.
  - Hecho cuando: "Salir" para el Play Mode en el editor y cierra el juego en una build.
- **T9.4 Menú, parte 2** (S): MenuHoverManager, HoverOutline y CameraFocusTarget.
- **Checkpoint 9:** V + recorrido completo del menú + commit.

### 10. docs (depende de: todo)
- **T10.1** Actualizar `FPS_CASINO_PROJECT/CLAUDE.local.md`: rutas, nombres nuevos, codificación y depuración visual.
- **T10.2** Pedir a David que ejecute `/graphify Assets`.
- **T10.3** Pasada por los criterios de éxito de la spec, marcándolos uno a uno.
- **Checkpoint final:** todos los criterios en verde y revisión con David antes del push o del PR.

## Preguntas abiertas
Ninguna por ahora.

