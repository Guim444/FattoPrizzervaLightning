using UnityEngine;

/// <summary>
/// Define el área jugable mediante 4 Transforms como marcadores de escena.
/// Clampea X y Z del player como paredes sólidas en LateUpdate.
/// Se puede activar/desactivar en caliente desde el Inspector o por código.
/// </summary>
public class PlayableAreaManager : MonoBehaviour
{
    // ==================================================
    // MARCADORES DEL ÁREA
    // ==================================================
    [Header("Marcadores del área (mover en Scene View)")]
    public Transform leftBound;
    public Transform rightBound;
    public Transform frontBound;
    public Transform backBound;

    [Header("Personaje")]
    public PlayerController player;
    [Header("Activación")]
    public bool isActive = true;

    private CharacterController _playerCC;


    float MinX => leftBound.position.x;
    float MaxX => rightBound.position.x;
    float MinZ => frontBound.position.z;
    float MaxZ => backBound.position.z;


    void Awake()
    {
        if (player != null)
            _playerCC = player.GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (!isActive) return;
        ClampCharacter(player != null ? player.transform : null, _playerCC);
    }

    void ClampCharacter(Transform t, CharacterController cc)
    {
        if (t == null || cc == null) return;
        if (leftBound == null || rightBound == null ||
            frontBound == null || backBound == null) return;

        float buffer = cc.radius + cc.skinWidth;

        float minX = MinX + buffer;
        float maxX = MaxX - buffer;
        float minZ = MinZ + buffer;
        float maxZ = MaxZ - buffer;

        Vector3 pos = t.position;
        bool outX = pos.x < minX || pos.x > maxX;
        bool outZ = pos.z < minZ || pos.z > maxZ;

        if (!outX && !outZ) return;

        cc.enabled = false;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        t.position = pos;
        cc.enabled = true;
    }


    /// <summary>
    /// Activa o desactiva el área jugable por código.
    /// Ejemplo: playableArea.SetActive(false) al entrar al ring de combate. O cuando se haga el trigger de entrar a la iglesia 
    /// </summary>
    public void SetActive(bool active) => isActive = active;


#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (leftBound == null || rightBound == null ||
            frontBound == null || backBound == null) return;

        float cx     = (MinX + MaxX) * 0.5f;
        float cz     = (MinZ + MaxZ) * 0.5f;
        float cy     = leftBound.position.y;
        float width  = MaxX - MinX;
        float depth  = MaxZ - MinZ;

        Gizmos.color = isActive ? Color.green : Color.gray;

        // Suelo del área
        Gizmos.DrawWireCube(
            new Vector3(cx, cy, cz),
            new Vector3(width, 0.05f, depth));

        // Paredes X (izquierda/derecha)
        DrawWall(MinX, cy, cz, depth, wallOnX: true);
        DrawWall(MaxX, cy, cz, depth, wallOnX: true);

        // Paredes Z (frente/fondo)
        DrawWall(cx, cy, MinZ, width, wallOnX: false);
        DrawWall(cx, cy, MaxZ, width, wallOnX: false);

        UnityEditor.Handles.Label(
            new Vector3(cx, cy + 0.4f, cz),
            isActive ? "Área Jugable [ON]" : "Área Jugable [OFF]");
    }

    void DrawWall(float x, float y, float z, float length, bool wallOnX)
    {
        float h    = 2f;
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