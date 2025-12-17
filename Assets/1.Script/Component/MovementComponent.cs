using UnityEngine;

public class MovementComponent : MonoBehaviour, IMovable
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    
    private Vector3 currentVelocity;
    private bool isMoving = false;

    public void Move(Vector3 direction)
    {
        if (direction == Vector3.zero)
        {
            isMoving = false;
            currentVelocity = Vector3.zero;
            return;
        }

        direction = direction.normalized;
        Vector3 movement = direction * moveSpeed * Time.deltaTime;
        
        // GameManager를 통한 유효 위치 계산
        Vector3 newPosition = GameManager.Instance.m_Player.GetValidPlayerPosition(
            transform.position, 
            transform.position + movement
        );
        
        // Transform 직접 이동
        transform.position = newPosition;
        
        currentVelocity = direction * moveSpeed;
        isMoving = true;

        // 회전 처리
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0f, speed);
    }

    public Vector3 GetCurrentVelocity()
    {
        return currentVelocity;
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    void Update()
    {
        // FixedUpdate 대신 Update 사용 (Rigidbody 없으므로)
        if (!isMoving)
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, Time.deltaTime * 5f);
        }
    }
}