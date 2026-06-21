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

    public bool HasAvailablePlayers =>
        slowPlayer != null
        || fastPlayer != null
        || GetComponentInChildren<AlembicStreamPlayer>(true) != null;

    private void Awake()
    {
        ResolveMissingPlayers();

        if (slowPlayer == null && fastPlayer == null)
        {
            Debug.LogWarning($"[{nameof(AlembicTreeWindController)}] {name} no tiene reproductores Alembic asignados.", this);
            enabled = false;
            return;
        }

        DisableStaticTreeRenderers();

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

    private void DisableStaticTreeRenderers()
    {
        foreach (var lodGroup in GetComponentsInChildren<LODGroup>(true))
        {
            if (!BelongsToAnimatedPlayer(lodGroup.transform))
                lodGroup.enabled = false;
        }

        foreach (var treeRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!BelongsToAnimatedPlayer(treeRenderer.transform))
                treeRenderer.enabled = false;
        }
    }

    private void ResolveMissingPlayers()
    {
        if (slowPlayer != null && fastPlayer != null)
            return;

        bool repaired = false;
        foreach (var player in GetComponentsInChildren<AlembicStreamPlayer>(true))
        {
            string playerName = player.gameObject.name.ToLowerInvariant();

            if (slowPlayer == null && playerName.Contains("slow"))
            {
                slowPlayer = player;
                repaired = true;
            }
            else if (fastPlayer == null && playerName.Contains("fast"))
            {
                fastPlayer = player;
                repaired = true;
            }
        }

        if (repaired)
            Debug.LogWarning($"[{nameof(AlembicTreeWindController)}] {name} reparó sus referencias Alembic durante esta ejecución. Revisa el prefab para corregirlas de forma permanente.", this);
    }

    private bool BelongsToAnimatedPlayer(Transform candidate)
    {
        return IsInsidePlayer(candidate, slowPlayer) || IsInsidePlayer(candidate, fastPlayer);
    }

    private static bool IsInsidePlayer(Transform candidate, AlembicStreamPlayer player)
    {
        if (player == null) return false;
        return candidate == player.transform || candidate.IsChildOf(player.transform);
    }

    /// <summary>
    /// Activa la animación rápida (true) o lenta (false).
    /// Llamado por WindStateManager al cambiar de estado.
    /// </summary>
    public void SetFast(bool fast)
    {
        bool useFast = fastPlayer != null && (fast || slowPlayer == null);

        if (slowPlayer != null) slowPlayer.gameObject.SetActive(!useFast);
        if (fastPlayer != null) fastPlayer.gameObject.SetActive(useFast);
    }
}
