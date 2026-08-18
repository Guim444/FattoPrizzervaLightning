using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Coordina las animaciones ambientales al pulsar teclas 1-4.
/// LightingStateManager gestiona su propio input para cielo y luces.
/// Este script escucha las mismas teclas para vídeos de ventisca, árboles y plantas.
///
/// Tecla 1 → Idle fast en loop (snap inmediato)
/// Tecla 2 → Transición → Idle loop  (ventisca alta a media)
/// Tecla 3 → Transición → Idle loop  (ventisca media a mínima)
/// Tecla 4 → Transición → Idle loop  (ventisca mínima a media)
/// </summary>
public class EnvironmentStateManager : MonoBehaviour
{
    [Header("Sub-managers")]
    [SerializeField] private WindStateManager windManager;
    [SerializeField] private PlantWindController plantManager;

    [Header("Performance Toggles")]
    [Tooltip("Activa o pausa la animacion de arboles, plantas y Alembic de viento.")]
    [SerializeField] private bool runTreeAnimations = true;
    [Tooltip("Activa o pausa animators, videos y fluctuacion de luces de fuego.")]
    [SerializeField] private bool runFireAnimations = true;
    [Tooltip("Activa o pausa los VideoPlayers de ventisca.")]
    [SerializeField] private bool runBlizzardVideos = true;

    [Header("Performance Auto Collect")]
    [SerializeField] private bool autoCollectPerformanceTargets = true;

    [Header("Test")]
    [SerializeField] private bool enableKeyboardShortcuts = true;

    private AlembicTreeWindController[] _treeControllers;
    private Animator[] _treeAnimators;
    private LoopingVideoSpriteReplacement[] _fireVideoReplacements;
    private VideoPlayer[] _fireVideoPlayers;
    private Animator[] _fireAnimators;
    private FireVisualScript[] _fireVisualScripts;

    private bool _lastRunTreeAnimations;
    private bool _lastRunFireAnimations;
    private bool _lastRunBlizzardVideos;
    private bool _performanceTogglesInitialized;

    private static readonly string[] TreeKeywords =
    {
        "tree", "arbol", "genericplant", "thinlog", "plant_windy", "plant_idle", "planta_mj"
    };

    private static readonly string[] FireKeywords =
    {
        "fire", "fuego", "candle", "vela", "brazier", "braziers", "campfire", "flame"
    };

    private void Awake()
    {
        bool repaired = false;

        if (windManager == null)
        {
            windManager = GetComponent<WindStateManager>();
            if (windManager == null)
                windManager = FindUniqueManager<WindStateManager>();

            repaired |= windManager != null;
        }

        if (plantManager == null)
        {
            plantManager = GetComponent<PlantWindController>();
            if (plantManager == null)
                plantManager = FindUniqueManager<PlantWindController>();

            repaired |= plantManager != null;
        }

        if (repaired)
            Debug.LogWarning($"[{nameof(EnvironmentStateManager)}] Se repararon referencias perdidas durante esta ejecución. Revisa el Inspector para corregirlas de forma permanente.", this);
    }

    private void Start()
    {
        RefreshPerformanceTargets();
        ApplyPerformanceToggles(true);
    }

