using System.Collections.Generic;
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
/// Ambos clips hacen loop manual avanzando CurrentTime.
/// Solo el GO activo se actualiza cada frame; al cambiar de velocidad,
/// el clip que entra se sincroniza con la fase del clip que sale.
/// </summary>
public class AlembicTreeWindController : MonoBehaviour
{
    [Header("Reproductores Alembic")]
    [SerializeField] private AlembicStreamPlayer slowPlayer;
    [SerializeField] private AlembicStreamPlayer fastPlayer;

    [Header("Velocidades de reproducción (multiplicador sobre tiempo real)")]
    [SerializeField] private float slowPlaybackSpeed = 1f;
    [SerializeField] private float fastPlaybackSpeed = 1f;

    [Header("Rendimiento")]
    [Tooltip("Activa el comportamiento antiguo: el clip visible y el oculto avanzan cada frame. Útil solo para comparar stutter/jumps.")]
    [SerializeField] private bool advanceInactivePlayersInBackground = false;

    [Header("Culling por cámara")]
    [Tooltip("Pausa el avance Alembic cuando el árbol queda fuera de la cámara principal.")]
    [SerializeField] private bool pauseWhenOffCamera = true;
    [Tooltip("Cámara usada para decidir si el árbol está visible. Vacío = busca MainCamera o CAM_Main.")]
    [SerializeField] private Camera visibilityCamera;
    [Tooltip("Margen extra de bounds para no pausar justo en el borde de cámara.")]
    [SerializeField, Min(0f)] private float visibilityBoundsPadding = 1f;
    [Tooltip("Cada cuántos segundos se comprueba visibilidad. Más alto = menos coste, menos inmediato.")]
    [SerializeField, Min(0.02f)] private float visibilityCheckInterval = 0.2f;

    private float _slowTime;
    private float _fastTime;
    private bool _animationPlaybackEnabled = true;
    private Renderer[] _animatedRenderers = new Renderer[0];
    private bool _isVisibleForPlayback = true;
    private float _nextVisibilityCheckTime;

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
        CacheAnimatedRenderers();

