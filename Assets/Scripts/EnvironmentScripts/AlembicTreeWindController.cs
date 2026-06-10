using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

/// <summary>
/// Controla la animación Alembic de un árbol según el estado de viento.
///
/// Setup esperado en escena por árbol:
///   TreeGameObject  ← este script aquí
///     └─ Slow       ← AlembicStreamPlayer con Tree_X_anim_slow.abc
///     └─ Fast       ← AlembicStreamPlayer con Tree_X_anim_fast.abc
///
/// Ambos clips hacen loop manual avanzando currentTime cada frame.
/// Solo el GO activo es visible; el otro sigue avanzando en background
/// para que al reactivarse no salte a frame 0.
/// </summary>
public class AlembicTreeWindController : MonoBehaviour
{
    [Header("Reproductores Alembic")]
    [SerializeField] private AlembicStreamPlayer slowPlayer;
    [SerializeField] private AlembicStreamPlayer fastPlayer;

    [Header("Velocidades de reproducción (multiplicador sobre tiempo real)")]
    [SerializeField] private float slowPlaybackSpeed = 1f;
    [SerializeField] private float fastPlaybackSpeed = 1f;

    private float _slowTime;
    private float _fastTime;

    private void Awake()
    {
        // El estado inicial es W1_MaxIdle (ventisca máxima) → fast
        SetFast(true);
    }

    private void Update()
    {
        AdvancePlayer(slowPlayer, ref _slowTime, slowPlaybackSpeed);
        AdvancePlayer(fastPlayer, ref _fastTime, fastPlaybackSpeed);
    }

    private void AdvancePlayer(AlembicStreamPlayer player, ref float time, float speed)
    {
        if (player == null) return;

        float duration = player.Duration;
        if (duration <= 0f) return;

        time += Time.deltaTime * speed;
        if (time > duration) time -= duration;

        player.CurrentTime = time;
    }

    /// <summary>
    /// Activa la animación rápida (true) o lenta (false).
    /// Llamado por WindStateManager al cambiar de estado.
    /// </summary>
    public void SetFast(bool fast)
    {
        if (slowPlayer != null) slowPlayer.gameObject.SetActive(!fast);
        if (fastPlayer != null) fastPlayer.gameObject.SetActive(fast);
    }
}