# Graph Report - Assets  (2026-10-22)

## Corpus Check
- Large corpus: 89 files · ~621,278 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder.

## Summary
- 747 nodes · 1540 edges · 45 communities (26 shown, 19 thin omitted)
- Extraction: 94% EXTRACTED · 6% INFERRED · 0% AMBIGUOUS · INFERRED: 100 edges (avg confidence: 0.83)
- Token cost: 193,636 input · 0 output

## Community Hubs (Navigation)
- Movimiento del jugador
- Interactuables (Bartender, armas de pared)
- Visuales y animación (tragaperras)
- Blackout del jugador
- Menús y pantalla ATM
- Movimiento de enemigos (NavMesh)
- Vida y daño de enemigos
- Puntos del jugador
- Fichas de ataque
- Máquina de ruleta
- Rondas y spawns
- Base de armas
- Efectos de botella (datos)
- BottleHolder
- EnemyBase
- CrowdSolver
- Máquina de estados
- Botella lanzada y rotura
- Tipos de arma
- Sprites de teclas
- WeaponManager
- Estados Chase/Engage
- HunterAI
- Impactos de bala
- UI de munición
- Mira dinámica
- Fogonazo y recarga
- Asignación de puestos
- Tipos de DrinkEffect
- Líquido con wobble
- Datos de ticket
- PlayerInteractor
- Estado Approach
- Licencias TMP
- Charco de botella
- Trazadora de bala
- Estado Queue
- Texturas Lanzacartas
- Texturas Ruleta
- Saltos de línea TMP
- Texturas packs de armas
- Atlas EmojiOne

## God Nodes (most connected - your core abstractions)
1. `PlayerController` - 75 edges
2. `WeaponBase` - 50 edges
3. `EnemyMovement` - 43 edges
4. `AttackSlotManager` - 37 edges
5. `EnemyBase` - 33 edges
6. `BottleHolder` - 32 edges
7. `SlotMachine` - 30 edges
8. `RoundManager` - 26 edges
9. `TicketData` - 25 edges
10. `HunterAI` - 24 edges

## Surprising Connections (you probably didn't know these)
- `EmojiOne Attribution` --semantically_similar_to--> `LiberationSans OFL License`  [INFERRED] [semantically similar]
  ExternalPacks/TextMesh Pro/Sprites/EmojiOne Attribution.txt → ExternalPacks/TextMesh Pro/Fonts/LiberationSans - OFL.txt
- `Textures_Guns (Low Poly Weapons VOL.1 colour palette atlas)` --semantically_similar_to--> `WeaponsLite_Texture_01 (LowPolyWeapons LITE palette strip)`  [INFERRED] [semantically similar]
  ExternalPacks/Low Poly Weapons VOL.1/Textures_Guns.png → ExternalPacks/LowPolyWeapons_LITE/Texture/WeaponsLite_Texture_01.png
- `SPR_KeyA (A key prompt sprite)` --references--> `TM_ImputKeys (pixel-art input prompt sprite sheet: keyboard, mouse, gamepad)`  [INFERRED]
  _Root_FPS_Casino/Art/Sprites/Inputkeys/SPR_KeyA.png → _Root_FPS_Casino/Art/Sprites/Inputkeys/TM_ImputKeys.png
- `SPR_KeyD (D key prompt sprite)` --references--> `TM_ImputKeys (pixel-art input prompt sprite sheet: keyboard, mouse, gamepad)`  [INFERRED]
  _Root_FPS_Casino/Art/Sprites/Inputkeys/SPR_KeyD.png → _Root_FPS_Casino/Art/Sprites/Inputkeys/TM_ImputKeys.png
- `SPR_KeyE (E interact key prompt sprite)` --references--> `TM_ImputKeys (pixel-art input prompt sprite sheet: keyboard, mouse, gamepad)`  [INFERRED]
  _Root_FPS_Casino/Art/Sprites/Inputkeys/SPR_KeyE.png → _Root_FPS_Casino/Art/Sprites/Inputkeys/TM_ImputKeys.png

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Lanzacartas PBR texture set** — _root_fps_casino_art_3d_textures_lanzacartas_t_lanzacartas_a_texture, _root_fps_casino_art_3d_textures_lanzacartas_t_lanzacartas_m_texture, _root_fps_casino_art_3d_textures_lanzacartas_t_lanzacartas_n_texture [INFERRED 0.95]
- **Ruleta PBR texture set** — _root_fps_casino_art_3d_textures_ruleta_t_ruleta_a_texture, _root_fps_casino_art_3d_textures_ruleta_rt_ruleta_m_texture, _root_fps_casino_art_3d_textures_ruleta_t_ruleta_n_texture [INFERRED 0.95]
- **Input key prompt sprite set (WASD, E, F, Q, R, left click)** — _root_fps_casino_art_sprites_inputkeys_spr_keya_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keyd_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keye_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keyf_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keylclick_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keyq_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keyr_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keys_sprite, _root_fps_casino_art_sprites_inputkeys_spr_keyw_sprite, _root_fps_casino_art_sprites_inputkeys_tm_imputkeys_texture [INFERRED 0.85]

