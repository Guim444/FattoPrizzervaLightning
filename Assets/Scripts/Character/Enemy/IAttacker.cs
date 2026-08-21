/// <summary>
/// Contract for components that execute attacks.
/// Separated from EnemyBase via ISP: allows EnemyBase to query
/// attack state without coupling to the concrete implementation.
/// </summary>
public interface IAttacker
{
    bool IsAttacking { get; }
}
