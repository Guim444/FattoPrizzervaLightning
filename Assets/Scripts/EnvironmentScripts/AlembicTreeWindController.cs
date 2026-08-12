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
    [Tooltip("Mantiene cargado desde Awake el Alembic seleccionado aunque su renderer quede fuera de rango. Evita picos al ampliar el culling.")]
    [SerializeField] private bool keepAlembicStreamLoaded = true;

    [Header("Culling por cámara")]
    [Tooltip("Pausa el avance Alembic cuando el árbol queda fuera de la cámara principal.")]
    [SerializeField] private bool pauseWhenOffCamera = true;
    private float _slowTime;
    private float _fastTime;
    private bool _animationPlaybackEnabled = true;
    private Renderer[] _animatedRenderers = new Renderer[0];
    private Renderer[] _slowRenderers = new Renderer[0];
    private Renderer[] _fastRenderers = new Renderer[0];
    private bool _isVisibleForPlayback = true;
    private bool _useFastPlayer = true;
    private int _playbackFrameInterval = 1;

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
        if (!_animationPlaybackEnabled || (pauseWhenOffCamera && !_isVisibleForPlayback))
            return;

        bool shouldUpdatePlayer = _playbackFrameInterval <= 1
            || Time.frameCount % _playbackFrameInterval == 0;

        if (advanceInactivePlayersInBackground || IsSelectedPlayer(slowPlayer))
            AdvancePlayer(slowPlayer, ref _slowTime, slowPlaybackSpeed, shouldUpdatePlayer);

        if (advanceInactivePlayersInBackground || IsSelectedPlayer(fastPlayer))
            AdvancePlayer(fastPlayer, ref _fastTime, fastPlaybackSpeed, shouldUpdatePlayer);
    }

    private void AdvancePlayer(
        AlembicStreamPlayer player,
        ref float time,
        float speed,
        bool updatePlayer)
    {
        if (player == null) return;

        float duration = player.Duration;
        if (duration <= 0f) return;

        time = WrapAlembicTime(time + Time.deltaTime * speed, duration);
        if (updatePlayer)
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
        _slowRenderers = slowPlayer != null
            ? slowPlayer.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];
        _fastRenderers = fastPlayer != null
            ? fastPlayer.GetComponentsInChildren<Renderer>(true)
            : new Renderer[0];

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
        _useFastPlayer = useFast;

        if (!advanceInactivePlayersInBackground)
            SyncPlayerBeforeSwitch(useFast);

        ApplyPlayerVisibility(!pauseWhenOffCamera || _isVisibleForPlayback);

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

    private bool IsSelectedPlayer(AlembicStreamPlayer player)
    {
        if (player == null)
            return false;

        return _useFastPlayer
            ? player == fastPlayer
            : player == slowPlayer;
    }

    public void RefreshVisibilityAndPlayback(
        Camera camera,
        float maxDistance,
        float everyTwoFramesDistance,
        float everyThreeFramesDistance,
        float boundsPadding)
    {
        if (_animatedRenderers == null || _animatedRenderers.Length == 0)
            CacheAnimatedRenderers();

        bool visibleForPlayback = true;
        float closestDistanceSqr = float.PositiveInfinity;

        if (camera != null && _animatedRenderers != null && _animatedRenderers.Length > 0)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            float maxDistanceSqr = maxDistance > 0f
                ? maxDistance * maxDistance
                : float.PositiveInfinity;
            bool foundRenderer = false;
            bool intersectsFrustum = false;

            foreach (Renderer renderer in _animatedRenderers)
            {
                if (renderer == null)
                    continue;

                foundRenderer = true;
                Bounds bounds = renderer.bounds;
                bounds.Expand(Mathf.Max(0f, boundsPadding));

                Vector3 closestPoint = bounds.ClosestPoint(camera.transform.position);
                float distanceSqr = (closestPoint - camera.transform.position).sqrMagnitude;
                closestDistanceSqr = Mathf.Min(closestDistanceSqr, distanceSqr);

                if (distanceSqr <= maxDistanceSqr
                    && GeometryUtility.TestPlanesAABB(planes, bounds))
                {
                    intersectsFrustum = true;
                }
            }

            if (foundRenderer)
                visibleForPlayback = intersectsFrustum;
            else
                closestDistanceSqr = (transform.position - camera.transform.position).sqrMagnitude;
        }
        else if (camera != null)
        {
            closestDistanceSqr = (transform.position - camera.transform.position).sqrMagnitude;
            float maxDistanceSqr = maxDistance > 0f
                ? maxDistance * maxDistance
                : float.PositiveInfinity;
            visibleForPlayback = closestDistanceSqr <= maxDistanceSqr;
        }

        int playbackFrameInterval = GetPlaybackFrameInterval(
            closestDistanceSqr,
            everyTwoFramesDistance,
            everyThreeFramesDistance);
        if (_playbackFrameInterval != playbackFrameInterval)
            _playbackFrameInterval = playbackFrameInterval;

        if (!pauseWhenOffCamera)
            visibleForPlayback = true;

        if (_isVisibleForPlayback != visibleForPlayback)
        {
            _isVisibleForPlayback = visibleForPlayback;
            ApplyPlayerVisibility(visibleForPlayback);
        }
    }

    private void ApplyPlayerVisibility(bool isVisible)
    {
        bool showSlowPlayer = isVisible && !_useFastPlayer && slowPlayer != null;
        bool showFastPlayer = isVisible && _useFastPlayer && fastPlayer != null;

        if (keepAlembicStreamLoaded)
        {
            bool keepSlowPlayerLoaded = slowPlayer != null;
            bool keepFastPlayerLoaded = fastPlayer != null;

            SetPlayerActive(slowPlayer, keepSlowPlayerLoaded);
            SetPlayerActive(fastPlayer, keepFastPlayerLoaded);
            SetRenderersEnabled(_slowRenderers, showSlowPlayer);
            SetRenderersEnabled(_fastRenderers, showFastPlayer);
            return;
        }

        SetPlayerActive(slowPlayer, showSlowPlayer);
        SetPlayerActive(fastPlayer, showFastPlayer);
    }

    private static void SetPlayerActive(AlembicStreamPlayer player, bool isActive)
    {
        if (player != null && player.gameObject.activeSelf != isActive)
            player.gameObject.SetActive(isActive);
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.enabled != isEnabled)
                renderer.enabled = isEnabled;
        }
    }

    private static int GetPlaybackFrameInterval(
        float distanceSqr,
        float everyTwoFramesDistance,
        float everyThreeFramesDistance)
    {
        if (float.IsPositiveInfinity(distanceSqr))
            return 1;

        float twoFramesSqr = Mathf.Max(0f, everyTwoFramesDistance);
        float threeFramesSqr = Mathf.Max(twoFramesSqr, everyThreeFramesDistance);
        twoFramesSqr *= twoFramesSqr;
        threeFramesSqr *= threeFramesSqr;

        if (distanceSqr > threeFramesSqr)
            return 3;

        if (distanceSqr > twoFramesSqr)
            return 2;

        return 1;
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
