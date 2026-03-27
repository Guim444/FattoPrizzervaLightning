using UnityEngine;

/// <summary>
/// ScriptableObject que expone todos los valores de la tabla de knockback
/// directamente en el Inspector de Unity.
///
/// SETUP:
///   Assets → Create → Combat → KnockbackResolverConfig
///   Asignar la referencia en CombatAttackHandler y ClashHandler.
///
/// Si no se asigna, KnockbackResolver usa sus valores por defecto internos.
/// </summary>
[CreateAssetMenu(menuName = "Combat/KnockbackResolverConfig", fileName = "KnockbackResolverConfig")]
public class KnockbackResolverConfig : ScriptableObject
{
    // --------------------------------------------------------
    //  FUERZAS SOBRE EL TARGET
    //  Filas: rango -3 a +3 (índice 0..6)
    //  Columnas: Clash=0, Punch=1, PunchRunning=2
    // --------------------------------------------------------

    [Header("Target forces — filas: rango -3 a +3 | cols: Clash / Punch / PunchRun")]
    public Row[] targetForces = new Row[7]
    {
        new Row(18f, 22f, 20f),  // rango -3
        new Row(14f, 16f, 13f),  // rango -2
        new Row(10f, 11f,  9f),  // rango -1
        new Row( 5f,  4f,  4f),  // rango  0
        new Row( 3f,  3f,  5f),  // rango +1
        new Row( 5f, 12f, 18f),  // rango +2
        new Row(12f, 20f, 28f),  // rango +3
    };

    [Header("Self (recoil) forces — misma estructura")]
    public Row[] selfForces = new Row[7]
    {
        new Row( 0f,  0f,  0f),  // rango -3
        new Row( 2f,  1f,  0f),  // rango -2
        new Row( 5f,  4f,  6f),  // rango -1
        new Row( 5f,  3f,  3f),  // rango  0
        new Row( 3f,  3f,  2f),  // rango +1
        new Row( 1f,  1f,  0f),  // rango +2
        new Row( 0f,  0f,  0f),  // rango +3
    };

    [Header("Animator triggers — vacío = sin trigger especial")]
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

    // --------------------------------------------------------
    //  ACCESSORS (llamados por KnockbackResolver)
    // --------------------------------------------------------

    public float GetTargetForce(int rowIndex, int colIndex)
        => targetForces[rowIndex][colIndex];

    public float GetSelfForce(int rowIndex, int colIndex)
        => selfForces[rowIndex][colIndex];

    public string GetAnimTrigger(int rowIndex, int colIndex)
    {
        string t = animTriggers[rowIndex][colIndex];
        return string.IsNullOrEmpty(t) ? null : t;
    }

    // --------------------------------------------------------
    //  TIPOS AUXILIARES (serializables por Unity)
    // --------------------------------------------------------

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
