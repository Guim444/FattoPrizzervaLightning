using UnityEngine;

/// <summary>
/// Motor de física de knockback compartido por el player y cualquier enemigo.
/// No es un MonoBehaviour: es un struct que vive dentro de los handlers.
///
/// FÍSICA:
///   - Decaimiento por fricción real:  v -= v * friction * dt
///     (no Lerp, que nunca llega a 0 y produce movimiento fantasma)
///   - Fricción de suelo vs aire:      groundFriction >> airFriction
///   - Masa:                           impulso inicial = force / mass
///     Un personaje pesado (endurance alto) absorbe más fuerza.
///   - Gravedad:                       componente Y baja a -gravityPull cuando está en aire.
///     Cuando el CC detecta suelo se resetea a -2 (estándar Unity).
///
/// USO:
///   1. Llamar Receive() al impactar.
///   2. Llamar Tick() cada frame en Update() mientras IsActive sea true.
///   3. El caller aplica el cc.Move resultante.
/// </summary>
[System.Serializable]
public struct KnockbackPhysicsBody
{
    // --------------------------------------------------------
    //  CONFIGURACIÓN (editable por Inspector en el handler)
    // --------------------------------------------------------

    [Tooltip("A mayor masa, menor desplazamiento al recibir el mismo impulso.\n" +
             "Valor sugerido: 1 + endurance * 0.4")]
    public float mass;

    [Tooltip("Fricción cuando el CC está en suelo. Decaimiento rápido = sensación de peso.")]
    public float groundFriction;

    [Tooltip("Fricción en el aire. Menor que groundFriction para conservar inercia.")]
    public float airFriction;

    [Tooltip("Velocidad mínima antes de detener el knockback (evita micro-vibraciones).")]
    public float stopThreshold;

    // --------------------------------------------------------
    //  ESTADO RUNTIME (no tocar desde Inspector)
    // --------------------------------------------------------

    /// <summary>Velocidad horizontal de knockback en world space.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>True mientras haya knockback activo.</summary>
    public bool IsActive { get; private set; }

    // --------------------------------------------------------
    //  CONSTRUCTOR DE VALORES POR DEFECTO
    // --------------------------------------------------------

    public static KnockbackPhysicsBody Default => new KnockbackPhysicsBody
    {
        mass          = 1f,
        groundFriction = 10f,
        airFriction   = 1.5f,
        stopThreshold = 0.15f,
    };

    // --------------------------------------------------------
    //  API PÚBLICA
    // --------------------------------------------------------

    /// <summary>
    /// Recibe un impulso de knockback.
    /// direction debe estar normalizado y sin componente Y (horizontal puro).
    /// </summary>
    public void Receive(Vector3 direction, float force)
    {
        if (mass <= 0f) mass = 1f;
        float effectiveForce = force / mass;

        // Sumamos en lugar de reemplazar para que dos impactos rápidos acumulen
        Velocity  += direction.normalized * effectiveForce;
        Velocity   = new Vector3(Velocity.x, 0f, Velocity.z); // solo plano horizontal
        IsActive   = true;
    }

    /// <summary>
    /// Actualiza la física cada frame. Devuelve el delta de movimiento
    /// que el caller debe pasar a cc.Move().
    /// </summary>
    /// <param name="isGrounded">cc.isGrounded del CharacterController.</param>
    /// <param name="deltaTime">Time.deltaTime.</param>
    /// <returns>Desplazamiento a aplicar con cc.Move().</returns>
    public Vector3 Tick(bool isGrounded, float deltaTime)
    {
        if (!IsActive) return Vector3.zero;

        float friction = isGrounded ? groundFriction : airFriction;

        // Decaimiento físico real: exponencial implícito con Euler explícito
        Velocity -= Velocity * friction * deltaTime;

        // Umbral de parada para evitar micro-vibraciones
        if (Velocity.magnitude < stopThreshold)
        {
            Velocity = Vector3.zero;
            IsActive = false;
            return Vector3.zero;
        }

        return Velocity * deltaTime;
    }

    /// <summary>
    /// Cancela el knockback inmediatamente (ej: el personaje muere o se agarra a algo).
    /// </summary>
    public void Cancel()
    {
        Velocity = Vector3.zero;
        IsActive = false;
    }

    /// <summary>
    /// Helper: devuelve la masa sugerida para un valor de endurance.
    /// Puede usarse en Awake() del handler para auto-configurar la masa.
    /// </summary>
    public static float MassFromEndurance(int endurance)
        => 1f + endurance * 0.4f;
}
