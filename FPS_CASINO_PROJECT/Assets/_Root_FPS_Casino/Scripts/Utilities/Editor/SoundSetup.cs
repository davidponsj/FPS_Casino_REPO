#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Tools → Casino → Colocar sonidos. Coloca de una vez todos los sonidos del paquete de audio:
/// 1. Ajustes de importación de cada sonido (música en streaming, bucles comprimidos, efectos cortos descomprimidos,
///    los de 3D en mono).
/// 2. Prefabs: armas (disparo y recarga), jugador (botella), PF_BottleBreak (romperse), robots (su sonido), barman (voz)
///    y el roce del slide (PlayerFootsteps).
/// 3. Escenas SCN_MainMenu y SCN_Gameplay: objeto "Musica" (música, ambiente y música al morir), jazz del VIP y sonido
///    del hitmarker. Las abre, las cambia y las guarda.
/// Se puede pasar varias veces: no duplica nada. Al acabar escribe en la consola todo lo que ha puesto.
/// </summary>
public static class SoundSetup
{
    private const string Root = "Assets/_Root_FPS_Casino";

    private static readonly (string prefab, string shoot, string reload)[] Weapons =
    {
        ("PF_Escopeta", "SFX_Escopeta_Shoot", "SFX_Escopeta_Reload"),
        ("PF_Lanzacartas", "SFX_Lanzacartas_Shoot", "SFX_Lanzacartas_Reload"),
        ("PF_Subfusil", "SFX_Subfusil_Shoot_01|SFX_Subfusil_Shoot_02", "SFX_Subfusil_Reload"),
        ("PF_Sniper", "SFX_Sniper_Shoot", "SFX_Sniper_Reload"),
        ("PF_Lanzagranadas", "SFX_Lanzagranadas_Shoot", "SFX_Lanzagranadas_Reload"),
    };

    private static readonly string[] Mono3D = { "SFX_Robot_Security", "SFX_Bottle_Break", "AMB_JazzVIP_Loop", "SFX_Bartender_Talk" };

    private static StringBuilder log;
    private static int warnings;
    private static AudioMixerGroup musicGroup, sfxGroup;

