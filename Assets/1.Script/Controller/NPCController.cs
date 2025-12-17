using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class NPCController : MonoBehaviour
{
    [Header("NPC Settings")]
    public float moveSpeed = 3f;
    public float ReachDistance = 0.2f;

    [Header("Pathfinding Settings")]
    public float avoidanceRange = 2.5f; // Distance to detect and avoid buildings
    public float pathUpdateRate = 0.2f; // How often to recalculate path
    public float obstacleCheckDistance = 1.5f; // Forward obstacle detection distance
    public LayerMask buildingLayer = -1; // Layer mask for buildings to avoid
    public float stuckThreshold = 0.1f; // Distance threshold to consider NPC as stuck
    public float stuckTimeLimit = 2f; // Time limit before trying alternative path

    [Header("Safety Settings")]
    public float maxMovementTime = 60f; // Maximum movement time (1 minute) - prevent infinite loop
    public float maxDistanceFromStart = 50f; // Maximum distance from starting position

    [Header("Animation")]
    [SerializeField] private string MoveParameter;

    private IHealth m_targetController;
    private Transform m_targetTransform;
    private Animator anim;

    // Pathfinding variables
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private bool isMovingToTarget = false;
    private Vector3 currentWaypoint;
    private List<Vector3> avoidancePoints = new List<Vector3>();

    // Safety variables
    private float movementStartTime = 0f;
    private Vector3 startPosition = Vector3.zero;
    private bool isTimeoutReached = false;

    public void Init(Transform _controller = null)
    {
        if (_controller != null)
        {
            m_targetTransform = _controller;
            m_targetController = _controller.gameObject.GetComponent<IHealth>();

            if (anim == null)
                anim = GetComponent<Animator>();
            if (anim == null)
                anim = GetComponentInChildren<Animator>();

            // Initialize pathfinding
            lastPosition = transform.position;
            startPosition = transform.position; // Store starting position
            currentWaypoint = m_targetTransform.position;
            isMovingToTarget = true;
            movementStartTime = Time.time; // Record movement start time
            isTimeoutReached = false;

            StartCoroutine(MoveToControllerWithAvoidance());
        }
        else
        {
#if !PLAYABLE_AD
            Debug.LogError("NPCController: Target controller is null!");
#endif

            // If target is null, return to pool immediately
            ObjectPool.Instance.ReturnToPool(this.gameObject);
        }
    }

    IEnumerator MoveToControllerWithAvoidance()
    {
        if (m_targetController == null || m_targetTransform == null)
        {
#if !PLAYABLE_AD
            Debug.LogError("NPCController: Target is null, returning to pool");
#endif

            ObjectPool.Instance.ReturnToPool(this.gameObject);
            yield break;
        }

        while (isMovingToTarget && !isTimeoutReached && gameObject.activeInHierarchy)
        {
            // Time limit check (prevent infinite loop)
            if (Time.time - movementStartTime > maxMovementTime)
            {
#if !PLAYABLE_AD
                Debug.LogWarning($"NPC {gameObject.name} movement timeout reached. Forcing completion.");
#endif

                isTimeoutReached = true;
                ForceReachTarget();
                yield break;
            }

            // Check if moved too far from starting position
            float distanceFromStart = Vector3.Distance(transform.position, startPosition);
            if (distanceFromStart > maxDistanceFromStart)
            {
#if !PLAYABLE_AD
                Debug.LogWarning($"NPC {gameObject.name} moved too far from start ({distanceFromStart:F1}). Teleporting closer to target.");
#endif

                TeleportToSafePosition();
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // Check if target is still valid
            if (m_targetTransform == null || !m_targetTransform.gameObject.activeInHierarchy)
            {
#if !PLAYABLE_AD
                Debug.LogWarning($"NPC {gameObject.name} target became invalid during movement");
#endif

                ObjectPool.Instance.ReturnToPool(this.gameObject);
                yield break;
            }

            float distanceToTarget = Vector3.Distance(transform.position, m_targetTransform.position);

            // Check if reached target
            if (distanceToTarget <= ReachDistance)
            {
                ReachTarget();
                yield break;
            }

            // Update animation
            //UpdateAnimation(true);

            // Check for obstacles and calculate movement
            Vector3 moveDirection = CalculateMovementDirection();

            // Move the NPC
            if (moveDirection != Vector3.zero)
            {
                Vector3 newPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;

                // Check if new position is safe
                if (IsSafePosition(newPosition))
                {
                    transform.position = newPosition;

                    // Rotate towards movement direction
                    if (moveDirection.magnitude > 0.1f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                    }
                }
                else
                {
                    // If position is not safe, find alternative path
                    HandleUnsafePosition();
                }
            }

            // Check if stuck and handle accordingly
            CheckIfStuckAndHandle();

            // Update path periodically
            if (Time.fixedTime % pathUpdateRate < Time.fixedDeltaTime)
            {
                UpdateCurrentWaypoint();
            }

            yield return null;
        }

        // If loop ended without reaching target
        if (!isTimeoutReached)
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"NPC {gameObject.name} exited movement loop without reaching target");
#endif

            ObjectPool.Instance.ReturnToPool(this.gameObject);
        }
    }

    /// <summary>
    /// Check if position is safe (map boundaries, building collisions etc)
    /// </summary>
    bool IsSafePosition(Vector3 position)
    {
        // Map boundary check (simple range limit)
        if (Mathf.Abs(position.x) > 100f || Mathf.Abs(position.z) > 100f)
        {
            return false;
        }

        // Building collision check
        if (GameManager.Instance != null)
        {
            return !GameManager.Instance.IsPositionCollidingWithBuilding(position, transform, false, true);
        }

        return true;
    }

    /// <summary>
    /// Handle when NPC reaches unsafe position
    /// </summary>
    void HandleUnsafePosition()
    {
        // Return to previous position
        if (lastPosition != Vector3.zero)
        {
            transform.position = lastPosition;
        }

        // Increase stuck timer (induce forced escape)
        stuckTimer += Time.deltaTime * 2f; // Increase 2x faster
    }

    /// <summary>
    /// Teleport to safe position
    /// </summary>
    void TeleportToSafePosition()
    {
        if (m_targetTransform == null) return;

        // Move to safe position near target
        Vector3 directionToTarget = (m_targetTransform.position - transform.position).normalized;
        Vector3 safePosition = transform.position + directionToTarget * 5f; // Move 5 units closer to target

        // Check if this position is safe
        if (IsSafePosition(safePosition))
        {
            transform.position = safePosition;
            lastPosition = safePosition;
        }
        else
        {
            // If that doesn't work, try moving directly to target vicinity
            Vector3 targetVicinity = m_targetTransform.position - directionToTarget * 3f; // 3 units away from target
            if (IsSafePosition(targetVicinity))
            {
                transform.position = targetVicinity;
                lastPosition = targetVicinity;
            }
        }
    }

    void UpdateCurrentWaypoint()
    {
        if (m_targetTransform == null) return;

        currentWaypoint = m_targetTransform.position;

        // Clear old avoidance points
        if (avoidancePoints.Count > 5)
        {
            avoidancePoints.Clear();
        }
    }

    Vector3 CalculateMovementDirection()
    {
        if (m_targetTransform == null) return Vector3.zero;

        Vector3 directionToTarget = (currentWaypoint - transform.position).normalized;

        // Check for obstacles in direct path
        if (!IsObstacleInDirection(directionToTarget))
        {
            return directionToTarget;
        }

        // Find avoidance direction
        Vector3 avoidanceDirection = FindAvoidanceDirection(directionToTarget);
        if (avoidanceDirection != Vector3.zero)
        {
            return avoidanceDirection.normalized;
        }

        // If no clear path found, try to move towards target anyway (will be handled by safety checks)
        return directionToTarget;
    }

    bool IsObstacleInDirection(Vector3 direction)
    {
        Ray ray = new Ray(transform.position + Vector3.up * 0.5f, direction);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, obstacleCheckDistance, buildingLayer))
        {
            if (IsBuilding(hit.collider.gameObject))
            {
                return true;
            }
        }

        return false;
    }

    Vector3 FindAvoidanceDirection(Vector3 toTarget)
    {
        // Calculate perpendicular directions for avoidance
        Vector3 rightDirection = Vector3.Cross(toTarget, Vector3.up).normalized;

        // Try waypoints to the right and left
        Vector3[] candidatePoints = {
            transform.position + rightDirection * avoidanceRange + toTarget.normalized * 2f,
            transform.position - rightDirection * avoidanceRange + toTarget.normalized * 2f,
            transform.position + toTarget.normalized * avoidanceRange
        };

        foreach (Vector3 candidate in candidatePoints)
        {
            if (!IsObstacleInDirection((candidate - transform.position).normalized))
            {
                return candidate;
            }
        }

        return Vector3.zero;
    }

    void CheckIfStuckAndHandle()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        if (distanceMoved < stuckThreshold)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckTimeLimit)
            {
                // NPC is stuck, try to teleport slightly or find alternative route
                HandleStuckSituation();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }

    void HandleStuckSituation()
    {
#if !PLAYABLE_AD
        Debug.Log($"NPC {gameObject.name} is stuck, attempting to resolve...");
#endif


        // Try to move the NPC to a nearby clear position
        Vector3[] escapeDirections = {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            (Vector3.right + Vector3.forward).normalized,
            (Vector3.right + Vector3.back).normalized,
            (Vector3.left + Vector3.forward).normalized,
            (Vector3.left + Vector3.back).normalized
        };

        foreach (Vector3 direction in escapeDirections)
        {
            Vector3 escapePosition = transform.position + direction * 2f;

            if (IsSafePosition(escapePosition) && !Physics.CheckSphere(escapePosition, 0.5f, buildingLayer))
            {
                transform.position = escapePosition;
                lastPosition = escapePosition;
#if !PLAYABLE_AD
                Debug.Log($"NPC {gameObject.name} escaped from stuck position");
#endif

                return;
            }
        }

        // If still can't escape, teleport closer to target (last resort)
        if (m_targetTransform != null)
        {
            Vector3 emergencyPosition = Vector3.Lerp(transform.position, m_targetTransform.position, 0.5f);
            if (IsSafePosition(emergencyPosition) && !Physics.CheckSphere(emergencyPosition, 0.5f, buildingLayer))
            {
                transform.position = emergencyPosition;
                lastPosition = emergencyPosition;
#if !PLAYABLE_AD
                Debug.Log($"NPC {gameObject.name} used emergency teleport");
#endif

                return;
            }
        }

        // Last resort: force target reach
#if !PLAYABLE_AD
        Debug.LogWarning($"NPC {gameObject.name} could not escape stuck position, forcing completion");
#endif

        ForceReachTarget();
    }

    bool IsBuilding(GameObject obj)
    {
        // Check if object is a building that should be avoided
        return obj.GetComponent<TurretController>() != null ||
               obj.GetComponent<WallController>() != null ||
               obj.GetComponent<EnhanceController>() != null ||
               obj.CompareTag("Building") ||
               obj.CompareTag("Obstacle");
    }

    void ReachTarget()
    {
        // Stop movement
        isMovingToTarget = false;
        //UpdateAnimation(false);

        // Check if target is a TurretController and enhance it
        if (m_targetTransform != null)
        {
            TurretController turret = m_targetTransform.GetComponent<TurretController>();
            if (turret != null && turret.IsBuilt() && !turret.IsUpgraded())
            {
                // Execute turret upgrade
                turret.UpgradeTurret();
#if !PLAYABLE_AD
                Debug.Log($"NPC {gameObject.name} reached turret {turret.name} and upgraded it to multi-shot!");
#endif
            }
            else if (turret != null)
            {
#if !PLAYABLE_AD
                Debug.Log($"NPC {gameObject.name} reached turret {turret.name} but it's either not built or already upgraded.");
#endif
            }
            else
            {
#if !PLAYABLE_AD
                Debug.LogWarning($"NPC {gameObject.name} reached target but it's not a valid turret");
#endif
            }
        }

        // Return to pool
        ObjectPool.Instance.ReturnToPool(this.gameObject);
    }

    /// <summary>
    /// Force reach target
    /// </summary>
    void ForceReachTarget()
    {
#if !PLAYABLE_AD
        Debug.Log($"NPC {gameObject.name} force-reached target due to timeout or stuck situation");
#endif

        ReachTarget();
    }