    private void Update()
    {
        ApplyPerformanceToggles(false);

        if (!enableKeyboardShortcuts) return;

        if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ApplyPhase(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ApplyPhase(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ApplyPhase(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ApplyPhase(4);
    }

    public void ApplyPhase(int phase)
    {
        if (phase < 1 || phase > 4)
        {
            Debug.LogWarning($"[{nameof(EnvironmentStateManager)}] La fase {phase} no existe.", this);
            return;
        }

        ApplyWindState(phase);
    }

    private void ApplyWindState(int key)
    {
        if (windManager == null) { Debug.LogWarning("[EnvironmentStateManager] windManager no asignado en el Inspector."); return; }

        switch (key)
        {
            // Tecla 1: idle fast en loop, snap inmediato (estado de inicio / reset)
            case 1:
                windManager.SnapToState(WindStateManager.WindPreset.W1_MaxIdle);
                if (runTreeAnimations)
                    plantManager?.PlayWindy();
                break;

            // Tecla 2: transición de máxima a media, luego idle media en loop
            case 2:
                windManager.TransitionTo(WindStateManager.WindPreset.W2_MaxToMedium);
                if (runTreeAnimations)
                    plantManager?.PlayWindyExit();
                break;

            // Tecla 3: transición de media a mínima, luego idle mínima en loop
            case 3:
                windManager.TransitionTo(WindStateManager.WindPreset.W3_MediumToMin);
                if (runTreeAnimations)
                    plantManager?.PlayIdleSmooth();
                break;

            // Tecla 4: transición de mínima a media (cielo azul), luego idle media en loop
            case 4:
                windManager.TransitionTo(WindStateManager.WindPreset.W4_MinToMedium);
                if (runTreeAnimations)
                    plantManager?.PlayWindy();
                break;
        }
    }

    private void RefreshPerformanceTargets()
    {
        if (!autoCollectPerformanceTargets)
            return;

        _treeControllers = Object.FindObjectsByType<AlembicTreeWindController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        _treeAnimators = FilterAnimators(TreeKeywords, FireKeywords);
        _fireVideoReplacements = FilterComponents<LoopingVideoSpriteReplacement>(FireKeywords);
        _fireVideoPlayers = FilterComponents<VideoPlayer>(FireKeywords);
        _fireAnimators = FilterAnimators(FireKeywords, TreeKeywords);
        _fireVisualScripts = FilterComponents<FireVisualScript>(FireKeywords);
    }

    private void ApplyPerformanceToggles(bool force)
    {
        if (force || !_performanceTogglesInitialized || _lastRunTreeAnimations != runTreeAnimations)
            ApplyTreeAnimationToggle(runTreeAnimations);

        if (force || !_performanceTogglesInitialized || _lastRunFireAnimations != runFireAnimations)
            ApplyFireAnimationToggle(runFireAnimations);

        if (force || !_performanceTogglesInitialized || _lastRunBlizzardVideos != runBlizzardVideos)
            ApplyBlizzardVideoToggle(runBlizzardVideos);

        _lastRunTreeAnimations = runTreeAnimations;
        _lastRunFireAnimations = runFireAnimations;
        _lastRunBlizzardVideos = runBlizzardVideos;
        _performanceTogglesInitialized = true;
    }

    private void ApplyTreeAnimationToggle(bool shouldRun)
    {
        windManager?.SetStandaloneVegetationPlaybackEnabled(shouldRun);
        plantManager?.SetAnimationPlaybackEnabled(shouldRun);

        if (_treeControllers != null)
        {
            foreach (var tree in _treeControllers)
                if (tree != null) tree.SetAnimationPlaybackEnabled(shouldRun);
        }

        if (_treeAnimators != null)
        {
            foreach (var animator in _treeAnimators)
                if (animator != null) animator.enabled = shouldRun;
        }
    }

    private void ApplyFireAnimationToggle(bool shouldRun)
    {
        if (_fireVideoReplacements != null)
        {
            foreach (var replacement in _fireVideoReplacements)
                if (replacement != null) replacement.SetPlaybackEnabled(shouldRun);
        }

        if (_fireVideoPlayers != null)
        {
            foreach (var player in _fireVideoPlayers)
            {
                if (player == null) continue;

                if (shouldRun)
                {
                    if (player.isPrepared)
                        player.Play();
                    else
                        player.Prepare();
                }
                else if (player.isPlaying)
                {
                    player.Pause();
                }
            }
        }

        if (_fireAnimators != null)
        {
            foreach (var animator in _fireAnimators)
                if (animator != null) animator.enabled = shouldRun;
        }

        if (_fireVisualScripts != null)
        {
            foreach (var fire in _fireVisualScripts)
                if (fire != null) fire.SetAnimationPlaybackEnabled(shouldRun);
        }
    }

    private void ApplyBlizzardVideoToggle(bool shouldRun)
    {
        windManager?.SetBlizzardVideoPlaybackEnabled(shouldRun);
    }

    private T[] FilterComponents<T>(string[] requiredKeywords) where T : Component
    {
        var matches = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var filtered = new System.Collections.Generic.List<T>();

        foreach (var component in matches)
        {
            if (component != null && HierarchyContainsAny(component.transform, requiredKeywords))
                filtered.Add(component);
        }

        return filtered.ToArray();
    }

    private Animator[] FilterAnimators(string[] requiredKeywords, string[] excludedKeywords)
    {
        var matches = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var filtered = new System.Collections.Generic.List<Animator>();

        foreach (var animator in matches)
        {
            if (animator == null)
                continue;

            bool isRequired = HierarchyContainsAny(animator.transform, requiredKeywords);
            bool isExcluded = HierarchyContainsAny(animator.transform, excludedKeywords);
            if (isRequired && !isExcluded)
                filtered.Add(animator);
        }

        return filtered.ToArray();
    }

    private static bool HierarchyContainsAny(Transform candidate, string[] keywords)
    {
        while (candidate != null)
        {
            string objectName = candidate.name.ToLowerInvariant();

            foreach (string keyword in keywords)
            {
                if (!string.IsNullOrEmpty(keyword) && objectName.Contains(keyword))
                    return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    private T FindUniqueManager<T>() where T : Component
    {
        var matches = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (matches.Length == 1)
            return matches[0];

        if (matches.Length > 1)
            Debug.LogWarning($"[{nameof(EnvironmentStateManager)}] Hay varios {typeof(T).Name}. Asigna la referencia manualmente.", this);

        return null;
    }
}
