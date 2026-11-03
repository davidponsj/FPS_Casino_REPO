# Plan: Robot de seguridad — hundirse al morir y luces emisivas

Rama: `dev-davidmallorca_V3`. Plan cerrado con David el 2026-10-30 (decisiones abajo). Para empezar: `/build` (o `/build auto`).
(El plan anterior, el de la limpieza, está en git: commit 3168895.)

## Contexto (ya hecho y funcionando, no tocar)
- El enemigo es `PF_EnemyHunter` (variante de `PF_EnemyBase`). Dentro lleva el modelo `AC_RobotSeguridad_v3` (FBX en
  `Animation/Characters/Enemies/`), con su Animator (`ACR_RobotSeguridad`: Start → Walk ⇄ Attack, Any State → Death con trigger `Die`).
- El `SkinnedMeshRenderer` del robot tiene 3 huecos de material: **0 = M_RobotSeguridad, 1 = M_RobotPinganillo, 2 = M_RobotBoca**
  (en `Art/3D/Materials/Enemies/`). Pinganillo y boca **ya son emisivos** (URP Lit, `_EMISSION` activado,
  `_EmissionColor` ≈ (1.61, 1.61, 1.61), mapa de emisión = textura de luces). El color lo da la textura: no se cambia.
- `EnemyHealth.TakeDamage`, al morir: `OnDied` → `OnAnyDied` → `StopBody()` (apaga TODOS los `Behaviour` de la raíz menos
  él mismo, y todos los colliders de los hijos) → trigger `Die` → `Destroy(gameObject, corpseSeconds)`.
- No hay AudioSource en el robot (aún no hay voces).

## Decisiones de David
1. **Desaparecer:** tras `corpseSeconds` en el suelo, el cuerpo **se hunde** en el suelo y luego se destruye. (Un chispazo VFX, más adelante, en otra tarea.)
2. **Pinganillo:** parpadeo **seco** encendido/apagado, como una sirena, desincronizado entre robots.
3. **Boca:** brilla según el **volumen de la voz** (AudioSource). Sin voz, apagada.
4. **Al morir se apagan las dos luces.**
5. **Color:** el de la textura tal cual. Solo se cambia la intensidad de la emisión (encender/apagar).

## Reglas (de CLAUDE.local.md, recordatorio)
- Código en inglés, comentarios/tooltips en español. `[Header]` por grupos, `[Tooltip]` con unidad y ejemplo, `[Min]`/`[Range]`.
- Casilla `showDebug` (apagada) + `DebugLabels.Above(...)` dentro de `#if UNITY_EDITOR`. En consola solo `LogWarning` si falta una referencia.
- Sin asignaciones de memoria por frame (buffers reutilizados).
- **Codificación:** `EnemyHealth.cs`, `DebugLabels.cs` y los archivos nuevos en **UTF-8 sin BOM**.
- **No tocar prefabs ni escenas**: los pasos del Editor los hace David (lista al final).
- Verificación con **Unity cerrado**: `bash tasks/compile.sh` y `bash tasks/test.sh` desde la raíz del repo.

---

## T1 · EnemyHealth: hundirse antes de desaparecer
**Archivo:** `Scripts/Characters/Enemies/EnemyHealth.cs`.

- Grupo `[Header("Muerte")]`, después de `corpseSeconds`, dos campos nuevos:
  - `sinkSeconds` (float, `[Min(0f)]`, por defecto **1**). Tooltip: "Segundos que tarda el cuerpo en hundirse en el suelo después de Corpse Seconds. 0 = desaparece de golpe. 1 = baja despacio, casi no se nota."
  - `sinkMeters` (float, `[Min(0f)]`, por defecto **1.5**). Tooltip: "Metros que baja el cuerpo al hundirse. Más que su altura, para que no asome nada. 1.5 = un robot de ~1,5 m."
- Ajustar el tooltip de `corpseSeconds`: "Segundos que se queda el cuerpo en el suelo antes de hundirse. 0 = se hunde (o desaparece) al instante."
- En `TakeDamage`, cambiar `Destroy(gameObject, corpseSeconds);` por `StartCoroutine(SinkAndDestroy());`.
- Corrutina nueva `SinkAndDestroy()`:
  1. `yield return new WaitForSeconds(corpseSeconds)` (si es 0, no esperar).
  2. Si `sinkSeconds > 0`: mover `transform.position` hacia abajo de forma lineal `sinkMeters` en `sinkSeconds` (con `Time.deltaTime`).
     Comentario de una línea: el NavMeshAgent ya está apagado (StopBody), por eso aquí sí se puede mover el transform.
  3. `Destroy(gameObject)`.
