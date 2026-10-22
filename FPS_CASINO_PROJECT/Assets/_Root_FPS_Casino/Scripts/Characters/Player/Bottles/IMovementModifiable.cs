/// <summary>
/// Lo implementarán los enemigos para que los charcos puedan ralentizarlos o paralizarlos.
///
/// "source" es quién aplica el efecto (el charco). Así, si un enemigo pisa dos charcos a la vez,
/// cada uno pone y quita SU efecto sin pisar al otro. Lo normal en el enemigo es guardar los
/// multiplicadores en un Dictionary&lt;object, float&gt; y usar el más bajo.
///
/// Para el daño (impacto, veneno, instakill) no hace falta nada nuevo: se usa IDamageable.
/// </summary>
public interface IMovementModifiable
{
    /// <summary>multiplier: 1 = normal, 0.5 = la mitad de rápido, 0 = paralizado.</summary>
    void SetSpeedMultiplier(object source, float multiplier);

    void ClearSpeedMultiplier(object source);
}