#if !PLAYABLE_AD
    // Public methods for external control
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = Mathf.Max(0.1f, newSpeed);
    }

    public void SetAvoidanceRange(float newRange)
    {
        avoidanceRange = Mathf.Max(0.5f, newRange);
    }

    public bool IsMoving()
    {
        return isMovingToTarget;
    }

    public Transform GetTarget()
    {
        return m_targetTransform;
    }

    /// <summary>
    /// Force stop (can be called externally)
    /// </summary>
    public void ForceStop()
    {
        isMovingToTarget = false;
        isTimeoutReached = true;
        StopAllCoroutines();
        ObjectPool.Instance.ReturnToPool(this.gameObject);
#if !PLAYABLE_AD
        Debug.Log($"NPC {gameObject.name} was force-stopped");
#endif

    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw avoidance range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, avoidanceRange);

        // Draw obstacle check distance
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * obstacleCheckDistance);

        // Draw path to target
        if (m_targetTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, m_targetTransform.position);

            // Draw current waypoint
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(currentWaypoint, 0.5f);
        }

        // Draw start position
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(startPosition, 1f);
    }
#endif
    void OnDestroy()
    {
        // Stop movement when object is destroyed
        isMovingToTarget = false;
    }

    void OnDisable()
    {
        // Stop movement when object is disabled
        isMovingToTarget = false;
    }
}