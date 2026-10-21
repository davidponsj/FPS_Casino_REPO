# Spec: IA Hunter + hordas por rondas (FPS Casino)

## Objetivo
Traer la IA **Hunter** del proyecto `Practica_IAs` al casino y hacer que aparezca por **rondas estilo CoD Zombies**: cada ronda salen más enemigos, todos iguales de fuertes. De momento cada enemigo **muere de un disparo** para poder probar que las rondas avanzan.

Usuario: el equipo, probando en la escena `SCN_Mallorca`.

## Supuestos (corrígeme si alguno está mal)
1. "Mueren de un disparo" = cualquier daño (bala, granada, charco de botella) mata al enemigo, sin importar la cantidad.
2. La Hunter **no pega** todavía: solo persigue y rodea al jugador. El ataque irá en otra tarea.
3. Matar enemigos **no da puntos** todavía (los puntos irán por tipo de baja: headshot, 360…).
4. El jugador no muere / no hay game over todavía.
5. El enemigo muerto simplemente desaparece (`Destroy`), sin animación ni ragdoll.
6. El prefab de la Hunter es provisional (el de práctica, cápsula con material).

## Tech stack
- Unity **6000.3.24f1** (igual en ambos proyectos), URP 17.3.0.
- `com.unity.ai.navigation` 2.0.14 (ya instalado en el casino: NavMesh, `NavMeshSurface`).
- TextMeshPro para el contador de ronda (como `PointsHUD`).
- Sin paquetes nuevos.

## Comandos
- Abrir: Unity Hub → `FPS_CASINO_PROJECT` con 6000.3.24f1.
- Probar: abrir `Assets/_Root_FPS_Casino/Design/Testing/SCN_Mallorca.unity` → Play.
- Comprobar rama: `git branch --show-current` (debe ser `dev-davidmallorca`).
- Subir: `git push origin dev-davidmallorca` (siempre preguntando antes).

## Estructura
```
Assets/_Root_FPS_Casino/
  Scripts/Enemies/            ← copiados de Practica_IAs (con .meta, mismos GUID)
    EnemyBase.cs  EnemyMovement.cs  StateMachine.cs
    Hunter/HunterAI.cs  Hunter/AttackSlotManager.cs  Hunter/CrowdSolver.cs
    EnemyHealth.cs            ← NUEVO: IDamageable, muere al primer impacto, avisa al morir
    RoundManager.cs           ← NUEVO: rondas, spawn, descanso entre rondas
  Scripts/UI/RoundHUD.cs      ← NUEVO: contador de ronda (TMP)
  Prefabs/Enemies/            ← Enemy_Base.prefab + Enemy_Hunter.prefab (variante) + materiales que usen
  Design/Testing/SCN_Mallorca.unity  ← copia de SCN_Main, aquí se monta todo
```
No se copia: Patroller, `PlayerController` de práctica, escenas, Settings de práctica.

## Diseño
- **EnemyHealth** (en el prefab Hunter): implementa `IDamageable`. `TakeDamage` → si no está muerto: evento `OnDied`, `Destroy(gameObject)`. Las armas ya buscan `GetComponentInParent<IDamageable>()` (`WeaponBase.cs:228`), así que no se tocan.
- **RoundManager** (en escena):
  - Campos de Inspector: prefab enemigo, lista de puntos de spawn, enemigos en ronda 1, enemigos extra por ronda, máximo vivos a la vez, segundos entre spawns, segundos de descanso entre rondas.
  - Ronda N: total = `enemigosRonda1 + extraPorRonda * (N-1)`. Spawnea de uno en uno en un punto aleatorio hasta el total, sin pasar del máximo vivos.
  - Cuando total spawneado y vivos = 0 → descanso → ronda N+1.
  - Evento `OnRoundStarted(int)` para el HUD.
- **RoundHUD**: escucha `OnRoundStarted` y pinta el número (patrón de `PointsHUD`).
- **Jugador en SCN_Mallorca**: añadir `AttackSlotManager` y `CrowdSolver` como override en la instancia de la escena (el prefab del jugador no se toca). Comprobar tag `Player`.
- **NavMesh**: `NavMeshSurface` horneado en `SCN_Mallorca`.

## Estilo de código
Igual que el resto del casino: comentarios y nombres de campos en español, `[SerializeField] private`, `[Header]`, eventos `Action`, resumen `/// <summary>` arriba.
```csharp
/// <summary>
/// Vida del enemigo. De momento muere al primer impacto, sea del daño que sea.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public event Action<EnemyHealth> OnDied;
    public bool IsDead { get; private set; }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        IsDead = true;
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }
}
```

