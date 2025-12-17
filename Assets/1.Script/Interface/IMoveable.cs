using UnityEngine;

public interface IMovable
{
    void Move(Vector3 direction);
    void SetMoveSpeed(float speed);
    Vector3 GetCurrentVelocity();
    bool IsMoving();
}