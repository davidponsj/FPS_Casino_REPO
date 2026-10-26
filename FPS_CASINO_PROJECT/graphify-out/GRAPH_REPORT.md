# Graph Report - Assets  (2026-10-26)

## Corpus Check
- Large corpus: 90 files · ~626,985 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder.

## Summary
- 790 nodes · 1658 edges · 51 communities (28 shown, 23 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 90 edges (avg confidence: 0.83)
- Token cost: 198,087 input · 0 output

## Community Hubs (Navigation)
- Movimiento del jugador
- HUD y tragaperras
- Interacción y barman
- Depuración visual (DebugLabels)
- Botella en la mano
- Botellas lanzadas y charcos
- Tests EditMode
- Ruleta y sus texturas
- Movimiento de enemigos
- Menú principal (ATM)
- Puestos de la horda
- Rondas y spawns
- Armas hitscan
- Colisión de la horda
- IA del enemigo
- Máquina de estados
- Gestor de armas
- Buffs del jugador
- Vida del jugador
- Mira dinámica
- Efectos de botella (SO)
- Blackout del jugador
- HUD de munición
- Estados Approach/Engage
- Tests de ralentización
- Tipos de efecto al beber
- Estado Chase
- Estados de movimiento
- Monedero de prueba
- Stats mejorables
- Fuentes TextMesh Pro
- Estado Queue
- Vida del enemigo
- Arma de pared
- Texturas Lanzacartas
- Sprite de teclas
- Sprites de recarga
- Saltos de línea TMP
- Paletas de armas low poly
- Sprite tecla A
- Sprite tecla D
- Sprite tecla E
- Sprite tecla F
- Sprite clic izquierdo
- Sprite tecla Q
- Sprite tecla S
- Sprite tecla W
- Emojis TMP

## God Nodes (most connected - your core abstractions)
1. `PlayerController` - 73 edges
2. `EnemyMovement` - 49 edges
3. `EnemyAI` - 47 edges
4. `WeaponBase` - 46 edges
5. `HordeAttackSlots` - 37 edges
6. `BottleHolder` - 33 edges
7. `SlotMachine` - 30 edges
8. `RoundManager` - 27 edges
9. `RouletteMachine` - 24 edges
10. `TicketData` - 24 edges

## Surprising Connections (you probably didn't know these)
- `T_Roulette_M (roulette mask/metallic texture)` --conceptually_related_to--> `RouletteMachine`  [INFERRED]
  _Root_FPS_Casino/Art/3D/Textures/Roulette/T_Roulette_M.png → _Root_FPS_Casino/Scripts/Environment/Props/Machines/Roulette/RouletteMachine.cs
- `EmojiOne Attribution` --semantically_similar_to--> `LiberationSans OFL License`  [INFERRED] [semantically similar]
  ExternalPacks/TextMesh Pro/Sprites/EmojiOne Attribution.txt → ExternalPacks/TextMesh Pro/Fonts/LiberationSans - OFL.txt
- `Textures_Guns (Low Poly Weapons VOL.1 colour palette atlas)` --semantically_similar_to--> `WeaponsLite_Texture_01 (LowPolyWeapons LITE palette strip)`  [INFERRED] [semantically similar]
  ExternalPacks/Low Poly Weapons VOL.1/Textures_Guns.png → ExternalPacks/LowPolyWeapons_LITE/Texture/WeaponsLite_Texture_01.png
- `Bartender` --references--> `BottleHolder`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Characters/Bartender.cs → _Root_FPS_Casino/Scripts/Characters/Player/Bottles/BottleHolder.cs
- `EnemyAI` --references--> `EnemyMovement`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Characters/Enemies/EnemyAI.cs → _Root_FPS_Casino/Scripts/Characters/Enemies/EnemyMovement.cs

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Lanzacartas PBR texture set** — _root_fps_casino_art_3d_textures_lanzacartas_t_lanzacartas_a_texture, _root_fps_casino_art_3d_textures_lanzacartas_t_lanzacartas_m_texture, _root_fps_casino_art_3d_textures_lanzacartas_t_lanzacartas_n_texture [INFERRED 0.95]

## Communities (51 total, 23 thin omitted)

### Community 0 - "Movimiento del jugador"
Cohesion: 0.08
Nodes (9): PlayerController, CapsuleCenter, CurrentHorizontalSpeed, CurrentState, FeetPosition, HorizontalVelocity, IsGrounded, SpeedMultiplier (+1 more)

### Community 1 - "HUD y tragaperras"
Cohesion: 0.06
Nodes (15): SlotLeverAnimator, BlackoutBarHUD, BottleEffectHUD, HealthBarHUD, CameraFocusTarget, ViewPoint, HoverOutline, HoverLabel (+7 more)

### Community 2 - "Interacción y barman"
Cohesion: 0.06
Nodes (15): Bartender, InteractionPrompt, PlayerInteractor, PlayerPoints, CurrentPoints, TotalEarned, SlotBetButton, BetAmount (+7 more)

### Community 4 - "Depuración visual (DebugLabels)"
Cohesion: 0.06
Nodes (4): LiquidWobble, BulletImpactEffect, BulletTracerEffect, DebugLabels

### Community 5 - "Botella en la mano"
Cohesion: 0.07
Nodes (13): BottleHolder, ActiveEffect, EffectDuration, EffectTimeRemaining, HasActiveEffect, HasBottle, TicketData, TicketInventory (+5 more)

### Community 6 - "Botellas lanzadas y charcos"
Cohesion: 0.09
Nodes (8): BottlePuddle, BottleShatterEffect, ThrownBottle, LiquidColor, GrenadeLauncherWeapon, GrenadeProjectile, IDamageable, IMovementModifiable

### Community 7 - "Tests EditMode"
Cohesion: 0.09
Nodes (6): EnemyAITests, PlayerControlsTests, RoundManagerTests, TicketDataTests, WeightedRandomTests, WeightedRandom

### Community 8 - "Ruleta y sus texturas"
Cohesion: 0.08
Nodes (20): T_Roulette_A (roulette albedo texture atlas), Roulette ticket colors (SO_Ticket_<Color>), Roulette segment color strip (cyan/green/yellow/red/purple/black repeating), T_Roulette_M (roulette mask/metallic texture), Packed RGB channel mask (R/G channels per UV island), T_Roulette_N (Roulette Normal Map), Roulette Number Pockets Strip (segmented UV), Roulette Wheel UV Island (radial spokes) (+12 more)

### Community 9 - "Movimiento de enemigos"
Cohesion: 0.09
Nodes (11): EnemyMovement, Colliders, Destination, HasDestination, IsStopped, NavPosition, PathIsPartial, Radius (+3 more)

### Community 10 - "Menú principal (ATM)"
Cohesion: 0.11
Nodes (7): MuzzleFlash, ATMScreen, Options, MenuOption, MenuPage, MenuArrow, Filled

### Community 11 - "Puestos de la horda"
Cohesion: 0.13
Nodes (8): Candidate, HordeAttackSlots, ClaimRange, HasFreeToken, QueueRadius, ReleaseRange, Ring, Slot

### Community 12 - "Rondas y spawns"
Cohesion: 0.19
Nodes (3): RoundManager, AliveCount, CurrentRound

### Community 13 - "Armas hitscan"
Cohesion: 0.15
Nodes (9): HitscanWeapon, WeaponBase, CurrentAmmo, CurrentSpreadDegrees, IsReloading, NormalizedSpread, PlayerCamera, ReloadProgress01 (+1 more)

### Community 14 - "Colisión de la horda"
Cohesion: 0.14
Nodes (3): HordeCollision, Instance, PlayerRadius

### Community 15 - "IA del enemigo"
Cohesion: 0.14
Nodes (5): EnemyAI, CurrentStateName, DistanceToTarget, Movement, Target

### Community 16 - "Máquina de estados"
Cohesion: 0.20
Nodes (7): State, MinDuration, Name, StateMachine, CanLeave, Current, TimeInState

### Community 17 - "Gestor de armas"
Cohesion: 0.22
Nodes (3): WeaponManager, CurrentWeapon, ReloadHUD

### Community 18 - "Buffs del jugador"
Cohesion: 0.19
Nodes (6): PlayerBuffs, FireRateMultiplier, InfiniteMagazine, MoveSpeedMultiplier, ReloadTimeMultiplier, SlotLuck

### Community 19 - "Vida del jugador"
Cohesion: 0.23
Nodes (4): PlayerHealth, CurrentHealth, IsDead, MaxHealth

### Community 22 - "Efectos de botella (SO)"
Cohesion: 0.18
Nodes (7): BottleEffect, HasImpact, HasTimedDrinkEffect, PuddleAffectsEnemies, ThrowSelfEffect, None, ReloadTime

### Community 25 - "Estados Approach/Engage"
Cohesion: 0.20
Nodes (4): ApproachState, MinDuration, EngageState, MinDuration

### Community 27 - "Tipos de efecto al beber"
Cohesion: 0.25
Nodes (8): DrinkEffect, FireRateBoost, HealPercent, InfiniteMagazine, KillSelf, None, SlotLuck, SpeedBoost

### Community 29 - "Estados de movimiento"
Cohesion: 0.29
Nodes (7): PlayerMovementState, Airborne, Idle, Sliding, Sprinting, Walking, WallRunning

### Community 32 - "Stats mejorables"
Cohesion: 0.33
Nodes (6): Stat, FireRate, InfiniteMagazine, MoveSpeed, ReloadTime, SlotLuck

### Community 33 - "Fuentes TextMesh Pro"
Cohesion: 0.40
Nodes (5): Liberation Sans Font (Red Hat / Google Arimo), LiberationSans OFL License, SIL Open Font License 1.1, EmojiOne (emoji sprite provider), EmojiOne Attribution

### Community 37 - "Texturas Lanzacartas"
Cohesion: 0.50
Nodes (4): Lanzacartas weapon (card launcher, red body with card-suit decals and gauge), T_Lanzacartas_A (Lanzacartas albedo), T_Lanzacartas_M (Lanzacartas metallic/mask), T_Lanzacartas_N (Lanzacartas normal map)

### Community 38 - "Sprite de teclas"
Cohesion: 0.67
Nodes (4): T_InputKeys (input prompt sprite sheet), Gamepad button icons (A/B/X/Y, D-pad, sticks, LB/RB/LT/RT), Keyboard key icons (F1-F12, Alt, Ctrl, Shift, Tab, Esc, Enter, arrows), Mouse and touch gesture icons

### Community 39 - "Sprites de recarga"
Cohesion: 0.67
Nodes (3): SPR_KeyR (R reload key prompt sprite), Radial reload indicator (ReloadRadialUI), SPR_ReloadCircle (solid red circle UI sprite)

## Ambiguous Edges - Review These
- `Roulette segment color strip (cyan/green/yellow/red/purple/black repeating)` → `Roulette ticket colors (SO_Ticket_<Color>)`  [AMBIGUOUS]
  _Root_FPS_Casino/Art/3D/Textures/Roulette/T_Roulette_A.png · relation: conceptually_related_to

## Knowledge Gaps
- **136 isolated node(s):** `InteractionPrompt`, `Movement`, `Target`, `CurrentStateName`, `DistanceToTarget` (+131 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 219 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **23 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PlayerController` connect `Movimiento del jugador` to `HUD y tragaperras`, `Archivos de scripts`, `Depuración visual (DebugLabels)`, `Botella en la mano`, `Botellas lanzadas y charcos`, `Ruleta y sus texturas`, `Rondas y spawns`, `Armas hitscan`, `Gestor de armas`, `Buffs del jugador`, `Acciones del Player Input`, `Estados de movimiento`?**
  _High betweenness centrality (0.141) - this node is a cross-community bridge._
- **What connects `InteractionPrompt`, `Movement`, `Target` to the rest of the system?**
  _136 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Movimiento del jugador` be split into smaller, more focused modules?**
  _Cohesion score 0.07894736842105263 - nodes in this community are weakly interconnected._
- **Why does `EnemyAI` connect `IA del enemigo` to `HUD y tragaperras`, `Estado Queue`, `Archivos de scripts`, `Depuración visual (DebugLabels)`, `Tests EditMode`, `Ruleta y sus texturas`, `Movimiento de enemigos`, `Puestos de la horda`, `Máquina de estados`, `Estados Approach/Engage`, `Estado Chase`, `Fichas de ataque`?**
  _High betweenness centrality (0.117) - this node is a cross-community bridge._
- **Should `HUD y tragaperras` be split into smaller, more focused modules?**
  _Cohesion score 0.05974025974025974 - nodes in this community are weakly interconnected._
- **Why does `EnemyMovement` connect `Movimiento de enemigos` to `HUD y tragaperras`, `Archivos de scripts`, `Depuración visual (DebugLabels)`, `Botella en la mano`, `Botellas lanzadas y charcos`, `Puestos de la horda`, `Rondas y spawns`, `Colisión de la horda`, `IA del enemigo`, `Estados Approach/Engage`, `Tests de ralentización`?**
  _High betweenness centrality (0.096) - this node is a cross-community bridge._

### Low-confidence Hints
_AMBIGUOUS edges — the extractor was unsure. Verify before acting on these._

- **What is the exact relationship between `Roulette segment color strip (cyan/green/yellow/red/purple/black repeating)` and `Roulette ticket colors (SO_Ticket_<Color>)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._