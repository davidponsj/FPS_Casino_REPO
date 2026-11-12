using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
#endif

/// <summary>
/// Animaciones del barman. Escucha al Bartender (no hay que conectar nada más):
/// - Normal: AC_IDLE en bucle.
/// - Al darle el ticket: saca la botella (AC_BOTTLE) y se queda con ella en la mano (AC_BOTTLE_idle en bucle) hasta que
///   vuelves a pulsar E para cogerla; entonces vuelve a la pose normal con AC_BottleidleToIdle.
/// - Si pulsas E sin ticket (o con una botella aún en la mano): AC_NOTICKET, el "no" (con un tiempo mínimo entre uno y
///   otro para que no repita si machacas la E).
/// El botón "Crear Animator Controller" del Inspector monta ACR_Bartender con esos estados y transiciones, marca en
/// bucle los idles del FBX y se lo pone al Animator: no hay que tocar el Animator a mano.
/// </summary>
[RequireComponent(typeof(Bartender))]
public class BartenderAnimation : MonoBehaviour
{
    private static readonly int GiveHash = Animator.StringToHash("Give");
    private static readonly int RefuseHash = Animator.StringToHash("Refuse");
    private static readonly int HoldingHash = Animator.StringToHash("Holding");

    private const string IdleClip = "AC_IDLE";
    private const string BottleClip = "AC_BOTTLE";
    private const string BottleIdleClip = "AC_BOTTLE_idle";
    private const string BottleToIdleClip = "AC_BottleidleToIdle";
    private const string NoTicketClip = "AC_NOTICKET";

#pragma warning disable 0414, 0169
    [Header("Referencias")]
    [Tooltip("Animator del modelo del barman (el hijo con el FBX). Vacío = lo busca en los hijos.")]
    [SerializeField] private Animator animator;

    [Header("Sin ticket")]
    [Tooltip("Segundos mínimos entre dos \"no\" seguidos si el jugador pulsa E muchas veces.")]
    [Min(0f)]
    [SerializeField] private float refuseCooldown = 2.5f;

    [Header("Animator Controller (solo editor)")]
    [Tooltip("El FBX del barman con las animaciones (AC_Bartender). Solo lo usa el botón que crea el Animator Controller.")]
    [SerializeField] private GameObject animationsModel;
    [Tooltip("Dónde se guarda el Animator Controller que se crea.")]
    [SerializeField] private string controllerPath = "Assets/_Root_FPS_Casino/Animation/Characters/Bartender/ACR_Bartender.controller";

    [Header("Depuración")]
    [Tooltip("Muestra sobre el barman la animación que está haciendo.")]
    [SerializeField] private bool showDebug;
#pragma warning restore 0414, 0169

    private Bartender bartender;
    private float nextRefuse;

    private void Awake()
    {
        bartender = GetComponent<Bartender>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;
        animator.applyRootMotion = false; // las animaciones no mueven al barman de su sitio
        // La botella se puede coger justo cuando acaba de sacarla.
        if (animator.runtimeAnimatorController != null)
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == BottleClip) bartender.PrepareSeconds = clip.length;
    }

    private void OnEnable()
    {
        bartender.TicketTaken += HandleTicketTaken;
        bartender.BottleGiven += HandleBottleGiven;
        bartender.Refused += HandleRefused;
    }

    private void OnDisable()
    {
        bartender.TicketTaken -= HandleTicketTaken;
        bartender.BottleGiven -= HandleBottleGiven;
        bartender.Refused -= HandleRefused;
    }

    private void HandleTicketTaken(TicketData ticket)
    {
        if (animator == null) return;
        animator.ResetTrigger(RefuseHash);
        animator.SetBool(HoldingHash, true);   // la sujeta hasta que la cojas
        animator.SetTrigger(GiveHash);
    }

    private void HandleBottleGiven(TicketData ticket)
    {
        if (animator != null) animator.SetBool(HoldingHash, false);
    }

    private void HandleRefused()
    {
        if (animator == null || Time.time < nextRefuse || bartender.IsBusy) return;
        nextRefuse = Time.time + refuseCooldown;
        animator.SetTrigger(RefuseHash);
    }