## Communities (45 total, 19 thin omitted)

### Community 0 - "Movimiento del jugador"
Cohesion: 0.07
Nodes (19): PlayerController, CapsuleCenter, CurrentHorizontalSpeed, CurrentState, FeetPosition, HorizontalVelocity, IsGrounded, IsSliding (+11 more)

### Community 1 - "Interactuables (Bartender, armas de pared)"
Cohesion: 0.05
Nodes (20): Bartender, InteractionPrompt, WallWeaponPickup, InteractionPrompt, IPointsReceiver, IPointsService, CurrentPoints, SlotBetButton (+12 more)

### Community 2 - "Visuales y animación (tragaperras)"
Cohesion: 0.06
Nodes (14): BottleEffectHUD, SlotLeverAnimator, TicketHUD, BlackoutBarUI, HealthBarUI, CameraFocusTarget, ViewPoint, HoverOutline (+6 more)

### Community 4 - "Blackout del jugador"
Cohesion: 0.06
Nodes (20): PlayerBlackout, Blackout01, PlayerBuffs, FireRateMultiplier, InfiniteMagazine, MoveSpeedMultiplier, ReloadTimeMultiplier, SlotLuck (+12 more)

### Community 5 - "Menús y pantalla ATM"
Cohesion: 0.10
Nodes (7): ATMScreen, Options, MenuOption, MenuPage, MenuTriangle, Filled, RoundHUD

### Community 6 - "Movimiento de enemigos (NavMesh)"
Cohesion: 0.08
Nodes (13): EnemyMovement, Agent, BaseSpeed, Colliders, CurrentSpeed, HasReachedDestination, IsStopped, NavPosition (+5 more)

### Community 7 - "Vida y daño de enemigos"
Cohesion: 0.11
Nodes (6): EnemyHealth, IsDead, IMovementModifiable, GrenadeLauncherWeapon, GrenadeProjectile, IDamageable

### Community 8 - "Puntos del jugador"
Cohesion: 0.08
Nodes (9): PlayerPoints, CurrentPoints, TotalEarned, TicketInventory, HasTicket, Instance, IsLocked, Tickets (+1 more)

### Community 9 - "Fichas de ataque"
Cohesion: 0.11
Nodes (7): AttackSlotManager, ClaimRange, HasFreeToken, QueueRadius, ReleaseRange, Ring, Slot

### Community 10 - "Máquina de ruleta"
Cohesion: 0.14
Nodes (7): RouletteMachine, InteractionPrompt, StepAngle, SegmentEvent, TicketSpawner, HasTicketWaiting, WaitingTicketName

### Community 11 - "Rondas y spawns"
Cohesion: 0.19
Nodes (3): RoundManager, AliveCount, CurrentRound

### Community 12 - "Base de armas"
Cohesion: 0.14
Nodes (9): WeaponBase, CurrentAmmo, CurrentSpreadDegrees, IsReloading, MagazineSize, NormalizedSpread, PlayerCamera, ReloadProgress01 (+1 more)

### Community 13 - "Efectos de botella (datos)"
Cohesion: 0.15
Nodes (9): BottleEffect, HasImpact, HasTimedDrinkEffect, PuddleAffectsEnemies, ThrowSelfEffect, None, ReloadTime, BottleEffectLibrary (+1 more)

### Community 14 - "BottleHolder"
Cohesion: 0.21
Nodes (6): BottleHolder, ActiveEffect, EffectDuration, EffectTimeRemaining, HasActiveEffect, HasBottle

### Community 15 - "EnemyBase"
Cohesion: 0.19
Nodes (5): EnemyBase, CurrentStateName, DistanceToTarget, Movement, Target

### Community 16 - "CrowdSolver"
Cohesion: 0.21
Nodes (3): CrowdSolver, Instance, PlayerRadius

### Community 17 - "Máquina de estados"
Cohesion: 0.20
Nodes (7): State, MinDuration, Name, StateMachine, CanLeave, Current, TimeInState

### Community 18 - "Botella lanzada y rotura"
Cohesion: 0.21
Nodes (3): BottleShatterEffect, ThrownBottle, LiquidColor

