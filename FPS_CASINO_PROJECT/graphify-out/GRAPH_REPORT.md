# Graph Report - Assets  (2026-10-19)

## Corpus Check
- Large corpus: 62 files · ~503,556 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder.

## Summary
- 482 nodes · 1011 edges · 19 communities (16 shown, 3 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 51 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Movimiento del jugador
- Armas y munición
- Botellas y tickets
- HUD y blackout
- Proyectiles y charcos
- Salud y buffs
- Efectos de disparo
- Ruleta y tickets
- Tragaperras
- Líquido e impactos
- Inventario de tickets
- Puntos del jugador
- Cartera de pruebas
- Mira dinámica
- Botón de apuesta
- Estados de movimiento
- Palanca tragaperras
- Barman

## God Nodes (most connected - your core abstractions)
1. `PlayerController` - 75 edges
2. `WeaponBase` - 50 edges
3. `BottleHolder` - 32 edges
4. `SlotMachine` - 30 edges
5. `TicketData` - 25 edges
6. `PlayerHealth` - 24 edges
7. `RouletteMachine` - 23 edges
8. `PlayerBuffs` - 22 edges
9. `BottlePuddle` - 20 edges
10. `BottleEffect` - 18 edges

## Surprising Connections (you probably didn't know these)
- `BlackoutBarUI` --references--> `PlayerBlackout`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Blackout/BlackoutBarUI.cs → _Root_FPS_Casino/Scripts/Blackout/PlayerBlackout.cs
- `PlayerBlackout` --references--> `BottleHolder`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Blackout/PlayerBlackout.cs → _Root_FPS_Casino/Scripts/Machines/BottleHolder.cs
- `BottlePuddle` --references--> `BottleEffect`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Bottles/BottlePuddle.cs → _Root_FPS_Casino/Scripts/Bottles/BottleEffect.cs
- `ThrownBottle` --references--> `BottleEffect`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Bottles/ThrownBottle.cs → _Root_FPS_Casino/Scripts/Bottles/BottleEffect.cs
- `BottleEffectHUD` --references--> `BottleHolder`  [EXTRACTED]
  _Root_FPS_Casino/Scripts/Bottles/BottleEffectHUD.cs → _Root_FPS_Casino/Scripts/Machines/BottleHolder.cs

## Import Cycles
- None detected.

## Communities (19 total, 3 thin omitted)

### Community 0 - "Movimiento del jugador"
Cohesion: 0.08
Nodes (12): PlayerController, CapsuleCenter, CurrentHorizontalSpeed, CurrentState, FeetPosition, HorizontalVelocity, IsGrounded, IsSliding (+4 more)

### Community 1 - "Armas y munición"
Cohesion: 0.06
Nodes (15): PlayerInteractor, AmmoUI, TommyGunWeapon, WeaponBase, CurrentAmmo, CurrentSpreadDegrees, IsReloading, MagazineSize (+7 more)

### Community 2 - "Botellas y tickets"
Cohesion: 0.05
Nodes (28): BottleEffect, HasImpact, HasTimedDrinkEffect, PuddleAffectsEnemies, DrinkEffect, FireRateBoost, HealPercent, InfiniteMagazine (+20 more)

### Community 3 - "HUD y blackout"
Cohesion: 0.06
Nodes (5): BlackoutBarUI, BottleEffectHUD, HealthBarUI, PointsHUD, ReloadRadialUI

### Community 4 - "Proyectiles y charcos"
Cohesion: 0.07
Nodes (11): BottlePuddle, Ticket, BottleShatterEffect, IMovementModifiable, ThrownBottle, LiquidColor, GrenadeLauncherWeapon, GrenadeProjectile (+3 more)

### Community 5 - "Salud y buffs"
Cohesion: 0.06
Nodes (20): PlayerBlackout, Blackout01, PlayerBuffs, FireRateMultiplier, InfiniteMagazine, MoveSpeedMultiplier, ReloadTimeMultiplier, SlotLuck (+12 more)

### Community 6 - "Efectos de disparo"
Cohesion: 0.10
Nodes (5): BulletTracerEffect, MuzzleFlash, PistolWeapon, ShotgunWeapon, SniperWeapon

### Community 7 - "Ruleta y tickets"
Cohesion: 0.14
Nodes (7): RouletteMachine, InteractionPrompt, StepAngle, SegmentEvent, TicketSpawner, HasTicketWaiting, WaitingTicketName

### Community 8 - "Tragaperras"
Cohesion: 0.17
Nodes (4): SlotMachine, InteractionPrompt, IsSpinning, Symbol

### Community 9 - "Líquido e impactos"
Cohesion: 0.15
Nodes (3): LiquidWobble, FillAmount, BulletImpactEffect

### Community 10 - "Inventario de tickets"
Cohesion: 0.15
Nodes (5): TicketInventory, HasTicket, Instance, IsLocked, Tickets

### Community 11 - "Puntos del jugador"
Cohesion: 0.20
Nodes (5): IPointsService, CurrentPoints, PlayerPoints, CurrentPoints, TotalEarned

### Community 13 - "Cartera de pruebas"
Cohesion: 0.24
Nodes (3): IPointsReceiver, TestPointsWallet, CurrentPoints

### Community 15 - "Botón de apuesta"
Cohesion: 0.32
Nodes (3): SlotBetButton, BetAmount, InteractionPrompt

### Community 16 - "Estados de movimiento"
Cohesion: 0.29
Nodes (7): PlayerMovementState, Airborne, Idle, Sliding, Sprinting, Walking, WallRunning

### Community 18 - "Barman"
Cohesion: 0.33
Nodes (4): IInteractable, InteractionPrompt, Bartender, InteractionPrompt

## Knowledge Gaps
- **83 isolated node(s):** `Blackout01`, `None`, `KillSelf`, `HealPercent`, `SpeedBoost` (+78 more)
  These have ≤1 connection - possible missing edges. (Counts symbols only; 139 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `PlayerController` connect `Movimiento del jugador` to `Armas y munición`, `Botellas y tickets`, `HUD y blackout`, `Proyectiles y charcos`, `Salud y buffs`, `Ruleta y tickets`, `Líquido e impactos`, `Estados de movimiento`?**
  _High betweenness centrality (0.250) - this node is a cross-community bridge._
- **What connects `Blackout01`, `None`, `KillSelf` to the rest of the system?**
  _83 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Movimiento del jugador` be split into smaller, more focused modules?**
  _Cohesion score 0.08106473079249849 - nodes in this community are weakly interconnected._
- **Why does `WeaponBase` connect `Armas y munición` to `Movimiento del jugador`, `HUD y blackout`, `Proyectiles y charcos`, `Salud y buffs`, `Efectos de disparo`, `Ruleta y tickets`, `Interacción máquinas`, `Mira dinámica`?**
  _High betweenness centrality (0.166) - this node is a cross-community bridge._
- **Should `Armas y munición` be split into smaller, more focused modules?**
  _Cohesion score 0.06077694235588972 - nodes in this community are weakly interconnected._
- **Why does `BottleHolder` connect `Botellas y tickets` to `Movimiento del jugador`, `Armas y munición`, `HUD y blackout`, `Proyectiles y charcos`, `Salud y buffs`, `Ruleta y tickets`, `Líquido e impactos`, `Interacción máquinas`, `Barman`?**
  _High betweenness centrality (0.109) - this node is a cross-community bridge._
- **Should `Botellas y tickets` be split into smaller, more focused modules?**
  _Cohesion score 0.05454545454545454 - nodes in this community are weakly interconnected._