    [MenuItem("Tools/Casino/Colocar sonidos")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        log = new StringBuilder("[Sonidos] Resultado:\n");
        warnings = 0;

        try
        {
            AssetDatabase.StartAssetEditing();
            ImportSettings();
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();

        FindMixerGroups();
        SetupWeapons();
        SetupPlayerAndBottles();
        SetupRobots();
        SetupBartender();
        SetupScenes();

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        EditorUtility.DisplayDialog("Colocar sonidos", warnings == 0 ? "Hecho. Mira la consola para ver todo lo que se ha puesto."
            : $"Hecho con {warnings} avisos. Mira la consola.", "Vale");
    }

    // ---------- 1. Importación ----------
    private static void ImportSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!(name.StartsWith("MUS_") || name.StartsWith("AMB_") || name.StartsWith("SFX_"))) continue;
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;

            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            bool isMusic = name.StartsWith("MUS_");
            bool isLong = name.StartsWith("AMB_") || name.EndsWith("_Loop") || name.EndsWith("_Talk") || name == "SFX_Robot_Security";
            s.loadType = isMusic ? AudioClipLoadType.Streaming : isLong ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = isMusic ? 0.6f : 0.75f;
            importer.defaultSampleSettings = s;
            importer.forceToMono = Mono3D.Contains(name);
            importer.loadInBackground = isMusic;
            importer.SaveAndReimport();
        }
        log.AppendLine("· Ajustes de importación puestos en Audio/.");
    }

    private static void FindMixerGroups()
    {
        string guid = AssetDatabase.FindAssets("AM_Main t:AudioMixer").FirstOrDefault();
        var mixer = guid != null ? AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        musicGroup = mixer != null ? mixer.FindMatchingGroups("Music").FirstOrDefault() : null;
        sfxGroup = mixer != null ? mixer.FindMatchingGroups("SFX").FirstOrDefault() : null;
        if (musicGroup == null || sfxGroup == null) Warn("No encuentro los grupos Music/SFX de AM_Main: los sonidos irán sin grupo.");
    }

    // ---------- 2. Prefabs ----------
    private static void SetupWeapons()
    {
        foreach (var entry in Weapons)
        {
            string prefab = entry.prefab, shoot = entry.shoot, reload = entry.reload;
            EditPrefab(prefab, root =>
            {
                var weapon = FindComponent(root, "WeaponBase");
                var anim = FindComponent(root, "WeaponAnimator");
                if (weapon == null || anim == null) { Warn($"{prefab}: sin WeaponBase o WeaponAnimator."); return false; }

                if (!weapon.TryGetComponent(out AudioSource _)) log.AppendLine($"  {prefab}: añadido AudioSource.");
                AudioSource source = GetOrAdd<AudioSource>(weapon.gameObject);
                Setup2D(source, sfxGroup);

                var so = new SerializedObject(anim);
                SetClips(so.FindProperty("fireClips"), shoot.Split('|').Select(Clip).ToArray());
                SerializedProperty reloads = so.FindProperty("reloadSounds");
                reloads.arraySize = 1;
                SerializedProperty r = reloads.GetArrayElementAtIndex(0);
                r.FindPropertyRelative("clip").objectReferenceValue = Clip(reload);
                r.FindPropertyRelative("atProgress").floatValue = 0f; // dura lo mismo que la recarga del arma
                r.FindPropertyRelative("volume").floatValue = 1f;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"· {prefab}: disparo {shoot.Replace('|', '+')}, recarga {reload}.");
                return true;
            });
        }
    }

    private static void SetupPlayerAndBottles()
    {
        EditPrefab("PF_Player", root =>
        {
            var holder = FindComponent(root, "BottleHolder");
            if (holder == null) { Warn("PF_Player: sin BottleHolder."); return false; }
            var audio = GetOrAdd<PlayerBottleAudio>(holder.gameObject);
            Set(audio, "takeClip", Clip("SFX_Bottle_Take"));
            Set(audio, "drinkClip", Clip("SFX_Bottle_Drink"));
            Set(audio, "output", sfxGroup);
            log.AppendLine("· PF_Player: PlayerBottleAudio (coger y beber botella).");
            return true;
        });

        EditPrefab("PF_BottleBreak", root =>
        {
            AudioSource source = GetOrAdd<AudioSource>(root);
            source.clip = Clip("SFX_Bottle_Break");
            source.playOnAwake = true;
            source.loop = false;
            source.volume = 0.9f;
            Setup3D(source, sfxGroup, 1.5f, 25f);
            log.AppendLine("· PF_BottleBreak: suena SFX_Bottle_Break al romperse.");
            return true;
        });
    }

    private static void SetupRobots()
    {
        // Primero los prefabs base y luego las variantes, para que estas lo hereden.
        var paths = PrefabsWith("EnemyHealth")
            .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadMainAssetAtPath(p)) == PrefabAssetType.Variant ? 1 : 0).ToList();
        if (paths.Count == 0) Warn("No hay prefabs de robots (con EnemyHealth).");
        foreach (string path in paths)
        {
            EditPrefabAt(path, root =>
            {
                var health = FindComponent(root, "EnemyHealth");
                if (health.GetComponentsInChildren<RobotAmbientAudio>(true).Length > 0) { log.AppendLine($"· {root.name}: ya tiene sonido de robot."); return false; }
                var audio = health.gameObject.AddComponent<RobotAmbientAudio>();
                Set(audio, "clip", Clip("SFX_Robot_Security"));
                Set(audio, "output", sfxGroup);
                log.AppendLine($"· {root.name}: RobotAmbientAudio (trozos sueltos, flojo, máx. 2 a la vez).");
                return true;
            });
        }
    }

    private static void SetupBartender()
    {
        EditPrefab("PF_Bartender", root =>
        {
            var speech = FindComponent(root, "BartenderSpeech");
            if (speech == null) { Warn("PF_Bartender: sin BartenderSpeech."); return false; }
            AudioSource source = GetOrAdd<AudioSource>(speech.gameObject);
            source.playOnAwake = false;
            Setup3D(source, sfxGroup, 1.5f, 12f);
            Set(speech, "voiceSource", source);
            Set(speech, "voiceTalk", Clip("SFX_Bartender_Talk"));
            log.AppendLine("· PF_Bartender: voz al hablar (SFX_Bartender_Talk).");
            return true;
        });

        // El roce del slide va en PlayerFootsteps (en la escena o en un prefab).
        foreach (string path in PrefabsWith("PlayerFootsteps"))
            EditPrefabAt(path, root => { Set(FindComponent(root, "PlayerFootsteps"), "slideLoop", Clip("SFX_Slide_Loop")); log.AppendLine($"· {root.name}: slide con SFX_Slide_Loop."); return true; });
    }

    // ---------- 3. Escenas ----------
    private static void SetupScenes()
    {
        foreach (string sceneName in new[] { "SCN_MainMenu", "SCN_Gameplay" })
        {
            string guid = AssetDatabase.FindAssets(sceneName + " t:Scene", new[] { Root }).FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == sceneName);
            if (guid == null) { Warn($"No encuentro la escena {sceneName}."); continue; }
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            bool menu = sceneName == "SCN_MainMenu";
            MusicPlayer music = FindInScene<MusicPlayer>(scene).FirstOrDefault();
            if (music == null)
            {
                var go = new GameObject("Musica");
                SceneManager.MoveGameObjectToScene(go, scene);
                music = go.AddComponent<MusicPlayer>();
            }
            Set(music, "music", Clip(menu ? "MUS_Menu" : "MUS_Gameplay"));
            Set(music, "deathMusic", menu ? null : Clip("MUS_Lost"));
            Set(music, "ambience", menu ? null : Clip("AMB_Casino_Loop"));
            Set(music, "musicGroup", musicGroup);
            Set(music, "sfxGroup", sfxGroup);
            log.AppendLine($"· {sceneName}: objeto Musica ({(menu ? "MUS_Menu" : "MUS_Gameplay + ambiente + MUS_Lost al morir")}).");

            if (!menu)
            {
                SetupJazz(scene);
                SetupHitMarker(scene);
                foreach (var steps in FindInScene<MonoBehaviour>(scene).Where(c => c.GetType().Name == "PlayerFootsteps"))
                    Set(steps, "slideLoop", Clip("SFX_Slide_Loop"));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void SetupJazz(Scene scene)
    {
        GameObject jazz = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).FirstOrDefault(g => g.name == "Jazz_VIP");
        if (jazz == null)
        {
            // En el centro de la zona VIP (sofás, pivotes, cordón o mesa VIP).
            var vip = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.Contains("SofaVip") || t.name.Contains("PivoteVip") || t.name.Contains("CordonVip") || t.name.Contains("MesaVip")).ToList();
            jazz = new GameObject("Jazz_VIP");
            SceneManager.MoveGameObjectToScene(jazz, scene);
            Vector3 center = vip.Count > 0 ? vip.Aggregate(Vector3.zero, (s, t) => s + t.position) / vip.Count : Vector3.zero;
            jazz.transform.position = center + Vector3.up * 1.5f;
            if (vip.Count == 0) Warn("No encuentro la zona VIP: Jazz_VIP está en (0, 1.5, 0). Muévelo a su sitio.");
        }
        AudioSource source = GetOrAdd<AudioSource>(jazz);
        source.clip = Clip("AMB_JazzVIP_Loop");
        source.loop = true;
        source.playOnAwake = true;
        source.volume = 0.6f;
        Setup3D(source, sfxGroup, 3f, 16f);
        log.AppendLine($"· SCN_Gameplay: Jazz_VIP en {jazz.transform.position} (se oye al acercarte al VIP).");
    }

    private static void SetupHitMarker(Scene scene)
    {
        var huds = FindInScene<MonoBehaviour>(scene).Where(c => c.GetType().Name == "HitMarkerHUD").ToList();
        foreach (string path in PrefabsWith("HitMarkerHUD"))
            EditPrefabAt(path, root => { SetHitMarker(FindComponent(root, "HitMarkerHUD")); log.AppendLine($"· {root.name}: hitmarker con SFX_Hit."); return true; });
        foreach (var hud in huds.Where(h => !PrefabUtility.IsPartOfPrefabInstance(h) || PrefabUtility.IsAddedComponentOverride(h)))
        {
            SetHitMarker(hud);
            log.AppendLine($"· {hud.name} (escena): hitmarker con SFX_Hit.");
        }
        if (huds.Count == 0 && PrefabsWith("HitMarkerHUD").Count == 0) Warn("No encuentro HitMarkerHUD.");
    }

    private static void SetHitMarker(Component hud)
    {
        var so = new SerializedObject(hud);
        if (so.FindProperty("audioSource").objectReferenceValue == null)
        {
            AudioSource source = GetOrAdd<AudioSource>(hud.gameObject);
            Setup2D(source, sfxGroup);
            so.FindProperty("audioSource").objectReferenceValue = source;
        }
        so.FindProperty("hitSound").objectReferenceValue = Clip("SFX_Hit");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);
    }

    // ---------- Utilidades ----------
    // TryGetComponent: en el editor GetComponent devuelve un "null falso" que el operador de nulos no detecta.
    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

    private static AudioClip Clip(string name)
    {
        string guid = AssetDatabase.FindAssets(name + " t:AudioClip", new[] { Root + "/Audio" })
            .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
        if (guid == null) { Warn($"Falta el sonido {name} en Audio/."); return null; }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static void EditPrefab(string prefabName, Func<GameObject, bool> edit)
    {
        string guid = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { Root })
            .FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == prefabName);
        if (guid == null) { Warn($"No encuentro el prefab {prefabName}."); return; }
        EditPrefabAt(AssetDatabase.GUIDToAssetPath(guid), edit);
    }

    private static void EditPrefabAt(string path, Func<GameObject, bool> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (edit(root)) PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static List<string> PrefabsWith(string typeName) =>
        AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); return go != null && go.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().Name == typeName); })
            .ToList();

    private static Component FindComponent(GameObject root, string typeName) =>
        root.GetComponentsInChildren<MonoBehaviour>(true).FirstOrDefault(c => c != null && (c.GetType().Name == typeName || IsSubclass(c.GetType(), typeName)));

    private static bool IsSubclass(Type t, string baseName)
    {
        for (Type b = t.BaseType; b != null; b = b.BaseType) if (b.Name == baseName) return true;
        return false;
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

    private static void Set(Object target, string field, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Warn($"{target.name}: no tiene el campo {field} (¿script sin actualizar?)."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetClips(SerializedProperty array, AudioClip[] clips)
    {
        array.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    private static void Setup2D(AudioSource source, AudioMixerGroup group)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = group;
    }

    private static void Setup3D(AudioSource source, AudioMixerGroup group, float min, float max)
    {
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = min;
        source.maxDistance = max;
        source.dopplerLevel = 0f;
        source.outputAudioMixerGroup = group;
    }

    private static void Warn(string message)
    {
        warnings++;
        log.AppendLine("  ⚠ " + message);
    }
}
#endif
