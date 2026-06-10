using UnityEngine;
using UnityEngine.Events;

public class TutorialRingManager : MonoBehaviour
{
    // ==================================================
    // RING BOUNDS
    // ==================================================
    [Header("Ring Bounds — X (solid walls)")]
    public Transform leftBound;

    public Transform rightBound;

    [Header("Ring Bounds — Z (victory zone)")]
    public Transform frontBound;

    public Transform backBound;

    // ==================================================
    // FIGHTERS
    // ==================================================
    [Header("Fighters")]
    public PlayerController player;

    public RioTutteEnemy enemy;

    [SerializeField]
    private Vector3 playerStartPosition;

    [SerializeField]
    private Vector3 enemyStartPosition;

    // ==================================================
    // CONFIGURATION
    // ==================================================
    [Header("Configuration")]
    [Tooltip("Player's starting endurance when the ring starts / resets.")]
    public int playerStartEndurance = 0;

    [Tooltip("Endurance the player drops to when the enemy reaches 3/4 of the ring.")]
    public int playerPenaltyEndurance = -1;

    // ==================================================
    // EVENTS
    // ==================================================
    [Header("Events")]
    public UnityEvent onPlayerOut;

    public UnityEvent onEnemyOut;

    // ==================================================
    // RETRY CANVAS
    // ==================================================
    [Header("Retry")]
    [Tooltip("Canvas shown when the player loses (HP 0 or exits the ring).")]
    [SerializeField] private GameObject retryCanvas;

    [Tooltip("Reference to TutorialManager to reset Endurance milestones on each retry.")]
    [SerializeField] private TutorialManager tutorialManager;

    // ==================================================
    // PRIVATE
    // ==================================================
    private CharacterController _playerCC;
    private CharacterController _enemyCC;

    private bool  _enemyThresholdTriggered = false;
    private bool  _resultTriggered         = false;
    private float _enemyStartHP;

    // ==================================================
    // SHORTCUTS
    // ==================================================
    float RingMinX => leftBound.position.x;
    float RingMaxX => rightBound.position.x;
    float RingMinZ => frontBound.position.z;
    float RingMaxZ => backBound.position.z;
    float RingWidth => RingMaxX - RingMinX;
    float RingDepth => RingMaxZ - RingMinZ;

    // ==================================================
    // UNITY
    // ==================================================

    void Awake()
    {
        if (player != null)
        {
            _playerCC = player.GetComponent<CharacterController>();
            playerStartPosition = player.transform.position;
        }
        if (enemy != null)
        {
            _enemyCC = enemy.GetComponent<CharacterController>();
            enemyStartPosition = enemy.transform.position;
            _enemyStartHP      = enemy.HP;
        }
    }

    void Update()
    {
        if (_resultTriggered) return;
        CheckEnemyThreshold();
        CheckOutOfBounds();
    }

    void LateUpdate()
    {
        // Clamp X para ambos luchadores (paredes sólidas)
        ClampX(player != null ? player.transform : null, _playerCC);
        ClampX(enemy != null ? enemy.transform : null, _enemyCC);
    }

    // ==================================================
    // CLAMP X
    // ==================================================

    void ClampX(Transform t, CharacterController cc)
    {
        if (t == null || cc == null) return;

        float buffer = cc.radius + cc.skinWidth;
        float minX = RingMinX + buffer;
        float maxX = RingMaxX - buffer;

        float x = t.position.x;
        if (x >= minX && x <= maxX) return;

        cc.enabled = false;
        Vector3 pos = t.position;
        pos.x = Mathf.Clamp(x, minX, maxX);
        t.position = pos;
        cc.enabled = true;
    }

    // ==================================================
    // THRESHOLD CHECK (3/4 del ring en Z)
    // ==================================================

    void CheckEnemyThreshold()
    {
        if (_enemyThresholdTriggered || enemy == null) return;

        float normalized = Mathf.Clamp01(
            (enemy.transform.position.z - RingMinZ) / RingDepth);

        if (normalized >= 0.75f)
        {
            _enemyThresholdTriggered = true;
            player.Combat.endurance = playerPenaltyEndurance;
        }
    }

    // ==================================================
    // OUT OF BOUNDS — Z
    // ==================================================