#if UNITY_EDITOR
    [ContextMenu("Crear Animator Controller")]
    public void BuildController()
    {
        if (animationsModel == null)
        {
            Debug.LogWarning("[Barman] Arrastra el FBX del barman (AC_Bartender) a Animations Model.", this);
            return;
        }
        string fbxPath = AssetDatabase.GetAssetPath(animationsModel);

        // Idles en bucle; el resto, una vez.
        if (AssetImporter.GetAtPath(fbxPath) is ModelImporter importer)
        {
            ModelImporterClipAnimation[] takes = importer.clipAnimations;
            if (takes == null || takes.Length == 0) takes = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation take in takes)
                take.loopTime = take.name == IdleClip || take.name == BottleIdleClip;
            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }

        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        AnimationClip idle = Find(clips, IdleClip), bottle = Find(clips, BottleClip), bottleIdle = Find(clips, BottleIdleClip),
                      bottleToIdle = Find(clips, BottleToIdleClip), noTicket = Find(clips, NoTicketClip);
        if (idle == null || bottle == null || bottleIdle == null || bottleToIdle == null || noTicket == null)
        {
            Debug.LogError("[Barman] Faltan animaciones en el FBX. Tiene: " + string.Join(", ", clips.Select(c => c.name)), this);
            return;
        }

        string folder = System.IO.Path.GetDirectoryName(controllerPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null) AssetDatabase.DeleteAsset(controllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Give", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Refuse", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Holding", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState sIdle = State(machine, "Idle", idle, new Vector3(300f, 0f));
        AnimatorState sBottle = State(machine, "Bottle", bottle, new Vector3(550f, -120f));
        AnimatorState sBottleIdle = State(machine, "BottleIdle", bottleIdle, new Vector3(800f, 0f));
        AnimatorState sBottleToIdle = State(machine, "BottleToIdle", bottleToIdle, new Vector3(550f, 120f));
        AnimatorState sNoTicket = State(machine, "NoTicket", noTicket, new Vector3(300f, 200f));
        machine.defaultState = sIdle;

        Transition(sIdle, sBottle, null, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Give");
        Transition(sNoTicket, sBottle, null, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Give");
        Transition(sBottle, sBottleIdle, bottle, 0.1f);
        Transition(sBottleIdle, sBottleToIdle, null, 0.1f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Holding");
        Transition(sBottleToIdle, sIdle, bottleToIdle, 0.15f);
        Transition(sIdle, sNoTicket, null, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Refuse");
        Transition(sNoTicket, sIdle, noTicket, 0.2f);

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            Undo.RecordObject(animator, "Animator del barman");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
        }
        AssetDatabase.SaveAssets();
        if (showDebug) Debug.Log($"[Barman] Creado {controllerPath} con 5 estados.", this);
    }

    private static AnimationClip Find(AnimationClip[] clips, string clipName) => clips.FirstOrDefault(c => c.name == clipName);

    private static AnimatorState State(AnimatorStateMachine machine, string stateName, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = machine.AddState(stateName, position);
        state.motion = clip;
        return state;
    }

    /// <summary>Transición de a → b. Con "waitFor" espera a que acabe esa animación; sin él, cambia en cuanto se cumpla la condición.</summary>
    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, AnimationClip waitFor, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasFixedDuration = true;
        t.duration = duration;
        t.hasExitTime = waitFor != null;
        t.exitTime = waitFor != null ? Mathf.Clamp01(1f - duration / Mathf.Max(0.01f, waitFor.length)) : 0f;
        return t;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || animator == null || animator.runtimeAnimatorController == null) return;
        AnimatorClipInfo[] info = animator.GetCurrentAnimatorClipInfo(0);
        string clip = info.Length > 0 ? info[0].clip.name : "-";
        DebugLabels.At(transform.position + Vector3.up * 2.2f, 1, $"Animación · {clip}", DebugLabels.Casino);
    }
#endif
}

#if UNITY_EDITOR
/// <summary>Botón en el Inspector para crear el Animator Controller.</summary>
[CustomEditor(typeof(BartenderAnimation))]
public class BartenderAnimationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.75f, 0.6f, 1f);
        if (GUILayout.Button("Crear Animator Controller", GUILayout.Height(30)))
            foreach (Object t in targets) ((BartenderAnimation)t).BuildController();
        GUI.backgroundColor = Color.white;
    }
}
#endif