## Cambio (2026-10-21): rondas tipo CoD + spawn dinámico
Sustituye a "lista de puntos de spawn" y "total lineal" del diseño de arriba. Solo cambia `RoundManager.cs`.

**Rondas (fórmula de CoD Zombies en solitario, fija en código):**
- `mult = max(1, N/5)`; si `N >= 10` → `mult *= N * 0.15`; `total = 24 + floor(3 * mult)`.
- Rondas 1–5: `total *= 0.25 / 0.3 / 0.5 / 0.7 / 0.9` (floor).
- Esperado: R1 6 · R2 8 · R3 13 · R4 18 · R5 24 · R6 27 · R10 33 · R20 60 · R30 105.
- Retraso entre spawns: 2 s en ronda 1, `× 0.95` cada ronda, mínimo 0,08 s (Inspector).
- Se mantiene: máx. 24 vivos (se rellena al matar), 10 s de descanso, evento `OnRoundStarted`.
- Autocomprobación: `Debug.Assert` en editor de los valores esperados.

**Spawn dinámico (sin puntos colocados a mano).** Por cada enemigo, hasta 10 candidatos:
1. Punto aleatorio en un anillo alrededor del jugador (10–25 m, Inspector).
2. `NavMesh.SamplePosition` → si no hay NavMesh cerca, descartar.
3. Descartar si el jugador lo ve: dentro del viewport de `Camera.main` **y** `Linecast` cámara → punto (+altura) sin obstáculo. Máscara configurable sin la capa `Enemy`.
4. Descartar si el camino por NavMesh no existe o mide más de `maxDistancia × 1,5` (evita "al otro lado de la pared"; las puertas cerradas futuras lo bloquean solas).
5. Descartar si la dirección está a menos de 45° de los últimos 3 spawns (llegan de todas partes).
6. Si ninguno pasa: usar el candidato válido en NavMesh más lejano (la ronda nunca se atasca).
- Sin asignaciones por frame: `NavMeshPath` y buffer de esquinas reutilizados.
- Gizmos: anillos mínimo/máximo alrededor del jugador y últimos puntos de spawn.

## Pruebas
- Manual en Play (no hay tests automáticos en el proyecto):
  1. Empieza la ronda 1 y el HUD dice "Ronda 1".
  2. Salen N enemigos (6 en ronda 1) fuera de la vista, desde direcciones distintas, y persiguen al jugador sin atravesarse.
  3. Un disparo de cualquier arma mata a un enemigo.
  4. Al matar al último → descanso → "Ronda 2" con más enemigos.
  5. Nunca hay más vivos que el máximo.
  6. Consola sin errores ni warnings de "no se encontró Player".

## Límites
- **Siempre:** trabajar en `dev-davidmallorca` (comprobar antes de cada commit); copiar con `.meta`; commits cortos en español.
- **Preguntar antes:** cualquier push; tocar `SCN_Main` u otros `.unity`/`.prefab` existentes; ProjectSettings/Packages; subir este `SPEC.md` al repo.
- **Nunca:** modificar scripts que ya existen en el casino (trabajo de David Sáez en paralelo); tocar ramas de otros, `main` o `dev`; force push / reset --hard.

## Orden de trabajo
0. Commit + push de lo de la ruleta (material, texturas, props), solo esos archivos.
1. Copiar `SCN_Main` → `Design/Testing/SCN_Mallorca.unity`.
2. Copiar scripts y prefabs de la Hunter.
3. `EnemyHealth` en el prefab Hunter.
4. `RoundManager` + `RoundHUD`.
5. Montar en `SCN_Mallorca`: NavMesh, puntos de spawn, componentes del jugador, HUD.

## Criterios de éxito
- Las 6 comprobaciones de "Pruebas" pasan en `SCN_Mallorca`.
- `git diff` no muestra cambios en ningún `.cs`, `.prefab` ni `.unity` que ya existiera (excepto los de la ruleta del paso 0).

## Preguntas abiertas
1. Valores por defecto propuestos (ajustables en Inspector): 6 enemigos en ronda 1, +3 por ronda, máx. 24 vivos, 1 s entre spawns, 10 s de descanso. ¿Vale?
2. Puntos de spawn: ¿dónde los pongo en `SCN_Mallorca` (bordes del mapa, puertas…), o pongo unos cuantos y los recolocas tú?
3. Algunos pasos (hornear NavMesh, colocar spawns y HUD en la escena) se hacen mejor en el Editor. Yo puedo crear los scripts y prefabs; ¿montas tú la escena siguiendo mis pasos, o intento editar el `.unity` a mano?
