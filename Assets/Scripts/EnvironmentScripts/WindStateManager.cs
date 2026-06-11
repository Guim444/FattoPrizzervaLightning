using UnityEngine;

/// <summary>
/// Gestiona la ventisca de sprites mediante CrossFade entre 4 clips de Animator,
/// controla la emisión de un ParticleSystem por estado,
/// y activa la animación Alembic slow/fast de los árboles.
///
/// Animator setup (ventisca):
///   - 4 estados (uno por clip). Entry apunta al estado por defecto (W1_MaxIdle).
///   - Sin flechas entre estados.
///   - Asignar el nombre exacto de cada estado en windStates[].stateName.
///
/// Mapeo wind → árboles:
///   W1_MaxIdle / W2_MaxToMedium → fast
///   W3_MediumToMin / W4_MinToMedium → slow
///
/// Teclas de prueba: 1 y 4 → fast, 2 y 3 → slow
/// </summary>
public class WindStateManager : MonoBehaviour
{
    public enum WindPreset { W1_MaxIdle = 0, W2_MaxToMedium = 1, W3_MediumToMin = 2, W4_MinToMedium = 3 }

    [System.Serializable]
    public struct WindStateData
    {
        [Tooltip("Nombre exacto del estado en el Animator Controller.")]
        public string stateName;
        [Range(0f, 1f)]
        [Tooltip("Tiempo normalizado de CrossFade hacia este estado (0 = instantáneo).")]
        public float crossFadeTime;
        [Tooltip("Emisión de partículas por segundo para este estado.")]
        public float emissionRate;
    }

    [Header("Estados (0=W1, 1=W2, 2=W3, 3=W4)")]
    [SerializeField] private WindStateData[] windStates = new WindStateData[]
    {
        new WindStateData { stateName = "W1_MaxIdle",     crossFadeTime = 0f,   emissionRate = 500f  },
        new WindStateData { stateName = "W2_MaxToMedium", crossFadeTime = 0.1f, emissionRate = 1000f },
        new WindStateData { stateName = "W3_MediumToMin", crossFadeTime = 0.1f, emissionRate = 2000f },
        new WindStateData { stateName = "W4_MinToMedium", crossFadeTime = 0.1f, emissionRate = 4000f },
    };

    [Header("Animators de ventisca")]
    [SerializeField] private Animator[] windAnimators;

    [Header("Árboles Alembic")]
    [SerializeField] private AlembicTreeWindController[] treeControllers;

    [Header("Particle System")]
    [SerializeField] private ParticleSystem blizzardParticles;

    private int _currentStateIndex = -1;

    // ── Teclas de prueba — árboles ───────────────────────────────────────────
    // 1 y 4 → fast,  2 y 3 → slow
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha4))
            SetTreeWindSpeed(true);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Alpha3))
            SetTreeWindSpeed(false);
    }

    // ── API pública — viento ─────────────────────────────────────────────────

    /// <summary>Transiciona al preset dado usando el CrossFade configurado en el Inspector.</summary>
    public void TransitionTo(WindPreset preset, float crossFadeOverride = -1f)
    {
        int idx = (int)preset;
        if (idx == _currentStateIndex) return;
        _currentStateIndex = idx;

        float cft = crossFadeOverride >= 0f ? crossFadeOverride : windStates[idx].crossFadeTime;
        CrossFadeAll(windStates[idx].stateName, cft);
        SetEmission(windStates[idx].emissionRate);
        SetTreeWindSpeed(preset);
    }

    /// <summary>Salta instantáneamente al preset dado desde el frame 0 del clip.</summary>
    public void SnapToState(WindPreset preset)
    {
        int idx = (int)preset;
        _currentStateIndex = idx;
        CrossFadeAll(windStates[idx].stateName, 0f);
        SetEmission(windStates[idx].emissionRate);
        SetTreeWindSpeed(preset);
    }

    // ── Privado ──────────────────────────────────────────────────────────────

    private void CrossFadeAll(string stateName, float transitionTime)
    {
        foreach (var anim in windAnimators)
        {
            if (anim != null)
                anim.CrossFade(stateName, transitionTime, 0);
        }
    }

    private void SetEmission(float rate)
    {
        if (blizzardParticles == null) return;
        var emission = blizzardParticles.emission;
        emission.rateOverTime = rate;
    }

    // W1 y W2 = viento fuerte → fast. W3 y W4 = viento suave → slow.
    private void SetTreeWindSpeed(WindPreset preset)
    {
        bool fast = preset == WindPreset.W1_MaxIdle || preset == WindPreset.W2_MaxToMedium;
        SetTreeWindSpeed(fast);
    }

    private void SetTreeWindSpeed(bool fast)
    {
        if (treeControllers == null) return;
        foreach (var tree in treeControllers)
            if (tree != null) tree.SetFast(fast);
    }
}