        // El estado inicial es W1_MaxIdle (ventisca máxima) → fast
        SetFast(true);
    }

    private void Update()
    {
        if (!_animationPlaybackEnabled || !IsVisibleForPlayback())
            return;

        if (advanceInactivePlayersInBackground || IsPlayerActive(slowPlayer))
            AdvancePlayer(slowPlayer, ref _slowTime, slowPlaybackSpeed);

        if (advanceInactivePlayersInBackground || IsPlayerActive(fastPlayer))
            AdvancePlayer(fastPlayer, ref _fastTime, fastPlaybackSpeed);
    }

    private void AdvancePlayer(AlembicStreamPlayer player, ref float time, float speed)
    {
        if (player == null) return;

        float duration = player.Duration;
        if (duration <= 0f) return;

        time = WrapAlembicTime(time + Time.deltaTime * speed, duration);
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

    private void CacheAnimatedRenderers()
    {
        var renderers = new List<Renderer>();
        AddAnimatedRenderers(slowPlayer, renderers);
        AddAnimatedRenderers(fastPlayer, renderers);
        _animatedRenderers = renderers.ToArray();
    }

    private static void AddAnimatedRenderers(AlembicStreamPlayer player, List<Renderer> renderers)
    {
        if (player == null)
            return;

        foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && !renderers.Contains(renderer))
                renderers.Add(renderer);
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

        if (!advanceInactivePlayersInBackground)
            SyncPlayerBeforeSwitch(useFast);

        if (slowPlayer != null) slowPlayer.gameObject.SetActive(!useFast);
        if (fastPlayer != null) fastPlayer.gameObject.SetActive(useFast);

        _nextVisibilityCheckTime = 0f;
    }

    public void SetPlaybackOffset(float offsetSeconds)
    {
        ResolveMissingPlayers();

        _slowTime = SamplePlayerAtOffset(slowPlayer, offsetSeconds);
        _fastTime = SamplePlayerAtOffset(fastPlayer, offsetSeconds);
    }

    public void SetAnimationPlaybackEnabled(bool shouldRun)
    {
        _animationPlaybackEnabled = shouldRun;

        if (shouldRun)
            _nextVisibilityCheckTime = 0f;
    }

    public void PrewarmPlayers(float sampleTime)
    {
        ResolveMissingPlayers();
        PrewarmPlayer(slowPlayer, sampleTime);
        PrewarmPlayer(fastPlayer, sampleTime);
    }

    private static void PrewarmPlayer(AlembicStreamPlayer player, float sampleTime)
    {
        if (player == null) return;

        float duration = player.Duration;
        if (duration <= 0f) return;

        bool wasActive = player.gameObject.activeSelf;
        if (!wasActive)
            player.gameObject.SetActive(true);

        player.CurrentTime = Mathf.Clamp(sampleTime, 0f, duration);

        if (!wasActive)
            player.gameObject.SetActive(false);
    }

    private static float SamplePlayerAtOffset(AlembicStreamPlayer player, float offsetSeconds)
    {
        if (player == null) return 0f;

        float duration = player.Duration;
        if (duration <= 0f) return 0f;

        float time = WrapAlembicTime(offsetSeconds, duration);
        player.CurrentTime = time;
        return time;
    }

    private void SyncPlayerBeforeSwitch(bool useFast)
    {
        if (useFast)
            SyncPlayerTime(slowPlayer, fastPlayer, _slowTime, ref _fastTime);
        else
            SyncPlayerTime(fastPlayer, slowPlayer, _fastTime, ref _slowTime);
    }

    private static void SyncPlayerTime(AlembicStreamPlayer source, AlembicStreamPlayer target, float sourceTime, ref float targetTime)
    {
        if (target == null) return;

        float targetDuration = target.Duration;
        if (targetDuration <= 0f) return;

        float normalizedTime = 0f;
        if (source != null && source.Duration > 0f)
            normalizedTime = Mathf.Clamp01(sourceTime / source.Duration);
        else
            normalizedTime = Mathf.Clamp01(targetTime / targetDuration);

        targetTime = WrapAlembicTime(normalizedTime * targetDuration, targetDuration);

        bool wasActive = target.gameObject.activeSelf;
        if (!wasActive)
            target.gameObject.SetActive(true);

        target.CurrentTime = targetTime;

        if (!wasActive)
            target.gameObject.SetActive(false);
    }

    private static bool IsPlayerActive(AlembicStreamPlayer player)
    {
        return player != null && player.gameObject.activeInHierarchy;
    }

    private bool IsVisibleForPlayback()
    {
        if (!pauseWhenOffCamera)
            return true;

        if (Time.unscaledTime < _nextVisibilityCheckTime)
            return _isVisibleForPlayback;

        _nextVisibilityCheckTime = Time.unscaledTime + Mathf.Max(0.02f, visibilityCheckInterval);
        _isVisibleForPlayback = IsInsideVisibilityCamera();
        return _isVisibleForPlayback;
    }

    private bool IsInsideVisibilityCamera()
    {
        Camera camera = ResolveVisibilityCamera();
        if (camera == null)
            return true;

        if (_animatedRenderers == null || _animatedRenderers.Length == 0)
            CacheAnimatedRenderers();

        if (_animatedRenderers == null || _animatedRenderers.Length == 0)
            return true;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        bool foundActiveRenderer = false;

        foreach (Renderer renderer in _animatedRenderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            foundActiveRenderer = true;
            Bounds bounds = renderer.bounds;
            bounds.Expand(visibilityBoundsPadding);

            if (GeometryUtility.TestPlanesAABB(planes, bounds))
                return true;
        }

        return !foundActiveRenderer;
    }

    private Camera ResolveVisibilityCamera()
    {
        if (visibilityCamera != null)
            return visibilityCamera;

        visibilityCamera = Camera.main;
        if (visibilityCamera != null)
            return visibilityCamera;

        GameObject namedCamera = GameObject.Find("CAM_Main");
        if (namedCamera == null)
            namedCamera = GameObject.Find("MainCamera");

        visibilityCamera = namedCamera != null
            ? namedCamera.GetComponent<Camera>()
            : null;

        return visibilityCamera;
    }

    private static float WrapAlembicTime(float time, float duration)
    {
        if (duration <= 0f) return 0f;

        time %= duration;
        if (time < 0f)
            time += duration;

        return time;
    }
}
