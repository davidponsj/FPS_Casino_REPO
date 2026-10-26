using UnityEngine;

/// <summary>
/// Un estado de una IA. TOwner es la IA a la que pertenece (EnemyAI),
/// de modo que el estado puede acceder a sus datos y componentes.
/// </summary>
public abstract class State<TOwner>
{
    protected readonly TOwner owner;

    /// <summary>Nombre para depuración: "ChaseState" → "Chase". Se calcula una sola vez.</summary>
    public string Name { get; }

    protected State(TOwner owner)
    {
        this.owner = owner;

        string typeName = GetType().Name;
        Name = typeName.EndsWith("State") ? typeName.Substring(0, typeName.Length - 5) : typeName;
    }

    /// <summary>
    /// Tiempo mínimo (en segundos) que hay que permanecer en este estado antes de poder salir
    /// con una transición normal. Evita oscilaciones. Una transición forzada se lo salta.
    /// </summary>
    public virtual float MinDuration => 0f;

    /// <summary>Se llama una vez al entrar en el estado.</summary>
    public virtual void Enter() { }

    /// <summary>Se llama cada frame mientras el estado está activo.</summary>
    public virtual void Tick() { }

    /// <summary>Se llama una vez al salir del estado (aquí se limpia lo que Enter dejó).</summary>
    public virtual void Exit() { }
}

/// <summary>
/// Máquina de estados ligera. No decide cuándo cambiar de estado (eso lo hace cada estado en su Tick):
/// solo ejecuta el cambio de forma limpia (Exit → Enter) y aplica la duración mínima.
/// </summary>
public class StateMachine<TOwner>
{
    private float enterTime;

    public State<TOwner> Current { get; private set; }

    public float TimeInState => Time.time - enterTime;

    /// <summary>True si ya ha pasado la duración mínima del estado actual.</summary>
    public bool CanLeave => Current == null || TimeInState >= Current.MinDuration;

    /// <summary>
    /// Cambia de estado. Devuelve false si no se cambió (mismo estado, o aún no ha pasado
    /// la duración mínima y no se ha forzado).
    /// </summary>
    public bool ChangeState(State<TOwner> next, bool force = false)
    {
        if (next == null || next == Current) return false;
        if (!force && !CanLeave) return false;

        Current?.Exit();
        Current = next;
        enterTime = Time.time;
        Current.Enter();
        return true;
    }

    public void Tick()
    {
        Current?.Tick();
    }
}