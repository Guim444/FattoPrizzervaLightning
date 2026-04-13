/// <summary>
/// Contrato para componentes que ejecutan ataques.
/// Separado de EnemyBase mediante ISP: permite que EnemyBase consulte
/// el estado de ataque sin acoplarse a la implementación concreta.
/// </summary>
public interface IAttacker
{
    bool IsAttacking { get; }
}
