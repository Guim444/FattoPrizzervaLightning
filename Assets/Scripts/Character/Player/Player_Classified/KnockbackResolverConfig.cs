using UnityEngine;

/// <summary>
/// ScriptableObject that exposes all knockback table values
/// directly in the Unity Inspector.
///
/// SETUP:
///   Assets → Create → Combat → KnockbackResolverConfig
///   Assign the reference in CombatAttackHandler and ClashHandler.
///
/// If not assigned, KnockbackResolver uses its internal default values.
/// </summary>
[CreateAssetMenu(menuName = "Combat/KnockbackResolverConfig", fileName = "KnockbackResolverConfig")]
public class KnockbackResolverConfig : ScriptableObject
{
    // --------------------------------------------------------
    //  TARGET FORCES
    //  Rows: range -3 to +3 (index 0..6)
    //  Columns: Clash=0, Punch=1, PunchRunning=2
    // --------------------------------------------------------

    // Design rule that the table always respects:
    //   ForceOnTarget:  Clash < Punch < PunchRunning
    //   ForceOnSelf:    Clash > Punch > PunchRunning
    //
    // Very negative endurance → player flies, enemy barely moves.
    // Very positive endurance → enemy flies, player barely retreats.

    // end -1 = "RioTutte phase 3" state: player hits are minimal.

    [Header("Target forces — rows: range -3 to +3 | cols: Clash / Punch / PunchRun")]
    public Row[] targetForces = new Row[7]
    {
        new Row( 0.0f,  0.5f,  2.0f),  // range -3  enemy barely moves
        new Row( 0.0f,  1.0f,  4.0f),  // range -2
        new Row( 0.5f,  2.0f,  6.0f),  // range -1  ← RioTutte phase 3
        new Row( 4.0f, 10.0f, 22.0f),  // range  0
        new Row( 5.5f, 16.0f, 30.0f),  // range +1
        new Row( 8.0f, 24.0f, 38.0f),  // range +2
        new Row(12.0f, 32.0f, 46.0f),  // range +3  enemy flies
    };

    [Header("Self (recoil) forces — same structure")]
    public Row[] selfForces = new Row[7]
    {
        new Row(25.0f, 18.0f, 10.0f),  // range -3  player flies
        new Row(18.0f, 12.0f,  6.0f),  // range -2
        new Row(12.0f,  8.0f,  3.0f),  // range -1  ← RioTutte phase 3: player bounces
        new Row( 4.0f,  2.0f,  0.5f),  // range  0
        new Row( 2.0f,  1.0f,  0.0f),  // range +1
        new Row( 1.0f,  0.5f,  0.0f),  // range +2
        new Row( 0.0f,  0.0f,  0.0f),  // range +3
    };

    [Header("Animator triggers — empty = no special trigger")]
    public TriggerRow[] animTriggers = new TriggerRow[7]
    {
        new TriggerRow("ClashStagger", "HitStagger",  "HitStagger"),   // -3
        new TriggerRow("ClashStagger", "HitStagger",  "HitStagger"),   // -2
        new TriggerRow("ClashRecoil",  "PunchRecoil", "PunchRecoil"),  // -1
        new TriggerRow("ClashRecoil",  "PunchRecoil", "PunchRecoil"),  //  0
        new TriggerRow("",             "",             ""),             // +1
        new TriggerRow("",             "",             ""),             // +2
        new TriggerRow("",             "",             ""),             // +3
    };

    public float GetTargetForce(int rowIndex, int colIndex)
        => targetForces[rowIndex][colIndex];

    public float GetSelfForce(int rowIndex, int colIndex)
        => selfForces[rowIndex][colIndex];

    public string GetAnimTrigger(int rowIndex, int colIndex)
    {
        string t = animTriggers[rowIndex][colIndex];
        return string.IsNullOrEmpty(t) ? null : t;
    }

    [System.Serializable]
    public class Row
    {
        public float clash;
        public float punch;
        public float punchRunning;

        public Row(float c, float p, float pr)
        {
            clash        = c;
            punch        = p;
            punchRunning = pr;
        }

        public float this[int col] => col switch
        {
            0 => clash,
            1 => punch,
            2 => punchRunning,
            _ => 0f
        };
    }

    [System.Serializable]
    public class TriggerRow
    {
        public string clash;
        public string punch;
        public string punchRunning;

        public TriggerRow(string c, string p, string pr)
        {
            clash        = c;
            punch        = p;
            punchRunning = pr;
        }

        public string this[int col] => col switch
        {
            0 => clash,
            1 => punch,
            2 => punchRunning,
            _ => ""
        };
    }
}
