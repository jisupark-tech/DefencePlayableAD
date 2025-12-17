using UnityEngine;

public interface ICombat
{
    void Attack(Transform target);
    void SetWeapon(IWeapon weapon);
    void SetTarget(Transform target);
    Transform GetCurrentTarget();
    bool CanAttack();
    float GetAttackRange();
    void SetAttackRange(float range);
}