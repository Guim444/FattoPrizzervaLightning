using UnityEngine;

public interface IKnockbackable
{
    void PushForce(Vector3 direction, int otherEndurance);
}
