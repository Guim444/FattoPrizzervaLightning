using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Responsabilidad única: recibir y almacenar el input del jugador en cada frame.
/// Expone propiedades de solo lectura para que otros componentes las consulten.
/// ConsumeFrameInputs() debe llamarse al final de Update() para resetear los inputs de un solo frame.
/// </summary>
[RequireComponent(typeof(PlayerInteractor))]
public class PlayerInputHandler : MonoBehaviour, Actions.IPlayerActions
{
    // ==================================================
    // INPUT SYSTEM STATE
    // ==================================================
    private Actions _actions;
    private Actions.PlayerActions _playerActions;
    private InputAction _punchAction;
    private InputAction _actionAction;
    private PlayerInteractor _interactor;

    // ==================================================
    // RAW INPUT STATE
    // ==================================================
    private Vector2 _moveInput;

    /// <summary>Input de movimiento normalizado del frame actual.</summary>
    public Vector2 MoveInput => _moveInput;

    /// <summary>True mientras el botón de correr esté pulsado.</summary>
    public bool IsRunInputHeld { get; private set; }

    /// <summary>True mientras el botón de puñetazo esté pulsado.</summary>
    public bool IsPunchInputHeld { get; private set; }

    /// <summary>True únicamente el frame en que se presionó el puñetazo (se consume al final del Update).</summary>
    public bool IsPunchInputPressed { get; private set; }

    /// <summary>True únicamente el frame en que se presionó la acción de interacción.</summary>
    public bool IsActionInputPressed { get; private set; }

    // ==================================================
    // INITIALIZATION
    // ==================================================

    void Awake()
    {
        _interactor = GetComponent<PlayerInteractor>();

        _actions = new Actions();
        _playerActions = _actions.Player;
        _playerActions.AddCallbacks(this);

        // Punch y Action son opcionales en el mapa Player.
        var playerMap = _playerActions.Get();
        _punchAction = playerMap.FindAction("PunchStarted", throwIfNotFound: false);
        _actionAction = playerMap.FindAction("Run", throwIfNotFound: false);

        if (_punchAction != null)
        {
            _punchAction.started += OnPunchStarted;
            _punchAction.canceled += OnPunchCanceled;
        }
        else
        {
            Debug.LogWarning(
                "PlayerInputHandler: no se encontró la acción 'PunchStarted' en el mapa 'Player' de Actions.inputactions.");
        }

        if (_actionAction != null)
        {
            _actionAction.started += OnActionStarted;
        }
        else
        {
            Debug.LogWarning(
                "PlayerInputHandler: no se encontró la acción 'Run' en el mapa 'Player' de Actions.inputactions.");
        }
    }

    void Update()
    {

    }

    void OnEnable()
    {
        _playerActions.Enable();
    }

    void OnDisable()
    {
        _playerActions.Disable();
    }

    void OnDestroy()
    {
        if (_punchAction != null)
        {
            _punchAction.started -= OnPunchStarted;
            _punchAction.canceled -= OnPunchCanceled;
        }

        if (_actionAction != null)
        {
            _actionAction.started -= OnActionStarted;
        }

        _playerActions.RemoveCallbacks(this);
        _actions.Dispose();
    }

    // ==================================================
    // INPUT SYSTEM CALLBACKS (Actions.IPlayerActions)
    // ==================================================

    public void OnMovement(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        IsRunInputHeld = context.ReadValueAsButton();
    }

    public void OnPunchStarted(InputAction.CallbackContext _)
    {
        IsPunchInputPressed = true;
        IsPunchInputHeld = true;
    }

    private void OnPunchCanceled(InputAction.CallbackContext _)
    {
        IsPunchInputHeld = false;
    }

    private void OnActionStarted(InputAction.CallbackContext _)
    {
        IsActionInputPressed = true;
        _interactor.TryInteract();
    }

    // ==================================================
    // FRAME CLEANUP
    // ==================================================

    /// <summary>
    /// Resetea los flags de un solo frame. Debe llamarse al final de PlayerController.Update().
    /// </summary>
    public void ConsumeFrameInputs()
    {
        IsPunchInputPressed = false;
        IsActionInputPressed = false;
    }

    public void ConsumePunchInput()
    {
        Debug.Log("presionado el puñetazo");
        IsPunchInputPressed = false;
    }
}
