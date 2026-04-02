using UnityEngine;

public class PlayerBoundaryClamp : MonoBehaviour
{
    [Header("Límites en Z")]
    public float minZ;
    public float maxZ;

    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        Vector3 pos = transform.position;

        if (pos.z < minZ || pos.z > maxZ)
        {
            cc.enabled = false;
            pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
            transform.position = pos;
            cc.enabled = true;
        }
    }
}
