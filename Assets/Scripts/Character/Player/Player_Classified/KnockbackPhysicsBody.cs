using UnityEngine;

/// <summary>
/// Knockback physics engine shared by the player and any enemy.
/// Not a MonoBehaviour: it is a struct that lives inside the handlers.
///
/// PHYSICS:
///   - Real friction decay:      v -= v * friction * dt
///     (not Lerp, which never reaches 0 and produces ghost movement)
///   - Ground vs air friction:   groundFriction >> airFriction
///   - Mass:                     initial impulse = force / mass
///     A heavy character (high endurance) absorbs more force.
///   - Gravity:                  Y component drops to -gravityPull when airborne.
///     When CC detects ground it resets to -2 (Unity standard).
///
/// USAGE:
///   1. Call Receive() on impact.
///   2. Call Tick() every frame in Update() while IsActive is true.
///   3. Caller applies the resulting cc.Move.
/// </summary>
[System.Serializable]
public struct KnockbackPhysicsBody
{

    [Tooltip("Higher mass → less displacement when receiving the same impulse.\n" +
             "Suggested value: 1 + endurance * 0.4")]
    public float mass;

    [Tooltip("Friction when CC is grounded. Fast decay = feeling of weight.")]
    public float groundFriction;

    [Tooltip("Friction in the air. Lower than groundFriction to preserve inertia.")]
    public float airFriction;

    [Tooltip("Minimum speed before stopping the knockback (avoids micro-vibrations).")]
    public float stopThreshold;

    //  RUNTIME STATE (do not touch from Inspector)

    /// <summary>Horizontal knockback velocity in world space.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>True while knockback is active.</summary>
    public bool IsActive { get; private set; }

    public static KnockbackPhysicsBody Default => new KnockbackPhysicsBody
    {
        mass          = 1f,
        groundFriction = 10f,
        airFriction   = 1.5f,
        stopThreshold = 0.15f,
    };

    /// <summary>
    /// Receives a knockback impulse.
    /// direction must be normalized and without Y component (horizontal only).
    /// </summary>
    public void Receive(Vector3 direction, float force)
    {
        if (mass <= 0f) mass = 1f;
        float effectiveForce = force / mass;

        // We add instead of replace so two rapid impacts accumulate
        Velocity  += direction.normalized * effectiveForce;
        Velocity   = new Vector3(Velocity.x, 0f, Velocity.z); // horizontal plane only
        IsActive   = true;
    }

    /// <summary>
    /// Updates physics each frame. Returns the movement delta
    /// that the caller must pass to cc.Move().
    /// </summary>
    /// <param name="isGrounded">cc.isGrounded from the CharacterController.</param>
    /// <param name="deltaTime">Time.deltaTime.</param>
    /// <returns>Displacement to apply with cc.Move().</returns>
    public Vector3 Tick(bool isGrounded, float deltaTime)
    {
        if (!IsActive) return Vector3.zero;

        float friction = isGrounded ? groundFriction : airFriction;

        // Real physical decay: implicit exponential with explicit Euler
        Velocity -= Velocity * friction * deltaTime;

        // Stop threshold to avoid micro-vibrations
        if (Velocity.magnitude < stopThreshold)
        {
            Velocity = Vector3.zero;
            IsActive = false;
            return Vector3.zero;
        }

        return Velocity * deltaTime;
    }

    /// <summary>
    /// Cancels knockback immediately (e.g.: character dies or grabs something).
    /// </summary>
    public void Cancel()
    {
        Velocity = Vector3.zero;
        IsActive = false;
    }

    /// <summary>
    /// Helper: returns the suggested mass for an endurance value.
    /// Can be used in the handler's Awake() to auto-configure mass.
    /// </summary>
    public static float MassFromEndurance(int endurance)
        => 1f + endurance * 0.4f;
}
