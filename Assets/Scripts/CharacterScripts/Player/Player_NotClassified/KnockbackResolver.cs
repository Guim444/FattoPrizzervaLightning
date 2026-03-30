using UnityEngine;

public enum AttackType { Clash, Punch, PunchRunning }

public readonly struct KnockbackResult
{
    public readonly float ForceOnTarget;
    public readonly float ForceOnSelf;
    public readonly string AnimatorTrigger;

    public KnockbackResult(float forceOnTarget, float forceOnSelf, string animatorTrigger)
    {
        ForceOnTarget   = forceOnTarget;
        ForceOnSelf     = forceOnSelf;
        AnimatorTrigger = animatorTrigger;
    }
}

/// <summary>
/// El rango es SOLO el endurance del player (-3 a +3).
/// No existe endurance del enemigo: solo el player controla el empuje.
///
/// Regla de diseño que la tabla respeta siempre:
///   ForceOnTarget:  Clash  <  Punch  <  PunchRunning
///   ForceOnSelf:    Clash  >  Punch  >  PunchRunning
///
/// End. muy negativo → player vuela, enemigo apenas se mueve.
/// End. muy positivo → player apenas retrocede, enemigo sale disparado.
/// </summary>
public static class KnockbackResolver
{
    //  Filas: endurance -3..+3 (índice 0..6)
    //  Cols:  Clash=0 | Punch=1 | PunchRunning=2

    private static readonly float[,] TargetForces = new float[7, 3]
    {
        //  Clash   Punch   PunchRun    endurance
        {    0.5f,   1.0f,   2.0f  },  // -3
        {    1.5f,   2.0f,   4.0f  },  // -2
        {    2.5f,   3.5f,   7.0f  },  // -1
        {    4.0f,   5.0f,  12.0f  },  //  0  ← Clash igual para ambos
        {    5.5f,   9.0f,  18.0f  },  // +1
        {    8.0f,  15.0f,  24.0f  },  // +2
        {   12.0f,  22.0f,  30.0f  },  // +3
    };

    private static readonly float[,] SelfForces = new float[7, 3]
    {
        //  Clash   Punch   PunchRun    endurance
        {   20.0f,  14.0f,   8.0f  },  // -3
        {   14.0f,  10.0f,   5.0f  },  // -2
        {    8.0f,   5.0f,   2.5f  },  // -1
        {    4.0f,   2.0f,   0.5f  },  //  0  ← Clash igual para ambos
        {    2.0f,   1.0f,   0.0f  },  // +1
        {    1.0f,   0.5f,   0.0f  },  // +2
        {    0.0f,   0.0f,   0.0f  },  // +3
    };

    private static readonly string[,] AnimTriggers = new string[7, 3]
    {
        { "ClashStagger", "HitStagger",  "HitStagger"  },  // -3
        { "ClashStagger", "HitStagger",  "HitStagger"  },  // -2
        { "ClashRecoil",  "PunchRecoil", "PunchRecoil" },  // -1
        { "ClashRecoil",  "PunchRecoil", "PunchRecoil" },  //  0
        { null,           null,           null          },  // +1
        { null,           null,           null          },  // +2
        { null,           null,           null          },  // +3
    };

    /// <summary>
    /// Solo necesita el endurance del player — no hay endurance del enemigo.
    /// </summary>
    public static KnockbackResult Resolve(
        AttackType attackType,
        int playerEndurance,
        KnockbackResolverConfig config = null)
    {
        int rowIndex = Mathf.Clamp(playerEndurance, -3, 3) + 3;  // 0..6
        int colIndex = (int)attackType;                            // 0..2

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

    public static AttackType StateToAttackType(State state)
    {
        switch (state)
        {
            case State.PunchRunning: return AttackType.PunchRunning;
            default:                 return AttackType.Punch;
        }
    }
}