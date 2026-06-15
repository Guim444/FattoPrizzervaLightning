using System;
using UnityEngine;

/// <summary>
/// Cambia entre dos conjuntos de lightmaps que comparten el mismo atlas UV.
/// Solo sustituye los slots pertenecientes a esta escena para no afectar
/// a lightmaps de otras escenas cargadas de forma aditiva.
/// </summary>
public class LightmapStateManager : MonoBehaviour
{
    private enum LightmapState
    {
        Warm,
        Blue
    }

    [Serializable]
    private struct LightmapSet
    {
        public Texture2D[] colors;
        public Texture2D[] directions;
    }

    [Header("Bakes")]
    [SerializeField] private LightmapSet warm;
    [SerializeField] private LightmapSet blue;

    [Header("Startup")]
    [SerializeField] private bool applyWarmOnAwake = true;

    private int _lightmapStartIndex = -1;
    private int _currentState = -1;

    private void Awake()
    {
        _lightmapStartIndex = FindSceneLightmapStartIndex();

        if (applyWarmOnAwake)
            ApplyWarm();
    }

    public void ApplyWarm()
    {
        Apply(warm, LightmapState.Warm);
    }

    public void ApplyBlue()
    {
        Apply(blue, LightmapState.Blue);
    }

    private void Apply(LightmapSet set, LightmapState state)
    {
        if (_currentState == (int)state)
            return;

        if (!IsValid(set))
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
                lightmapDir = set.directions[i]
            };
        }

        LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;
        LightmapSettings.lightmaps = combined;
        _currentState = (int)state;
    }

    private bool IsValid(LightmapSet set)
    {
        if (set.colors == null || set.colors.Length == 0)
        {
            Debug.LogError($"[{nameof(LightmapStateManager)}] El conjunto no contiene lightmaps de color.", this);
            return false;
        }

        if (set.directions == null || set.directions.Length != set.colors.Length)
        {
            Debug.LogError($"[{nameof(LightmapStateManager)}] Color y direccion deben tener la misma cantidad de atlas.", this);
            return false;
        }

        for (int i = 0; i < set.colors.Length; i++)
        {
            if (set.colors[i] == null || set.directions[i] == null)
            {
                Debug.LogError($"[{nameof(LightmapStateManager)}] Falta una textura en el indice {i}.", this);
                return false;
            }
        }

        return true;
    }

    private int FindSceneLightmapStartIndex()
    {
        int minimumIndex = int.MaxValue;

        foreach (var sceneRenderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (sceneRenderer.gameObject.scene != gameObject.scene)
                continue;

            int index = sceneRenderer.lightmapIndex;
            if (index >= 0 && index < 65534)
                minimumIndex = Mathf.Min(minimumIndex, index);
        }

        if (minimumIndex != int.MaxValue)
            return minimumIndex;

        Debug.LogWarning($"[{nameof(LightmapStateManager)}] No se encontro ningun Renderer baked en la escena. Se usara el indice 0.", this);
        return 0;
    }
}
