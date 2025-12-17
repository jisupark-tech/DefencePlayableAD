using UnityEngine;

public interface IWeapon
{
    void Fire(Vector3 startPosition, Transform target, BulletOwner owner, object shooter = null);
    float GetFireRate();
    float GetRange();
    string GetBulletType();
}