- Actualizar el `<summary>` de la clase: "...deja el cuerpo Corpse Seconds en el suelo, lo hunde Sink Meters en Sink Seconds y se destruye."
- Depuración (`OnDrawGizmos`): cuando está muerto, `"Vida · muerto"` → `"Vida · muerto · hundiéndose"` mientras baja (basta un bool privado `sinking`).

**Hecho cuando:** compila; con `corpseSeconds = 3` y `sinkSeconds = 1` el robot hace la animación de muerte, se queda 3 s y se hunde en 1 s;
con `corpseSeconds = 0` y `sinkSeconds = 0` desaparece al instante como antes. `RoundManager` y `KillScore` siguen igual (los eventos no cambian de sitio).

## T2 · RobotLights: pinganillo parpadeante y boca que habla
**Archivo nuevo:** `Scripts/Characters/Enemies/RobotLights.cs` (va en la **raíz** del robot, junto a EnemyHealth).

Comportamiento:
- **Cómo cambia la emisión:** con `MaterialPropertyBlock` por hueco (`renderer.SetPropertyBlock(block, materialIndex)`), poniendo
  `_EmissionColor = colorOriginalDelMaterial * intensidad`. El color original se lee una vez en `Awake` de
  `renderer.sharedMaterials[index].GetColor("_EmissionColor")`. Intensidad 0 = apagada, 1 = como el material. **No crear materiales.**
  Solo llamar a `SetPropertyBlock` cuando la intensidad cambie (el pinganillo cambia 2 veces por ciclo, no cada frame).
- **Pinganillo:** encendido `blinkOnSeconds`, apagado `blinkOffSeconds`, en bucle. Desfase al azar por robot (`Random.value * ciclo`) en `Awake`.
- **Boca:** si `voiceSource` está sonando, lee `GetOutputData` en un buffer reutilizado (`float[256]`), calcula el RMS y
  `objetivo = Clamp01(rms * mouthSensitivity) * mouthMaxIntensity`; suavizar con `1 - Exp(-mouthSmoothing * dt)` (como el resto del proyecto).
  Si no suena o no hay `voiceSource`, objetivo 0.
- **Apagar al morir:** en `OnDisable`, poner las dos luces a 0. Como `EnemyHealth.StopBody()` apaga todos los Behaviour de la raíz,
  al morir las luces se apagan solas sin código extra en EnemyHealth.
- Si falta el renderer o un índice no existe → `LogWarning` en `Awake` y `enabled = false`.

Inspector:
- `[Header("Referencias")]`
  - `bodyRenderer` (`Renderer`): "Renderer del robot (el SkinnedMeshRenderer del modelo). Vacío = el primero que encuentre en los hijos."
  - `voiceSource` (`AudioSource`): "AudioSource de la voz del robot. La boca brilla con su volumen. Vacío = la boca no se enciende."
- `[Header("Pinganillo")]`
  - `pinganilloMaterialIndex` (int, `[Min(0)]`, **1**): "Hueco del material del pinganillo en el renderer. 1 = M_RobotPinganillo."
  - `blinkOnSeconds` (float, `[Min(0.01f)]`, **0.4**): "Segundos encendido en cada parpadeo. 0.4 = destello de sirena."
  - `blinkOffSeconds` (float, `[Min(0.01f)]`, **0.6**): "Segundos apagado entre parpadeos. 0.6 = un parpadeo por segundo."
  - `pinganilloIntensity` (float, `[Min(0f)]`, **1**): "Brillo encendido respecto al material. 1 = como el material, 3 = el triple."
