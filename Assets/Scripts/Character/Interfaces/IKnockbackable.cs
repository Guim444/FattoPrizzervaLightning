using UnityEngine;

public interface IKnockbackable
{
    void ReceiveKnockback(Vector3 direction, float force);
}
