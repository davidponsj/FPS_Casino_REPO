# Tareas: IA Hunter + hordas

Rutas relativas a `FPS_CASINO_PROJECT/Assets/_Root_FPS_Casino/`.

## Fase 0: limpiar

- [x] **T0: Subir lo de la ruleta** (c6bea5e)
  - Acceptance: commit solo con `M_Ruleta.mat`, `Models/Props/`, `Textures/Ruleta/` (+ `.meta`); push a `origin dev-davidmallorca` tras confirmar.
  - Verify: `git status` limpio; `git log -1 --stat` solo esos archivos.
  - Files: los 6 untracked de la ruleta. · Size: XS

## Fase 1: la Hunter existe en el casino

- [x] **T1: Crear SCN_Mallorca** (0e85ab5)
  - Acceptance: `Design/Testing/SCN_Mallorca.unity` = copia de `SCN_Main` (sin copiar su `.meta`, Unity le da GUID nuevo).
  - Verify: Unity la abre y se ve igual que Main; `SCN_Main` sin cambios en `git status`.
  - Files: 1 nuevo. · Size: XS

- [x] **T2: Copiar la IA Hunter** (9735b2c, falta confirmar compilación en Unity)
  - Acceptance:
    - `Scripts/Enemies/` ← `EnemyBase`, `EnemyMovement`, `StateMachine`, `Hunter/{HunterAI, AttackSlotManager, CrowdSolver}` con `.meta`.
    - `Prefabs/Enemies/` ← `Enemy_Base`, `Enemy_Hunter` con `.meta`; `Art/.../Materials` ← `M_Red` si no existe.
    - Capa del prefab 7 → 12 (`Enemy`); masks revisados.
  - Verify: Unity compila sin errores; el prefab Hunter abre sin "Missing script".
  - Files: ~6 scripts, 2 prefabs, 1 material. · Size: M

- [x] **T3: EnemyHealth (muere de un golpe)** (fd6311a)
  - Acceptance: `Scripts/Enemies/EnemyHealth.cs` implementa `IDamageable`, evento `OnDied`, `Destroy` al primer daño; añadido a `Enemy_Base.prefab` (la Hunter lo hereda).
  - Verify: Hunter arrastrada a SCN_Mallorca → un disparo la elimina; sin errores.
  - Files: 1 nuevo + prefab Hunter. · Size: S

### Checkpoint A
- [ ] Compila limpio · Hunter en SCN_Mallorca persigue al jugador (tras hornear NavMesh) y muere de un tiro · revisar contigo.

## Fase 2: rondas

- [x] **T4: RoundManager + RoundHUD** (8e774d5) — sin Hordas.prefab: los spawns son de la escena, se montan en T5
  - Acceptance:
    - `Scripts/Enemies/RoundManager.cs`: total ronda N = `enemigosRonda1 + extraPorRonda*(N-1)` (6, +3); máx. 24 vivos; 1 s entre spawns; 10 s descanso; spawn en punto aleatorio; evento `OnRoundStarted(int)`.
    - `Scripts/UI/RoundHUD.cs`: texto "Ronda N" (patrón `PointsHUD`).
    - `Prefabs/Enemies/Hordas.prefab`: RoundManager + 4 puntos de spawn hijos + Canvas con RoundHUD.
  - Verify: compila; prefab sin referencias vacías.
  - Files: 2 scripts + 1 prefab. · Size: S-M

- [x] **T4b: Rondas tipo CoD + spawn dinámico** (d47e9aa, falta confirmar compilación en Unity) — ver "Cambio (2026-10-21)" en `SPEC.md`
  - Acceptance:
    - `RoundManager.cs`: total por ronda con la fórmula CoD (6, 8, 13, 18, 24, 27… R10 33); retraso 2 s × 0,95 por ronda, mín. 0,08 s.
    - Spawn en anillo 10–25 m alrededor del jugador: en NavMesh, fuera de la vista, camino ≤ 1,5 × máx., direcciones separadas ≥ 45° de los últimos 3; fallback al más lejano.
    - Se quitan `spawnPoints`, `enemiesFirstRound`, `extraEnemiesPerRound`, `secondsBetweenSpawns`. `RoundHUD` sin cambios.
    - Sin asignaciones por spawn/frame; `Debug.Assert` de la curva en editor.
  - Verify: compila sin errores; en Play no salta ningún Assert; las pruebas de T5.
  - Files: `Scripts/Enemies/RoundManager.cs`. · Size: S-M

- [x] **T5: Montaje en SCN_Mallorca** (42e9389) — probado en Play: funciona
  - [x] NavMeshSurface horneado (Humanoid, Physics Colliders); compila y Hunter sin "Missing script".
  - [x] Objeto `RoundManager` con `Enemy_Hunter` asignado (sin puntos de spawn).
  - [x] `RoundHUD` + `RoundText` dentro de `UI_Canva.prefab` (renombrado de `Canvas.prefab`, mismo GUID); texto vacío por defecto.
  - [x] `AttackSlotManager` + `CrowdSolver` en `Player.prefab` (decisión B: avisar al equipo en el PR).
  - Verify: las 6 pruebas manuales de `SPEC.md`.
  - Files: `SCN_Mallorca.unity`. · Size: S

### Checkpoint final
- [x] 6 pruebas de SPEC.md pasan · `git diff --stat` sin cambios en `.cs/.prefab/.unity` previos · commit (preguntar push).
