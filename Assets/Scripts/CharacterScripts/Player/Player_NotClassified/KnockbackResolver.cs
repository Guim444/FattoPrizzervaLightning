using UnityEngine;

// ============================================================
//  TIPOS DE DATOS
// ============================================================

/// <summary>
/// Tipo de ataque que origina el knockback.
/// Determina qué columna de la tabla de diseño se usa.
/// </summary>
public enum AttackType
{
    Clash,
    Punch,
    PunchRunning
}

/// <summary>
/// Resultado completo de un cálculo de knockback.
/// Contiene las fuerzas para ambos participantes y el trigger de animación.
/// </summary>
public readonly struct KnockbackResult
{
    /// <summary>Fuerza aplicada al receptor del golpe.</summary>
    public readonly float ForceOnTarget;

    /// <summary>
    /// Fuerza de reacción aplicada al atacante (Newton 3).
    /// 0 cuando el atacante es dominante (rango +3).
    /// </summary>
    public readonly float ForceOnSelf;

    /// <summary>
    /// Nombre del trigger de Animator que debe activarse en el atacante.
    /// Puede ser null si no corresponde ninguna animación especial.
    /// </summary>
    public readonly string AnimatorTrigger;

    public KnockbackResult(float forceOnTarget, float forceOnSelf, string animatorTrigger)
    {
        ForceOnTarget    = forceOnTarget;
        ForceOnSelf      = forceOnSelf;
        AnimatorTrigger  = animatorTrigger;
    }
}

// ============================================================
//  RESOLVER
// ============================================================

/// <summary>
/// Clase estática pura. Sin estado, sin MonoBehaviour.
/// Traduce directamente la tabla de diseño (rango -3 a +3) a fuerzas.
///
/// TABLA DE DISEÑO (Rango = attackerEndurance - receiverEndurance):
///
///  Rango | Clash                          | Punch                          | Punch-Running
///  ------+--------------------------------+--------------------------------+---------------------------
///   -3   | Atropella al player            | Desplaza al player muchísimo   | Desplaza mucho, casi sin retroceso
///   -2   | Desplaza al player muchísimo   | Desplaza mucho, casi no retro. | Desplaza al player, retrocede un poco
///   -1   | Desplaza mucho, casi no retro. | Desplaza al player, retrocede  | Player mueve enemigo, pero retrocede también
///    0   | Equilibrado, ambos retroceden  | Mueve un poco, retrocede un po | Mueve un poco, retrocede
///   +1   | Mueve un poco al enemigo       | Mueve al enemigo, retrocede    | Mueve más al enemigo, retrocede poco
///   +2   | Mueve un poco más              | Desplaza mucho, casi sin retro | Desplaza al enemigo muchísimo, apenas retrocede
///   +3   | Desplaza mucho, casi sin retro | Desplaza muchísimo, sin retro  | Atropella al enemigo
///
/// Todos los valores de fuerza son escalables desde el Inspector
/// a través de KnockbackResolverConfig (ScriptableObject).
/// </summary>
public static class KnockbackResolver
{
    // --------------------------------------------------------
    //  CONFIGURACIÓN POR DEFECTO (fallback si no hay config)
    // --------------------------------------------------------

    // Fuerzas sobre el TARGET indexadas por rango+3 (índice 0 = rango -3, índice 6 = rango +3)
    // Layout: [Clash, Punch, PunchRunning]
    private static readonly float[,] TargetForces = new float[7, 3]
    {
        //  Clash   Punch   PunchRun    (rango)
        {   18f,    22f,    20f     },  // -3
        {   14f,    16f,    13f     },  // -2
        {   10f,    11f,     9f     },  // -1
        {    5f,     4f,     4f     },  //  0
        {    3f,     3f,     5f     },  // +1
        {    5f,    12f,    18f     },  // +2
        {   12f,    20f,    28f     },  // +3
    };

    // Fuerzas de reacción sobre el ATACANTE (self)
    private static readonly float[,] SelfForces = new float[7, 3]
    {
        //  Clash   Punch   PunchRun    (rango)
        {    0f,     0f,     0f     },  // -3  (target domina completamente)
        {    2f,     1f,     0f     },  // -2
        {    5f,     4f,     6f     },  // -1
        {    5f,     3f,     3f     },  //  0  (equilibrio)
        {    3f,     3f,     2f     },  // +1
        {    1f,     1f,     0f     },  // +2
        {    0f,     0f,     0f     },  // +3  (atacante domina completamente)
    };

    // Triggers de Animator por tipo de ataque y rango
    // null = sin trigger especial (animación normal del estado)
    private static readonly string[,] AnimTriggers = new string[7, 3]
    {
        //  Clash               Punch               PunchRun
        {   "ClashStagger",    "HitStagger",        "HitStagger"    },  // -3
        {   "ClashStagger",    "HitStagger",        "HitStagger"    },  // -2
        {   "ClashRecoil",     "PunchRecoil",       "PunchRecoil"   },  // -1
        {   "ClashRecoil",     "PunchRecoil",       "PunchRecoil"   },  //  0
        {   null,              null,                null            },  // +1
        {   null,              null,                null            },  // +2
        {   null,              null,                null            },  // +3
    };

    // --------------------------------------------------------
    //  API PÚBLICA
    // --------------------------------------------------------

    /// <summary>
    /// Calcula fuerzas de knockback para ambos participantes.
    /// </summary>
    /// <param name="attackType">Tipo de ataque (Clash, Punch, PunchRunning).</param>
    /// <param name="attackerEndurance">Endurance del que ataca.</param>
    /// <param name="receiverEndurance">Endurance del que recibe.</param>
    /// <param name="config">Config de ScriptableObject. Si es null usa valores por defecto.</param>
    public static KnockbackResult Resolve(
        AttackType attackType,
        int attackerEndurance,
        int receiverEndurance,
        KnockbackResolverConfig config = null)
    {
        int range      = Mathf.Clamp(attackerEndurance - receiverEndurance, -3, 3);
        int rowIndex   = range + 3;          // 0..6
        int colIndex   = (int)attackType;    // 0..2

        float forceTarget;
        float forceSelf;
        string animTrigger;

        if (config != null)
        {
            forceTarget = config.GetTargetForce(rowIndex, colIndex);
            forceSelf   = config.GetSelfForce(rowIndex, colIndex);
            animTrigger = config.GetAnimTrigger(rowIndex, colIndex);
        }
        else
        {
            forceTarget = TargetForces[rowIndex, colIndex];
            forceSelf   = SelfForces[rowIndex, colIndex];
            animTrigger = AnimTriggers[rowIndex, colIndex];
        }

        return new KnockbackResult(forceTarget, forceSelf, animTrigger);
    }

    /// <summary>
    /// Determina el AttackType a partir del estado actual del jugador.
    /// Proxy limpio entre la state machine y el resolver.
    /// </summary>
    public static AttackType StateToAttackType(State state)
    {
        switch (state)
        {
            case State.PunchRunning: return AttackType.PunchRunning;
            case State.Punching:     return AttackType.Punch;
            default:                 return AttackType.Clash;
        }
    }
}