- `[Header("Boca")]`
  - `mouthMaterialIndex` (int, `[Min(0)]`, **2**): "Hueco del material de la boca en el renderer. 2 = M_RobotBoca."
  - `mouthMaxIntensity` (float, `[Min(0f)]`, **1.5**): "Brillo de la boca cuando habla fuerte, respecto al material. 1.5 = algo más que el material."
  - `mouthSensitivity` (float, `[Min(0f)]`, **8**): "Cuánto se enciende con poco volumen. Alto = brilla con voz baja. 8 = una voz normal la enciende entera."
  - `mouthSmoothing` (float, `[Min(0.1f)]`, **20**): "Rapidez con la que sigue a la voz. Alto = nervioso, bajo = suave. 20 = sigue las sílabas."
- `[Header("Depuración")]` `showDebug`: "Muestra sobre el robot el brillo del pinganillo y de la boca, y si está hablando."
  Texto: `"Luces · pinganillo {on/off} · boca {intensidad:0.00} · {hablando/callado}"` en `DebugLabels.RobotLightsLine`.

Para el test, dos funciones `public static`:
- `bool IsBlinkOn(float time, float onSeconds, float offSeconds, float offset)`.
- `float Loudness(float[] samples, int count)` → RMS.

**Hecho cuando:** compila; en Play el pinganillo parpadea seco y cada robot a su ritmo; la boca está apagada (no hay voz);
al morir se apagan las dos; con un AudioSource de prueba con un clip en loop, la boca brilla con el volumen.

## T3 · DebugLabels + test
- `Scripts/Utilities/DebugLabels.cs`: añadir `public const int RobotLightsLine = 4;` debajo de `EnemyAttackLine`.
- `Scripts/Tests/Editor/RobotLightsTests.cs` (NUnit, sin asmdef, mismo estilo que `KillScoreTests`):
  - `IsBlinkOn`: con on 0.4 / off 0.6 / offset 0 → t=0.1 encendido, t=0.5 apagado, t=1.1 encendido; con offset 0.5 → t=0.1 apagado.
  - `Loudness`: silencio (todo 0) → 0; señal constante 0.5 → 0.5; `count` menor que el array solo cuenta esos.

**Hecho cuando:** `bash tasks/test.sh` en verde (Unity cerrado).

## Checkpoint
1. `bash tasks/compile.sh` sin `error CS` y `bash tasks/test.sh` en verde.
2. Commit en `dev-davidmallorca_V3` (comprobar la rama antes) y push a esa rama. Mensaje corto en español,
   p. ej. "Robot: se hunde al morir y luces emisivas (pinganillo y boca)".
3. **No incluir** en el commit `PF_EnemyHunter.prefab` si sigue con el override `maxHealth: 10` de pruebas: preguntar a David.
   Los assets del robot sin commitear (`Art/3D/Models|Textures|Materials/...`, `Animation/`, `PF_EnemyBase`, `PF_EnemyHunter`)
   van en un commit aparte solo si David lo confirma.

## Pasos del Editor (los hace David después del build)
1. Abrir `PF_EnemyHunter`, seleccionar la **raíz** → Add Component → **Robot Lights**. Dejar los valores por defecto
   (`Body Renderer` vacío lo busca solo). Comprobar en el Skinned Mesh Renderer del modelo que el hueco 1 es el pinganillo y el 2 la boca.
2. (Para cuando haya voces) Add Component → **Audio Source** en la raíz: Play On Awake desmarcado, Spatial Blend = 1 (3D).
   Arrastrarlo a `Voice Source` de Robot Lights.
3. Para el halo de luz hace falta **Bloom**: si las luces solo se ven más claras pero sin halo, añadir en la escena
   GameObject → Volume → Global Volume con un perfil que tenga Bloom (p. ej. `Assets/Settings/DefaultVolumeProfile`).
4. Probar en SCN_Mallorca: parpadeo, muerte (luces fuera → 3 s en el suelo → se hunde), y la boca con un clip de prueba en el AudioSource.

## Riesgos
| Riesgo | Cómo se cubre |
|---|---|
| `EnemyAI` (solo con su depuración activa) pinta el color con un PropertyBlock de todo el renderer; los de RobotLights son por hueco y pueden taparlo en boca y pinganillo | Solo afecta a la depuración. Aceptado. |
| Al hundirse, el cuerpo atraviesa el suelo con el Rigidbody cinemático | Cinemático no colisiona; sin problema. Colliders ya apagados. |
| `GetOutputData` sin voz devuelve ceros | La boca se queda a 0, que es lo esperado. |
