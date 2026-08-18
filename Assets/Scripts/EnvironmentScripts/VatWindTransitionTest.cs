using UnityEngine;

/// <summary>
/// Controlador independiente para probar la transición global de la vegetación VAT
/// entre viento débil (0) y fuerte (1).
///
/// No debe estar activo al mismo tiempo que WindStateManager, ya que ambos
/// escriben la propiedad global _VAT_WindBlend.
/// </summary>
public sealed class VatWindTransitionTest : MonoBehaviour
{
    private static readonly int WindBlendId = Shader.PropertyToID("_VAT_WindBlend");

    [Header("Estado deseado")]
    [Tooltip("Puedes cambiar este checkbox durante Play Mode para iniciar la transición.")]
    [SerializeField] private bool strongWind = true;

    [Header("Transición")]
    [SerializeField, Min(0f)] private float transitionDuration = 1.5f;
    [SerializeField] private AnimationCurve transitionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Ignora Time.timeScale. Útil para probar la transición con el juego pausado.")]
    [SerializeField] private bool useUnscaledTime;

    [Header("Control de prueba")]
    [Tooltip("Permite alternar viento débil/fuerte con la tecla indicada.")]
    [SerializeField] private bool enableToggleKey = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Space;

    [Header("Depuración")]
    [SerializeField, Range(0f, 1f)] private float currentBlend = 1f;

    private bool _previousStrongWind;
    private float _transitionStartBlend;
    private float _transitionTargetBlend;
    private float _transitionElapsed;
    private bool _isTransitioning;

    public float CurrentBlend => currentBlend;
    public bool IsStrongWind => strongWind;
    public bool IsTransitioning => _isTransitioning;

    private void OnEnable()
    {
        _previousStrongWind = strongWind;
        currentBlend = strongWind ? 1f : 0f;
        _transitionTargetBlend = currentBlend;
        _isTransitioning = false;
        ApplyGlobalBlend();
    }

    private void Update()
    {
        if (enableToggleKey && Input.GetKeyDown(toggleKey))
            strongWind = !strongWind;

        // Detecta también cambios realizados desde el Inspector durante Play Mode.
        if (strongWind != _previousStrongWind)
        {
            _previousStrongWind = strongWind;
            BeginTransition(strongWind ? 1f : 0f);
        }

        if (!_isTransitioning)
            return;

        if (transitionDuration <= 0f)
        {
            currentBlend = _transitionTargetBlend;
            _isTransitioning = false;
            ApplyGlobalBlend();
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _transitionElapsed += deltaTime;
        float normalizedTime = Mathf.Clamp01(_transitionElapsed / transitionDuration);
        float curvedTime = transitionCurve != null
            ? Mathf.Clamp01(transitionCurve.Evaluate(normalizedTime))
            : normalizedTime;

        currentBlend = Mathf.Lerp(_transitionStartBlend, _transitionTargetBlend, curvedTime);
        ApplyGlobalBlend();

        if (normalizedTime >= 1f)
        {
            currentBlend = _transitionTargetBlend;
            _isTransitioning = false;
            ApplyGlobalBlend();
        }
    }

    [ContextMenu("Vegetation VAT Wind/Set Weak")]
    public void SetWeakWind()
    {
        SetStrongWind(false);
    }

    [ContextMenu("Vegetation VAT Wind/Set Strong")]
    public void SetStrongWind()
    {
        SetStrongWind(true);
    }

    [ContextMenu("Vegetation VAT Wind/Toggle")]
    public void ToggleWind()
    {
        SetStrongWind(!strongWind);
    }

    public void SetStrongWind(bool shouldBeStrong)
    {
        strongWind = shouldBeStrong;

        // Los métodos públicos deben reaccionar inmediatamente aunque se llamen
        // varias veces dentro del mismo frame.
        if (strongWind == _previousStrongWind &&
            Mathf.Approximately(_transitionTargetBlend, strongWind ? 1f : 0f))
            return;

        _previousStrongWind = strongWind;
        BeginTransition(strongWind ? 1f : 0f);
    }

    public void SetImmediate(bool shouldBeStrong)
    {
        strongWind = shouldBeStrong;
        _previousStrongWind = strongWind;
        currentBlend = strongWind ? 1f : 0f;
        _transitionTargetBlend = currentBlend;
        _isTransitioning = false;
        ApplyGlobalBlend();
    }

    private void BeginTransition(float targetBlend)
    {
        _transitionStartBlend = currentBlend;
        _transitionTargetBlend = Mathf.Clamp01(targetBlend);
        _transitionElapsed = 0f;
        _isTransitioning = !Mathf.Approximately(_transitionStartBlend, _transitionTargetBlend);

        if (!_isTransitioning)
        {
            currentBlend = _transitionTargetBlend;
            ApplyGlobalBlend();
        }
    }

    private void ApplyGlobalBlend()
    {
        Shader.SetGlobalFloat(WindBlendId, Mathf.Clamp01(currentBlend));
    }
}
