/// <summary>
/// Cualquier cosa que pueda recibir daño (seguratas robots, barriles explosivos, etc.)
/// implementa esto. Así las armas no necesitan saber nada del enemigo.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}