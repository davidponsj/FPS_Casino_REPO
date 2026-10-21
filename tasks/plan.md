# Plan: IA Hunter + hordas por rondas

Basado en `SPEC.md` (aprobado). Rama: `dev-davidmallorca`.

## Resumen
Copiar la Hunter de `Practica_IAs` al casino, darle una vida de un golpe (`EnemyHealth`) y hacerla aparecer por rondas con `RoundManager` + `RoundHUD`. Todo se prueba en `SCN_Mallorca` (copia de `SCN_Main`).

## Decisiones
- **Copiar con `.meta`** → se mantienen los GUID y el prefab Hunter (variante de `Enemy_Base`) sigue apuntando a sus scripts y a `M_Red`.
- **Capa del enemigo**: en práctica es la 7 (`Enemy`), pero en el casino la 7 es `Wall` (el jugador haría wallrun sobre ellos). El casino ya tiene `Enemy` en la capa **12** → cambio `m_Layer: 7 → 12` en el prefab copiado. ProjectSettings no se toca.
- **Las armas no se tocan**: `hitMask = ~0` y `GetComponentInParent<IDamageable>()` ya alcanzan al enemigo.
- **Montaje de escena en el Editor (tú)**: hornear NavMesh y añadir componentes en escena es fiable en el Editor y frágil editando YAML. Yo dejo todo listo en un prefab `Hordas.prefab` (RoundManager + puntos de spawn hijos + Canvas con RoundHUD) y una lista de pasos cortos. Tú: arrastrar el prefab, hornear, añadir 2 componentes al jugador.
- **El tag `Player`** ya está en `Prefabs/Characters/Player.prefab` → `EnemyBase.FindPlayer()` funciona sin cambios.

## Grafo de dependencias
```
T0 ruleta (independiente)
T1 SCN_Mallorca ─────────────────────────┐
T2 copiar IA (scripts + prefabs + capa) ─┼─→ T3 EnemyHealth ─→ T4 RoundManager+RoundHUD+Hordas.prefab ─→ T5 montaje (Editor)
```

## Riesgos
| Riesgo | Impacto | Mitigación |
|---|---|---|
| Nombres de clase chocan con el casino (`StateMachine`, `State<T>`, `EnemyBase`) | Alto: no compila | Comprobado al copiar con grep; si choca, renombrar solo en los nuevos |
| Capas/masks serializados en `EnemyMovement`/`CrowdSolver` apuntan a capas de práctica | Medio: se atraviesan o no detectan | Revisar masks del prefab al copiar y ajustarlos a capas del casino |
| `SCN_Main` cambia en `dev` después de copiarla | Bajo | `SCN_Mallorca` es solo de pruebas; se recopia si hace falta |
| Charcos/granadas matan de un golpe también | Bajo (esperado) | Aceptado en el spec |

## Tareas
Ver `tasks/todo.md`.
