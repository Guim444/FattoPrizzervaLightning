using UnityEngine;

public enum AttackType { Clash, Punch, PunchRunning }

public readonly struct KnockbackResult
{
    public readonly float ForceOnTarget;
    public readonly float ForceOnSelf;

    public KnockbackResult(float forceOnTarget, float forceOnSelf, string animatorTrigger)
    {
        ForceOnTarget   = forceOnTarget;
        ForceOnSelf     = forceOnSelf;
    }
}

/// <summary>
/// The range is ONLY the player's endurance (-3 to +3).
/// Enemy endurance does not exist: only the player controls the push.
///
/// Design rule that the table always respects:
///   ForceOnTarget:  Clash  <  Punch  <  PunchRunning
///   ForceOnSelf:    Clash  >  Punch  >  PunchRunning
///
/// Very negative End. → player flies, enemy barely moves.
/// Very positive End. → player barely retreats, enemy launches.
/// </summary>
public static class KnockbackResolver
{
    //  Filas: endurance -3..+3 (índice 0..6)
    //  Cols:  Clash=0 | Punch=1 | PunchRunning=2

    private static readonly float[,] TargetForces = new float[7, 3]
    {
        //  Clash   Punch   PunchRun    endurance
        {    0.0f,   0.5f,   2.0f  },  // -3  player dominated → enemy barely moves
        {    0.0f,   1.0f,   4.0f  },  // -2
        {    0.5f,   8.0f,   15.0f  },  // -1  ← RioTutte phase 3: minimal hits
        {    4.0f,  10.0f,  22.0f  },  //  0  balanced
        {    5.5f,  16.0f,  30.0f  },  // +1
        {    8.0f,  24.0f,  38.0f  },  // +2
        {   12.0f,  32.0f,  46.0f  },  // +3  player domina → enemy sale disparado
    };

    private static readonly float[,] SelfForces = new float[7, 3]
    {
        //  Clash   Punch   PunchRun    endurance
        {   12.0f,  18.0f,  10.0f  },  // -3  player dominated → flies
        {    9.0f,  12.0f,   6.0f  },  // -2
        {    6.0f,   8.0f,   3.0f  },  // -1  ← RioTutte phase 3: player bounces
        {    2.0f,   2.0f,   0.5f  },  //  0  balanced clash: soft push
        {    1.0f,   1.0f,   0.0f  },  // +1
        {    0.5f,   0.5f,   0.0f  },  // +2
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
    /// Only needs the player's endurance — enemy endurance does not exist.
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

}
