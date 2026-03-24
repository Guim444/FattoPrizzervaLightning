using UnityEngine;

/// <summary>
/// Responsabilidad única: gestionar la física de knockback del jugador.
/// Implementa IKnockbackable. Calcula las fuerzas según endurance, aplica
/// la velocidad de retroceso y decae hasta que se considera insignificante.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerKnockbackHandler : MonoBehaviour, IKnockbackable
{
    // ==================================================
    // KNOCKBACK FORCES
    // ==================================================
    [Header("Game Design: Knockback Forces")]
    public float pushForceSelf         = 10f;
    public float pushForceMostlySelf   = 5f;
    public float pushForceBoth         = 3.5f;
    public float pushForceMostlyOther  = 3f;
    public float pushForceOnlyOther    = 0f;
    public float pushForceOtherPlus    = 15f;
    public float sprintPunchMinPush    = 3f;
    public float pushForceBaseMultiplier = 2f;

    // ==================================================
    // KNOCKBACK STATE
    // ==================================================
    [Header("Knockback")]
    public bool    hasKnockback         = false;
    public Vector3 knockbackVelocity;
    public float   knockbackDecaySpeed  = 5f;
    public float   knockbackMinVelocity = 1f;

    // ==================================================
    // PRIVATE REFS
    // ==================================================
    private CharacterController _cc;
    private PlayerController    _player;

    // ==================================================
    // INITIALIZATION
    // ==================================================

    void Awake()
    {
        _cc     = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();
    }

    // ==================================================
    // PUBLIC API – IKnockbackable
    // ==================================================

    /// <summary>
    /// Calcula y aplica el impulso de knockback en función de la diferencia
    /// de endurance entre atacante y receptor.
    /// </summary>
    public void PushForce(Vector3 direction, int otherEndurance)
    {
        var combat = _player.combat;

        TypeOfDamage enduranceDistance = (TypeOfDamage)(combat.endurance - otherEndurance + 2);
        if ((int)enduranceDistance < 0)
            enduranceDistance = TypeOfDamage.PushOnlyOtherPlus;

        float pushMultiplier = ResolvePushMultiplier(enduranceDistance);

        if (combat.damageBoost >= 2)
            pushMultiplier = Mathf.Max(pushMultiplier, sprintPunchMinPush);

        combat.damageBoost = 0;

        if (pushMultiplier > 0)
        {
            knockbackVelocity = direction.normalized * pushMultiplier * pushForceBaseMultiplier;
            hasKnockback = true;
        }
    }

    // ==================================================
    // PUBLIC API – FRAME UPDATE
    // ==================================================

    /// <summary>
    /// Procesa el knockback activo en el frame actual.
    /// Debe llamarse al inicio de PlayerController.Update() cuando HP > 0.
    /// </summary>
    public void HandleKnockback()
    {
        if (!hasKnockback) return;

        if (_cc.enabled)
            _cc.Move(knockbackVelocity * Time.deltaTime);

        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecaySpeed * Time.deltaTime);

        _player.movement.LastDirection = Vector3.zero;
        _player.movement.CurrentSpeed  = Vector3.zero;

        if (knockbackVelocity.magnitude < knockbackMinVelocity)
        {
            hasKnockback      = false;
            knockbackVelocity = Vector3.zero;
            _player.currentState = State.Idle;
            StateMachine.SetState(State.Idle);
        }
    }

    // ==================================================
    // PRIVATE HELPERS
    // ==================================================

    private float ResolvePushMultiplier(TypeOfDamage enduranceDistance)
    {
        switch (enduranceDistance)
        {
            case TypeOfDamage.PushOnlySelf:      return pushForceSelf;
            case TypeOfDamage.PushMostlySelf:    return pushForceMostlySelf;
            case TypeOfDamage.PushBoth:          return pushForceBoth;
            case TypeOfDamage.PushMostlyOther:   return pushForceMostlyOther;
            case TypeOfDamage.PushOnlyOther:     return pushForceOnlyOther;
            case TypeOfDamage.PushOnlyOtherPlus: return pushForceOtherPlus;
            default:                             return 0f;
        }
    }
}