### Community 19 - "Tipos de arma"
Cohesion: 0.15
Nodes (4): PistolWeapon, ShotgunWeapon, SniperWeapon, TommyGunWeapon

### Community 20 - "Sprites de teclas"
Cohesion: 0.17
Nodes (12): SPR_KeyA (A key prompt sprite), SPR_KeyD (D key prompt sprite), SPR_KeyE (E interact key prompt sprite), SPR_KeyF (F key prompt sprite), SPR_KeyLClick (left mouse click prompt sprite), SPR_KeyQ (Q key prompt sprite), SPR_KeyR (R reload key prompt sprite), SPR_KeyS (S key prompt sprite) (+4 more)

### Community 21 - "WeaponManager"
Cohesion: 0.24
Nodes (4): WeaponManager, CurrentWeapon, HasWeaponEquipped, ReloadRadialUI

### Community 22 - "Estados Chase/Engage"
Cohesion: 0.18
Nodes (4): ChaseState, MinDuration, EngageState, MinDuration

### Community 30 - "Tipos de DrinkEffect"
Cohesion: 0.25
Nodes (8): DrinkEffect, FireRateBoost, HealPercent, InfiniteMagazine, KillSelf, None, SlotLuck, SpeedBoost

### Community 35 - "Licencias TMP"
Cohesion: 0.40
Nodes (5): Liberation Sans Font (Red Hat / Google Arimo), LiberationSans OFL License, SIL Open Font License 1.1, EmojiOne (emoji sprite provider), EmojiOne Attribution

### Community 39 - "Texturas Lanzacartas"
Cohesion: 0.50
Nodes (4): Lanzacartas weapon (card launcher, red body with card-suit decals and gauge), T_Lanzacartas_A (Lanzacartas albedo), T_Lanzacartas_M (Lanzacartas metallic/mask), T_Lanzacartas_N (Lanzacartas normal map)

### Community 40 - "Texturas Ruleta"
Cohesion: 0.50
Nodes (4): RT_Ruleta_M (Ruleta RGB channel mask), Ruleta prop (casino roulette), T_Ruleta_A (Ruleta albedo, rainbow stripes and orange sunburst), T_Ruleta_N (Ruleta normal map, radial spokes)

## Knowledge Gaps
- **145 isolated node(s):** `InteractionPrompt`, `Movement`, `Target`, `CurrentStateName`, `DistanceToTarget` (+140 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 234 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **19 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PlayerController` connect `Movimiento del jugador` to `PlayerInteractor`, `Visuales y animación (tragaperras)`, `Archivos de enemigos y botellas`, `Blackout del jugador`, `Menús y pantalla ATM`, `Vida y daño de enemigos`, `Empuje por colisión`, `Máquina de ruleta`, `Rondas y spawns`, `Base de armas`, `BottleHolder`, `WeaponManager`, `Callbacks de input`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **What connects `InteractionPrompt`, `Movement`, `Target` to the rest of the system?**
  _145 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Movimiento del jugador` be split into smaller, more focused modules?**
  _Cohesion score 0.0671602326811211 - nodes in this community are weakly interconnected._
- **Why does `WeaponBase` connect `Base de armas` to `Movimiento del jugador`, `Interactuables (Bartender, armas de pared)`, `PlayerInteractor`, `Archivos de enemigos y botellas`, `Visuales y animación (tragaperras)`, `Blackout del jugador`, `Trazadora de bala`, `Vida y daño de enemigos`, `Máquina de ruleta`, `Rondas y spawns`, `Tipos de arma`, `WeaponManager`, `Callbacks de input`, `UI de munición`, `Mira dinámica`, `Fogonazo y recarga`?**
  _High betweenness centrality (0.097) - this node is a cross-community bridge._
- **Should `Interactuables (Bartender, armas de pared)` be split into smaller, more focused modules?**
  _Cohesion score 0.05310734463276836 - nodes in this community are weakly interconnected._
- **Why does `EnemyMovement` connect `Movimiento de enemigos (NavMesh)` to `Visuales y animación (tragaperras)`, `Archivos de enemigos y botellas`, `Vida y daño de enemigos`, `Fichas de ataque`, `Empuje por colisión`, `Rondas y spawns`, `EnemyBase`, `CrowdSolver`, `Estados Chase/Engage`?**
  _High betweenness centrality (0.081) - this node is a cross-community bridge._
- **Should `Visuales y animación (tragaperras)` be split into smaller, more focused modules?**
  _Cohesion score 0.05870020964360587 - nodes in this community are weakly interconnected._