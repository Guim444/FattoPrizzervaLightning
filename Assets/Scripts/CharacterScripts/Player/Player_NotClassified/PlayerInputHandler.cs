using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single responsibility: receive and store player input each frame.
/// Exposes read-only properties for other components to query.
/// ConsumeFrameInputs() must be called at the end of Update() to reset single-frame inputs.
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

    /// <summary>Normalized movement input for the current frame.</summary>
    public Vector2 MoveInput => _moveInput;

    /// <summary>True while the run button is held.</summary>
    public bool IsRunInputHeld { get; private set; }

    /// <summary>True while the punch button is held.</summary>
    public bool IsPunchInputHeld { get; private set; }

    /// <summary>True only on the frame the punch was pressed (consumed at end of Update).</summary>
    public bool IsPunchInputPressed { get; private set; }

    /// <summary>True only on the frame the interaction action was pressed.</summary>
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

        // Punch and Action are optional in the Player map.
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
                "PlayerInputHandler: action 'PunchStarted' not found in 'Player' map of Actions.inputactions.");
        }

        if (_actionAction != null)
        {
            _actionAction.started += OnActionStarted;
        }
        else
        {
            Debug.LogWarning(
                "PlayerInputHandler: action 'Run' not found in 'Player' map of Actions.inputactions.");
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
        _moveInput = Vector2.zero;
        IsRunInputHeld = false;
        IsPunchInputHeld = false;
        IsPunchInputPressed = false;
        IsActionInputPressed = false;
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
    /// Resets single-frame flags. Must be called at the end of PlayerController.Update().
    /// </summary>
    public void ConsumeFrameInputs()
    {
        IsPunchInputPressed = false;
        IsActionInputPressed = false;
    }

    public void ConsumePunchInput()
    {
        Debug.Log("Punch input consumed");
        IsPunchInputPressed = false;
    }
}
