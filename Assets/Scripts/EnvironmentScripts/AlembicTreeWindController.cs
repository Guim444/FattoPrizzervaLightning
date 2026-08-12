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
    [Tooltip("Cámara usada para decidir si el árbol está visible. Vacío = busca MainCamera o CAM_Main.")]
    [SerializeField] private Camera visibilityCamera;
    [Tooltip("Margen extra de bounds para no pausar justo en el borde de cámara.")]
    [SerializeField, Min(0f)] private float visibilityBoundsPadding = 1f;
    [Tooltip("Cada cuántos segundos se comprueba visibilidad. Más alto = menos coste, menos inmediato.")]
    [SerializeField, Min(0.02f)] private float visibilityCheckInterval = 0.2f;
    [Tooltip("Distancia máxima desde la cámara a los bounds para actualizar el Alembic. 0 = sin límite. WindStateManager la ajusta según la fase.")]
    [SerializeField, Min(0f)] private float maxPlaybackDistance = 30f;

    [Header("Actualizacion por distancia")]
    [Tooltip("A partir de esta distancia, los Alembic visibles se actualizan cada 2 frames.")]
    [SerializeField, Min(0f)] private float reducedPlaybackDistance = 40f;
    [Tooltip("A partir de esta distancia, los Alembic visibles se actualizan cada 3 frames.")]
    [SerializeField, Min(0f)] private float lowPlaybackDistance = 80f;

    private float _slowTime;
    private float _fastTime;
    private bool _animationPlaybackEnabled = true;
    private Renderer[] _animatedRenderers = new Renderer[0];
    private Renderer[] _slowRenderers = new Renderer[0];
    private Renderer[] _fastRenderers = new Renderer[0];
    private bool _isVisibleForPlayback = true;
    private bool _useFastPlayer = true;
    private float _nextVisibilityCheckTime;
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
        if (!_animationPlaybackEnabled || !IsVisibleForPlayback())
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

    public void SetMaxPlaybackDistance(float distance)
    {
        maxPlaybackDistance = Mathf.Max(0f, distance);
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

    private bool IsSelectedPlayer(AlembicStreamPlayer player)
    {
        if (player == null)
            return false;

        return _useFastPlayer
            ? player == fastPlayer
            : player == slowPlayer;
    }

    private bool IsVisibleForPlayback()
    {
        if (!pauseWhenOffCamera)
        {
            if (Time.unscaledTime >= _nextVisibilityCheckTime)
            {
                _nextVisibilityCheckTime = Time.unscaledTime
                    + Mathf.Max(0.02f, visibilityCheckInterval);
                IsInsideVisibilityCamera();
            }

            if (!_isVisibleForPlayback)
            {
                _isVisibleForPlayback = true;
                ApplyPlayerVisibility(true);
            }

            return true;
        }

        if (Time.unscaledTime < _nextVisibilityCheckTime)
            return _isVisibleForPlayback;

        _nextVisibilityCheckTime = Time.unscaledTime + Mathf.Max(0.02f, visibilityCheckInterval);
        bool isVisible = IsInsideVisibilityCamera();
        if (isVisible != _isVisibleForPlayback)
        {
            _isVisibleForPlayback = isVisible;
            ApplyPlayerVisibility(isVisible);
        }

        return _isVisibleForPlayback;
    }

    private void ApplyPlayerVisibility(bool isVisible)
    {
        bool showSlowPlayer = isVisible && !_useFastPlayer && slowPlayer != null;
        bool showFastPlayer = isVisible && _useFastPlayer && fastPlayer != null;

        if (keepAlembicStreamLoaded)
        {
            bool keepSlowPlayerLoaded = !_useFastPlayer && slowPlayer != null;
            bool keepFastPlayerLoaded = _useFastPlayer && fastPlayer != null;

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

    private bool IsInsideVisibilityCamera()
    {
        Camera camera = ResolveVisibilityCamera();
        if (camera == null)
        {
            _playbackFrameInterval = 1;
            return true;
        }

        if (_animatedRenderers == null || _animatedRenderers.Length == 0)
            CacheAnimatedRenderers();

        if (_animatedRenderers == null || _animatedRenderers.Length == 0)
        {
            _playbackFrameInterval = 1;
            return true;
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        float maxDistanceSqr = maxPlaybackDistance > 0f
            ? maxPlaybackDistance * maxPlaybackDistance
            : float.PositiveInfinity;
        bool foundRenderer = false;
        float closestRendererDistanceSqr = float.PositiveInfinity;
        bool intersectsFrustum = false;

        foreach (Renderer renderer in _animatedRenderers)
        {
            if (renderer == null)
                continue;

            foundRenderer = true;
            Bounds bounds = renderer.bounds;
            bounds.Expand(visibilityBoundsPadding);

            Vector3 closestPoint = bounds.ClosestPoint(camera.transform.position);
            float distanceSqr = (closestPoint - camera.transform.position).sqrMagnitude;
            closestRendererDistanceSqr = Mathf.Min(closestRendererDistanceSqr, distanceSqr);

            if (distanceSqr <= maxDistanceSqr
                && GeometryUtility.TestPlanesAABB(planes, bounds))
            {
                intersectsFrustum = true;
            }
        }

        _playbackFrameInterval = GetPlaybackFrameInterval(closestRendererDistanceSqr);

        if (foundRenderer)
            return intersectsFrustum;

        float rootDistanceSqr = (transform.position - camera.transform.position).sqrMagnitude;
        _playbackFrameInterval = GetPlaybackFrameInterval(rootDistanceSqr);
        return maxPlaybackDistance <= 0f || rootDistanceSqr <= maxDistanceSqr;
    }

    private int GetPlaybackFrameInterval(float distanceSqr)
    {
        if (distanceSqr > lowPlaybackDistance * lowPlaybackDistance)
            return 3;

        if (distanceSqr > reducedPlaybackDistance * reducedPlaybackDistance)
            return 2;

        return 1;
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
