using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestiona los límites del ring del tutorial.
///
/// RESPONSABILIDADES:
///   – Define el área de combate con dos Transforms (leftBound / rightBound).
///   – Detecta cuando el enemigo alcanza el 75% del ring → sube su Endurance a 1.
///   – Detecta cuando cualquiera de los dos sale del ring → dispara el resultado.
///
/// SETUP EN EDITOR:
///   1. Crear un GameObject vacío "TutorialRingManager".
///   2. Crear dos GameObjects hijos "LeftBound" y "RightBound" y asignarlos.
///   3. Asignar Player y Enemy en el Inspector.
///   4. Conectar los UnityEvents (UI de victoria/derrota, etc.).
///
/// GIZMOS: Muestra el ring en amarillo y la marca de 3/4 en rojo en Scene View.
/// </summary>
public class TutorialRingManager : MonoBehaviour
{
    // ==================================================
    // RING CONFIGURATION
    // ==================================================
    [Header("Ring Bounds")]
    [Tooltip("Límite izquierdo del ring (posición X mínima).")]
    public Transform leftBound;

    [Tooltip("Límite derecho del ring (posición X máxima).")]
    public Transform rightBound;

    // ==================================================
    // FIGHTERS
    // ==================================================
    [Header("Fighters")]
    public PlayerController player;
    public RioTutteEnemy enemy;

    // ==================================================
    // CONFIGURATION
    // ==================================================
    [Header("Configuration")]
    [Tooltip("¿El enemigo empieza en el lado DERECHO del ring?\n" +
             "Afecta desde qué dirección se calcula el threshold de 3/4.")]
    public bool enemyStartsOnRight = true;

    [Tooltip("Valor de Endurance al que sube el enemigo cuando alcanza 3/4.")]
    public int enemyEnduranceUpgradeValue = 1;

    // ==================================================
    // EVENTS
    // ==================================================
    [Header("Events")]
    [Tooltip("El player salió del ring → derrota.")]
    public UnityEvent onPlayerOut;

    [Tooltip("El enemigo salió del ring → victoria.")]
    public UnityEvent onEnemyOut;

    [Tooltip("El enemigo fue empujado 3/4 del ring → sube su Endurance.")]
    public UnityEvent onEnemyEnduranceUpgrade;

    // ==================================================
    // PRIVATE STATE
    // ==================================================
    private bool _enemyThresholdTriggered = false;
    private bool _resultTriggered         = false;

    // ==================================================
    // SHORTCUTS
    // ==================================================
    float RingLeft  => leftBound.position.x;
    float RingRight => rightBound.position.x;
    float RingWidth => RingRight - RingLeft;

    // ==================================================
    // UPDATE
    // ==================================================

    void Update()
    {
        if (_resultTriggered) return;

        CheckEnemyThreshold();
        CheckOutOfBounds();
    }

    // ==================================================
    // THRESHOLD CHECK (3/4)
    // ==================================================

    void CheckEnemyThreshold()
    {
        if (_enemyThresholdTriggered) return;

        // Posición normalizada del enemigo dentro del ring (0 = left, 1 = right)
        float normalizedPos = (enemy.transform.position.x - RingLeft) / RingWidth;
        normalizedPos = Mathf.Clamp01(normalizedPos);

        // Si empieza a la derecha: ha sido empujado 3/4 cuando está en el 25% izquierdo
        // Si empieza a la izquierda: ha sido empujado 3/4 cuando está en el 75% derecho
        bool crossed = enemyStartsOnRight
            ? normalizedPos <= 0.25f
            : normalizedPos >= 0.75f;

        if (crossed)
        {
            _enemyThresholdTriggered = true;
            enemy.endurance = enemyEnduranceUpgradeValue;
            onEnemyEnduranceUpgrade.Invoke();
            Debug.Log($"[TutorialRing] Enemigo alcanzó 3/4 → Endurance = {enemyEnduranceUpgradeValue}");
        }
    }

    // ==================================================
    // OUT OF BOUNDS CHECK
    // ==================================================

    void CheckOutOfBounds()
    {
        float px = player.transform.position.x;
        float ex = enemy.transform.position.x;

        if (px < RingLeft || px > RingRight)
        {
            _resultTriggered = true;
            Debug.Log("[TutorialRing] Player salió del ring → DERROTA.");
            onPlayerOut.Invoke();
        }
        else if (ex < RingLeft || ex > RingRight)
        {
            _resultTriggered = true;
            Debug.Log("[TutorialRing] Enemigo salió del ring → VICTORIA.");
            onEnemyOut.Invoke();
        }
    }

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>Reinicia el estado del ring (para rejugar el tutorial).</summary>
    public void ResetRing()
    {
        _enemyThresholdTriggered = false;
        _resultTriggered         = false;
        Debug.Log("[TutorialRing] Ring reiniciado.");
    }

    // ==================================================
    // GIZMOS (visibles en Scene View)
    // ==================================================

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (leftBound == null || rightBound == null) return;

        float halfH = 2f;
        Vector3 center = (leftBound.position + rightBound.position) * 0.5f;

        // Ring completo (amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(RingWidth, halfH * 2f, 1f));

        // Línea de límites
        Gizmos.DrawLine(leftBound.position  + Vector3.up * halfH, leftBound.position  - Vector3.up * halfH);
        Gizmos.DrawLine(rightBound.position + Vector3.up * halfH, rightBound.position - Vector3.up * halfH);

        // Marca de 3/4 (rojo)
        float thresholdX = enemyStartsOnRight
            ? RingLeft + RingWidth * 0.25f
            : RingLeft + RingWidth * 0.75f;

        Gizmos.color = Color.red;
        Vector3 markBottom = new Vector3(thresholdX, center.y - halfH, center.z);
        Vector3 markTop    = new Vector3(thresholdX, center.y + halfH, center.z);
        Gizmos.DrawLine(markBottom, markTop);

        // Label "3/4"
        UnityEditor.Handles.Label(markTop + Vector3.up * 0.2f, "3/4 Threshold");
    }
#endif
}