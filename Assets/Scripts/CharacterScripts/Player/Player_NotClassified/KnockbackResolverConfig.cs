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

    // Regla de diseño que la tabla respeta siempre:
    //   ForceOnTarget:  Clash < Punch < PunchRunning
    //   ForceOnSelf:    Clash > Punch > PunchRunning
    //
    // Endurance muy negativo → el player vuela, el enemigo apenas se mueve.
    // Endurance muy positivo → el enemigo sale disparado, el player apenas retrocede.

    // end -1 = estado de "fase 3 RioTutte": golpes del player son mínimos.

    [Header("Target forces — filas: rango -3 a +3 | cols: Clash / Punch / PunchRun")]
    public Row[] targetForces = new Row[7]
    {
        new Row( 0.0f,  0.5f,  2.0f),  // rango -3  enemy apenas se mueve
        new Row( 0.0f,  1.0f,  4.0f),  // rango -2
        new Row( 0.5f,  2.0f,  6.0f),  // rango -1  ← fase 3 RioTutte
        new Row( 4.0f, 10.0f, 22.0f),  // rango  0
        new Row( 5.5f, 16.0f, 30.0f),  // rango +1
        new Row( 8.0f, 24.0f, 38.0f),  // rango +2
        new Row(12.0f, 32.0f, 46.0f),  // rango +3  enemy vuela
    };

    [Header("Self (recoil) forces — misma estructura")]
    public Row[] selfForces = new Row[7]
    {
        new Row(25.0f, 18.0f, 10.0f),  // rango -3  player sale volando
        new Row(18.0f, 12.0f,  6.0f),  // rango -2
        new Row(12.0f,  8.0f,  3.0f),  // rango -1  ← fase 3 RioTutte: player rebota
        new Row( 4.0f,  2.0f,  0.5f),  // rango  0
        new Row( 2.0f,  1.0f,  0.0f),  // rango +1
        new Row( 1.0f,  0.5f,  0.0f),  // rango +2
        new Row( 0.0f,  0.0f,  0.0f),  // rango +3
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
