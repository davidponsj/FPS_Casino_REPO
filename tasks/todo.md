# TODO: Limpieza y reorganización

Detalle de cada tarea (qué, criterios "Hecho cuando", dependencias) en `tasks/plan.md`. Spec en `SPEC.md`.
**Estado (2026-10-22):** plan revisado (criterios de aceptación, tareas de ≤5 archivos, checkpoints), falta que David lo apruebe. Nada del proyecto tocado. Siguiente paso: `/build`.

## 0. tooling
- [x] T0.1 Script check_refs + línea base
- [x] T0.2 Compilar en batch
- [x] T0.3 Tests EditMode (o self-checks si no se puede)
- [x] **Checkpoint 0:** comandos probados + revisión con David

## 1. folders
- [x] T1.1 Corregir faltas en nombres de carpetas
- [ ] **Checkpoint 1:** V + commit

## 2. move-scripts
- [x] T2.1 Mover scripts a su carpeta
- [x] T2.2 HordeAttackSlots, HordeCollision, RouletteTicketDispenser, MenuArrow
- [x] T2.3 AmmoHUD, HealthBarHUD, BlackoutBarHUD, ReloadHUD
- [ ] **Checkpoint 2:** V + commit

## 3. rename-assets
- [x] T3.1 Ruleta
- [x] T3.2 Lanzacartas y botella
- [x] T3.3 ScriptableObjects, M_Liquid, T_InputKeys
- [ ] **Checkpoint 3:** V (modelos y materiales iguales) + commit

## 4. code-core
- [x] T4.1 WeightedRandom + test
- [x] T4.2 Puntos (D2, B4)
- [x] T4.3 Player (sin controller): summary, depuración, Inspector
- [x] T4.4 PlayerController: Inspector + depuración
- [ ] **Checkpoint 4:** V + commit

## 5. code-weapons (riesgo alto)
- [x] Apuntar valores del Inspector de las 4 armas hitscan (tasks/weapon_values_before.txt)
- [x] T5.1 HitscanWeapon
- [x] T5.2 GUIDs en prefabs + borrar las 3 armas viejas
- [x] T5.3 Código muerto + OnWeaponChanged
- [x] T5.4 Inspector + depuración de armas
- [x] T5.5 Inspector de efectos (MuzzleFlash, Tracer, Impact)
- [ ] **Checkpoint 5:** V + las 5 armas como antes + commit

## 6. code-casino
- [x] T6.1 TicketData con su BottleEffect, fuera Library
- [ ] T6.2 Regla única de bloqueo de la ruleta
- [ ] T6.3 Fuera TicketPickup
- [ ] T6.4 Inspector + depuración: ruleta, dispensador, barman
- [ ] T6.5 Inspector + depuración: tragaperras
- [ ] **Checkpoint 6:** V + ciclo completo del casino + commit

## 7. code-bottles
- [ ] T7.1 Input Drink/Throw (+ David conecta los eventos)
- [ ] T7.2 Código muerto + Inspector + depuración
- [ ] **Checkpoint 7:** V + las 6 botellas + commit

## 8. code-enemies (riesgo alto)
- [ ] Apuntar valores del Inspector de PF_EnemyHunter
- [ ] T8.1 EnemyAI (EnemyBase + HunterAI)
- [ ] T8.2 Charco ralentiza (IMovementModifiable) + código muerto
- [ ] T8.3 Inspector + depuración: EnemyAI, EnemyMovement, EnemyHealth
- [ ] T8.4 Inspector + depuración: RoundManager, HordeAttackSlots, HordeCollision, StateMachine
- [ ] **Checkpoint 8:** V + 3 rondas + commit

## 9. code-ui-menu
- [ ] T9.1 HUDs parte 1
- [ ] T9.2 HUDs parte 2 + MenuArrow
- [ ] T9.3 Botón Salir (B5) + ATMScreen + MenuCameraMover
- [ ] T9.4 MenuHoverManager, HoverOutline, CameraFocusTarget
- [ ] **Checkpoint 9:** V + menú completo + commit

## 10. docs
- [ ] T10.1 CLAUDE.local.md
- [ ] T10.2 /graphify Assets (David)
- [ ] T10.3 Criterios de éxito
- [ ] **Checkpoint final:** revisión con David antes del push/PR

## Checklist manual (tras cada bloque, en SCN_Gameplay / SCN_Mallorca / SCN_MainMenu)
- [ ] Moverse, sprint, slide, wallrun, walljump
- [ ] Coger arma de pared; disparar las 5 armas; recargar; retícula y munición en el HUD
- [ ] Ruleta → ticket → barman → beber (F) / lanzar (Q) → charco → ruleta de nuevo
- [ ] Tragaperras: apostar, ganar o perder, puntos en el HUD
- [ ] Rondas: spawns, muertes de un golpe, contador de ronda
- [ ] Menú: hover, zoom al cajero, opciones, Salir
- [ ] Consola limpia al jugar; con "Mostrar depuración" activa se ve el estado en la Scene
