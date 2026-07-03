using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Cambia entre tres conjuntos de lightmaps que comparten el mismo atlas UV.
/// Solo sustituye los slots pertenecientes a esta escena para no afectar
/// a lightmaps de otras escenas cargadas de forma aditiva.
/// </summary>
public class LightmapStateManager : MonoBehaviour
{
    private enum LightmapState
    {
        Warm1,
        Warm2,
        Blue
    }

    [Serializable]
    private struct LightmapSet
    {
        public Texture2D[] colors;
        public Texture2D[] directions;
        public Texture2D[] shadowMasks;
    }

    [Header("Bakes")]
    [FormerlySerializedAs("warm")]
    [Tooltip("Bake utilizado por la tecla 1.")]
    [SerializeField] private LightmapSet warm1;
    [Tooltip("Bake utilizado por las teclas 2 y 3.")]
    [SerializeField] private LightmapSet warm2;
    [Tooltip("Bake utilizado por la tecla 4.")]
    [SerializeField] private LightmapSet blue;

    [Header("Startup")]
    [SerializeField] private bool applyWarmOnAwake = true;

    private int _lightmapStartIndex = -1;
    private int _sceneLightmapCount = -1;
    private int _currentState = -1;
    private bool _warnedWarm2Fallback;

    private void Awake()
    {
        FindSceneLightmapRange(out _lightmapStartIndex, out _sceneLightmapCount);

        if (applyWarmOnAwake)
            ApplyWarm1();
    }

    public void ApplyWarm1()
    {
        Apply(warm1, LightmapState.Warm1);
    }

    public void ApplyWarm2()
    {
        if (IsEmpty(warm2))
        {
            if (!_warnedWarm2Fallback)
            {
                Debug.LogWarning(
                    $"[{nameof(LightmapStateManager)}] Warm2 todavía no está asignado. Se mantiene Warm1 como fallback.",
                    this);
                _warnedWarm2Fallback = true;
            }

            ApplyWarm1();
            return;
        }

        Apply(warm2, LightmapState.Warm2);
    }

    public void ApplyBlue()
    {
        Apply(blue, LightmapState.Blue);
    }

    private void Apply(LightmapSet set, LightmapState state)
    {
        if (_currentState == (int)state)
            return;

        if (!IsValid(set, state) || !MatchesLoadedSceneLayout(set, state))
            return;

        int startIndex = Mathf.Max(0, _lightmapStartIndex);
        LightmapData[] current = LightmapSettings.lightmaps ?? Array.Empty<LightmapData>();
        int requiredLength = Mathf.Max(current.Length, startIndex + set.colors.Length);
        var combined = new LightmapData[requiredLength];

        Array.Copy(current, combined, current.Length);

        for (int i = 0; i < set.colors.Length; i++)
        {
            combined[startIndex + i] = new LightmapData
            {
                lightmapColor = set.colors[i],
                lightmapDir = set.directions[i],
                shadowMask = HasShadowMasks(set) ? set.shadowMasks[i] : null
            };
        }

        LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;
        LightmapSettings.lightmaps = combined;
        _currentState = (int)state;
    }

    private bool IsValid(LightmapSet set, LightmapState state)
    {
        if (set.colors == null || set.colors.Length == 0)
        {
            Debug.LogError($"[{nameof(LightmapStateManager)}] El bake {state} no contiene lightmaps de color.", this);
            return false;
        }

        if (set.directions == null || set.directions.Length != set.colors.Length)
        {
            Debug.LogError($"[{nameof(LightmapStateManager)}] El bake {state} debe tener la misma cantidad de atlas de color y direccion.", this);
            return false;
        }

        if (set.shadowMasks != null
            && set.shadowMasks.Length > 0
            && set.shadowMasks.Length != set.colors.Length)
        {
            Debug.LogError($"[{nameof(LightmapStateManager)}] El bake {state} debe tener la misma cantidad de shadow masks que atlas de color.", this);
            return false;
        }

        for (int i = 0; i < set.colors.Length; i++)
        {
            bool missingShadowMask = HasShadowMasks(set) && set.shadowMasks[i] == null;
            if (set.colors[i] == null || set.directions[i] == null || missingShadowMask)
            {
                Debug.LogError($"[{nameof(LightmapStateManager)}] Falta una textura del bake {state} en el indice {i}.", this);
                return false;
            }
        }

        return true;
    }

    private bool MatchesLoadedSceneLayout(LightmapSet set, LightmapState state)
    {
        if (_sceneLightmapCount <= 0 || set.colors.Length == _sceneLightmapCount)
            return true;

        int startIndex = Mathf.Max(0, _lightmapStartIndex);
        int endIndex = startIndex + _sceneLightmapCount - 1;
        int loadedCount = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;

        Debug.LogError(
            $"[{nameof(LightmapStateManager)}] El bake {state} tiene {set.colors.Length} atlas, pero los renderers baked de esta escena usan {_sceneLightmapCount} atlas desde el indice {startIndex} hasta el {endIndex}. LightmapSettings tiene {loadedCount} slots globales. No se aplica para evitar lightmaps cruzados/grises. Rehaz o reasigna ese bake usando el mismo LightingData de la escena.",
            this);
        return false;
    }

    private static bool HasShadowMasks(LightmapSet set)
    {
        return set.shadowMasks != null && set.shadowMasks.Length > 0;
    }

    private static bool IsEmpty(LightmapSet set)
    {
        return set.colors == null || set.colors.Length == 0;
    }

    private void FindSceneLightmapRange(out int startIndex, out int atlasCount)
    {
        int minimumIndex = int.MaxValue;
        int maximumIndex = -1;

        foreach (var sceneRenderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sceneRenderer.gameObject.scene != gameObject.scene)
                continue;

            int index = sceneRenderer.lightmapIndex;
            if (index >= 0 && index < 65534)
            {
                minimumIndex = Mathf.Min(minimumIndex, index);
                maximumIndex = Mathf.Max(maximumIndex, index);
            }
        }

        if (minimumIndex != int.MaxValue)
        {
            startIndex = minimumIndex;
            atlasCount = maximumIndex - minimumIndex + 1;
            return;
        }

        Debug.LogWarning($"[{nameof(LightmapStateManager)}] No se encontro ningun Renderer baked en la escena. Se usara el indice 0.", this);
        startIndex = 0;
        atlasCount = 0;
    }
}