    void CheckOutOfBounds()
    {
        if (player != null)
        {
            float pz = player.transform.position.z;
            if (pz < RingMinZ || pz > RingMaxZ)
            {
                _resultTriggered = true;
                Debug.Log("[TutorialRing] Player left the ring → DEFEAT.");
                onPlayerOut.Invoke();
                return;
            }
        }

        if (enemy != null)
        {
            float ez = enemy.transform.position.z;
            if (ez < RingMinZ || ez > RingMaxZ)
            {
                _resultTriggered = true;
                Debug.Log("[TutorialRing] Enemy left the ring → VICTORY.");
                onEnemyOut.Invoke();
            }
        }
    }

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Muestra el canvas de retry y pausa el juego.
    /// Conectar a PlayerCombat.onDied y a TutorialRingManager.onPlayerOut en el Inspector.
    /// </summary>
    public void ShowRetry()
    {
        Time.timeScale = 0f;
        if (retryCanvas != null) retryCanvas.SetActive(true);
    }

    /// <summary>
    /// Llamado por el botón "Reintentar" del canvas.
    /// Resetea posiciones y vida del player, pero mantiene la fase actual de RioTutte.
    /// </summary>
    public void Retry()
    {
        if (retryCanvas != null) retryCanvas.SetActive(false);
        Time.timeScale = 1f;

        RestartPositions();
        _enemyThresholdTriggered = false;
        _resultTriggered         = false;

        if (player != null)
        {
            player.Combat.HP       = player.Combat.maxHP;
            player.Combat.endurance = playerStartEndurance;
            player.canMove         = true;
            player.animator.SetBool("isDead", false);
        }

        if (enemy != null)
        {
            enemy.HP        = _enemyStartHP;
            enemy.activeAI  = true;
            enemy.ResetPhaseCounters();
        }

        tutorialManager?.ResetMilestones();

        Debug.Log("[TutorialRing] Retry — RioTutte phase preserved.");
    }

    public void ResetRing()
    {
        RestartPositions();
        _enemyThresholdTriggered = false;
        _resultTriggered = false;
        if (player != null)
            player.Combat.endurance = playerStartEndurance;
        Debug.Log("[TutorialRing] Ring reset.");
            player.Combat.endurance = playerStartEndurance;
        Debug.Log("[TutorialRing] Ring reiniciado.");
    }

    public void RestartPositions()
    {
        TeleportTo(player != null ? player.transform : null, _playerCC, playerStartPosition);
        TeleportTo(enemy != null ? enemy.transform : null, _enemyCC, enemyStartPosition);
    }

    /// <summary>
    /// Actualiza las posiciones de inicio con las posiciones actuales de player y enemigo.
    /// Llamar desde HUDManager justo después de teleportar al player a su posición de combate.
    /// </summary>
    public void RefreshStartPositions()
    {
        if (player != null) playerStartPosition = player.transform.position;
        if (enemy != null)  enemyStartPosition  = enemy.transform.position;
    }

    void TeleportTo(Transform t, CharacterController cc, Vector3 destination)
    {
        if (t == null) return;
        if (cc != null) cc.enabled = false;
        t.position = destination;
        if (cc != null) cc.enabled = true;
    }
    // ==================================================
    // GIZMOS
    // ==================================================

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (leftBound == null || rightBound == null ||
            frontBound == null || backBound == null) return;

        float cx = (RingMinX + RingMaxX) * 0.5f;
        float cz = (RingMinZ + RingMaxZ) * 0.5f;
        float cy = leftBound.position.y;

        // Suelo del ring (amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(cx, cy, cz),
            new Vector3(RingWidth, 0.05f, RingDepth));

        // Paredes X — cian (sólidas)
        Gizmos.color = Color.cyan;
        DrawWall(RingMinX, cy, cz, RingDepth, wallOnX: true);
        DrawWall(RingMaxX, cy, cz, RingDepth, wallOnX: true);

        // Bordes Z — rojo (victoria/derrota)
        Gizmos.color = Color.red;
        DrawWall(cx, cy, RingMinZ, RingWidth, wallOnX: false);
        DrawWall(cx, cy, RingMaxZ, RingWidth, wallOnX: false);

        // Marca 3/4 en Z
        float threshZ = RingMinZ + RingDepth * 0.75f;
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawLine(new Vector3(RingMinX, cy, threshZ),
            new Vector3(RingMaxX, cy, threshZ));
        UnityEditor.Handles.Label(
            new Vector3(cx, cy + 0.3f, threshZ), "3/4 Threshold");
    }

    void DrawWall(float x, float y, float z, float length, bool wallOnX)
    {
        float h = 1.5f;
        float half = length * 0.5f;

        Vector3 a = wallOnX
            ? new Vector3(x, y, z - half)
            : new Vector3(x - half, y, z);
        Vector3 b = wallOnX
            ? new Vector3(x, y, z + half)
            : new Vector3(x + half, y, z);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(a + Vector3.up * h, b + Vector3.up * h);
        Gizmos.DrawLine(a, a + Vector3.up * h);
        Gizmos.DrawLine(b, b + Vector3.up * h);
    }
#endif
}
