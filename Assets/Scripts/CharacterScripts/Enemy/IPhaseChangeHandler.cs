/// <summary>
/// Contrato para enemigos que implementan un sistema de fases progresivas.
/// Separado de EnemyBase mediante ISP: no todos los enemigos tienen fases.
/// </summary>
public interface IPhaseChangeHandler
{
    int CurrentPhase { get; }
    void AdvancePhase();
}
