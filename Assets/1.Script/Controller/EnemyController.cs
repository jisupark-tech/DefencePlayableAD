using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour, IHealth
{
    [Header("Can Targetable")]
    public bool targetable = true;
    [Header("Enemy Settings")]
    public int health = 1;
    public float moveSpeed = 3f;
    public int goldValue = 1;
    public float waypointReachDistance = 1f;
    public Transform EffectPos;
    public Material m_DeadMaterial;
    public Material m_AlliveMaterial;
    public Material m_DeadCrownMaterial;
    public Material m_AlliveCrownMaterial;
    public SkinnedMeshRenderer m_skinMeshRender;
    public SkinnedMeshRenderer m_crownMeshRender;

    [Header("Building Attack Settings")]
    public float buildingDetectionRange = 3f;
    public int buildingDamage = 10;
    public float buildingAttackRate = 1f;
    public float enemySpacing = 1.2f;
    public float attackPositionRadius = 2.5f;

    [Header("WebGL Optimization")]
    public bool usePhysicsDetection = false;
    public float buildingCheckInterval = 0.1f;

    [Header("Enemy Avoidance Settings")]
    public float avoidanceStrength = 2.0f;
    public float avoidanceRange = 2.0f;

    [Header("Animation")]
    public Animator anim;

    [Header("Improved Targeting")]
    public float targetRefreshRate = 0.3f;
    public float stuckCheckTime = 2f;
    public float obstacleAvoidDistance = 1.5f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 360f; // 빠른 회전으로 수정
    public bool enableSmoothRotation = true; // 회전 활성화 토글

    private Transform[] path;
    private int currentWaypointIndex = 0;
    private int maxHealth;

    // 건물 공격 관련
    private IHealth targetBuilding;
    private Transform targetBuildingTransform;
    private bool isAttackingBuilding = false;
    private float nextAttackTime = 0f;

    // 개선된 AI 관련
    private Vector3 assignedAttackPosition;

    private bool hasAssignedPosition = false;

    // WebGL 최적화를 위한 캐시
    private List<Transform> cachedBuildings = new List<Transform>();
    private float lastBuildingCacheTime = 0f;
    private float buildingCacheInterval = 1f;

    private Transform targetPos;

    // 회피 시스템 최적화
    private Vector3 lastAvoidanceDirection = Vector3.zero;
    private float lastAvoidanceTime = 0f;
    private float avoidanceUpdateInterval = 0.1f;

    // 새로운 개선 시스템
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private float lastTargetCheckTime = 0f;
    private bool isStuck = false;
    private Vector3 randomDirection = Vector3.zero;
    private float randomMoveTime = 0f;

    void OnEnable()
    {
        maxHealth = health;
        health = maxHealth;
        currentWaypointIndex = 0;

        // 개선된 AI 초기화
        isAttackingBuilding = false;
        hasAssignedPosition = false;
        targetBuilding = null;
        targetBuildingTransform = null;


        // 회피 시스템 초기화
        lastAvoidanceDirection = Vector3.zero;
        lastAvoidanceTime = 0f;

        // 새로운 시스템 초기화
        lastPosition = transform.position;
        stuckTimer = 0f;
        lastTargetCheckTime = 0f;
        isStuck = false;
        randomDirection = Vector3.zero;
        randomMoveTime = 0f;

        // WebGL용 건물 캐시 초기화
        RefreshBuildingCache();
    }

    public void Initialize(Transform[] enemyPath)
    {
        if (m_skinMeshRender != null && m_AlliveMaterial!= null)
        {
            m_skinMeshRender.material = m_AlliveMaterial;
        }

        if (m_crownMeshRender != null && m_AlliveCrownMaterial != null)
        {
            m_crownMeshRender.material = m_AlliveCrownMaterial;
        }

        path = enemyPath;

        if (path != null && path.Length > 0)
        {
            transform.position = path[0].position;
            StartCoroutine(FollowPath());
        }
    }

    public void Initialize(Transform _targetPos)
    {
        if (m_skinMeshRender != null && m_AlliveMaterial != null)
        {
            m_skinMeshRender.material = m_AlliveMaterial;
        }

        if (m_crownMeshRender != null && m_AlliveCrownMaterial != null)
        {
            m_crownMeshRender.material = m_AlliveCrownMaterial;
        }

        path = new Transform[1];
        path[0] = _targetPos;
        if (path != null && path.Length > 0 && this.gameObject.activeInHierarchy)
        {
            StartCoroutine(TraceTarget());
        }
    }

    void RefreshBuildingCache()
    {
        cachedBuildings.Clear();

        if (GameManager.Instance != null)
        {
            // 벽들 먼저 추가 (최우선) - 간단한 체크만
            foreach (var wall in GameManager.Instance.m_walls)
            {
                if (wall != null && wall.gameObject.activeInHierarchy && wall.IsBuilt())
                {
                    cachedBuildings.Add(wall.transform);
                }
            }

            // 터렛들 추가 (중간 우선순위) - 간단한 체크만
            foreach (var turret in GameManager.Instance.m_Turrets)
            {
                if (turret != null && turret.gameObject.activeInHierarchy && turret.IsBuilt())
                {
                    cachedBuildings.Add(turret.transform);
                }
            }

            // 광산들 추가 (중간 우선순위) - 간단한 체크만
            foreach (var mine in GameManager.Instance.m_Mines)
            {
                if (mine != null && mine.gameObject.activeInHierarchy && mine.IsBuilt())
                {
                    cachedBuildings.Add(mine.transform);
                }
            }

            foreach(var enhance in GameManager.Instance.m_Enhances)
            {
                if(enhance != null && enhance.gameObject.activeInHierarchy && enhance.IsBuilt())
                {
                    cachedBuildings.Add(enhance.transform);
                }
            }

            // 메인 센터 추가 (낮은 우선순위)
            MainCenterController _center = GameManager.Instance.GetMainCenter();
            if (_center != null && _center.gameObject.activeInHierarchy)
            {
                cachedBuildings.Add(_center.transform);
            }

            //TODO 플레이어 삭제
            // 플레이어 추가 (최저 우선순위)
            PlayerController _player = GameManager.Instance.m_Player;
            if (_player != null && _player.gameObject.activeInHierarchy)
            {
                cachedBuildings.Add(_player.transform);
            }
        }
#if !PLAYABLE_AD
        Debug.Log($"Enemy cached {cachedBuildings.Count} buildings for detection");
#endif
    }

    // 간단한 건물 유효성 체크 (너무 엄격하지 않게)
    bool IsValidTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        // IHealth 체크만 (간단하게)
        IHealth health = target.GetComponent<IHealth>();
        if (health != null && health.IsDead() && health.CanTargetable())
            return false;

        return true;
    }

    IEnumerator FollowPath()
    {
        while (currentWaypointIndex < path.Length && gameObject.activeInHierarchy)
        {
            Transform targetWaypoint = path[currentWaypointIndex];

            while (Vector3.Distance(transform.position, targetWaypoint.position) > waypointReachDistance && gameObject.activeInHierarchy)
            {
                // 개선된 건물 체크
                CheckForBuildingsImproved();

                if (isAttackingBuilding)
                {
                    yield return StartCoroutine(AttackBuilding());
                }
                else
                {
                    // 기본 이동 (항상 실행)
                    MoveTowardsTargetDirect(targetWaypoint.position);
                }

                yield return null;
            }

            currentWaypointIndex++;
        }

        ReachEnd();
    }

    IEnumerator TraceTarget()
    {
        while (gameObject.activeInHierarchy)
        {
            // 개선된 건물 체크
            CheckForBuildingsImproved();

            if (anim != null)
            {
                //anim.SetBool("IsDead", false);
                anim.SetBool("IsAttacking", isAttackingBuilding);
            }

            if (isAttackingBuilding)
            {
                yield return StartCoroutine(AttackBuilding());
            }
            else
            {
                // 기본 이동 (항상 실행)
                MoveTowardsTargetDirect(path[0].position);
            }

            yield return null;
        }

        ReachEnd();
    }

    // 직접적인 이동 메서드 (코루틴이 아님)
    void MoveTowardsTargetDirect(Vector3 targetPosition)
    {
        // 막힘 상태 체크
        CheckIfStuck();

        Vector3 moveDirection;

        if (isStuck && randomMoveTime > 0f)
        {
            // 막힌 상태에서 랜덤 방향으로 이동
            moveDirection = randomDirection.normalized;
            randomMoveTime -= Time.deltaTime;

            if (randomMoveTime <= 0f)
            {
                isStuck = false;
            }
        }
        else
        {
            // 일반 이동
            moveDirection = (targetPosition - transform.position).normalized;

            // 장애물 회피 체크
            Vector3 obstacleAvoidance = CheckForObstacles(moveDirection);
            if (obstacleAvoidance != Vector3.zero)
            {
                moveDirection = (moveDirection + obstacleAvoidance).normalized;
            }
        }

        // 다른 적 회피
        Vector3 avoidanceDirection = CalculateAvoidanceDirection();
        Vector3 finalDirection = (moveDirection + avoidanceDirection * 0.5f).normalized;

        // 실제 이동
        Vector3 nextPosition = transform.position + finalDirection * moveSpeed * Time.deltaTime;

        if (GameManager.Instance != null)
        {
            Vector3 validPosition = GameManager.Instance.GetValidMovePosition(transform.position, nextPosition, transform, false);
            transform.position = validPosition;
        }
        else
        {
            transform.position = nextPosition;
        }

        // 회전 (간단하게)
        if (!isStuck && avoidanceDirection.magnitude < 0.3f && !isAttackingBuilding)
        {
            if (enableSmoothRotation)
            {
                SmoothRotateTowards(moveDirection);
            }
            else
            {
                // 즉시 회전
                if (moveDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(moveDirection);
                }
            }
        }
    }

    // 간단한 부드러운 회전
    void SmoothRotateTowards(Vector3 direction)
    {
        if (direction == Vector3.zero) return;

        direction.y = 0; // Y축 제한
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    // 막힘 상태 체크
    void CheckIfStuck()
    {
        if (Vector3.Distance(transform.position, lastPosition) < 0.1f)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= stuckCheckTime && !isStuck)
            {
                isStuck = true;
                randomDirection = GetRandomDirection();
                randomMoveTime = 1.5f;
#if !PLAYABLE_AD
                Debug.Log($"Enemy {gameObject.name} is stuck, moving randomly");
#endif
            }
        }
        else
        {
            stuckTimer = 0f;
            lastPosition = transform.position;
        }
    }

    // 랜덤 방향 생성
    Vector3 GetRandomDirection()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    // 장애물 체크 및 회피
    Vector3 CheckForObstacles(Vector3 moveDirection)
    {
        Vector3 avoidance = Vector3.zero;
        Vector3 checkPosition = transform.position + moveDirection * obstacleAvoidDistance;

        foreach (var building in cachedBuildings)
        {
            if (building == null) continue;

            float distanceToBuilding = Vector3.Distance(checkPosition, building.position);

            if (distanceToBuilding < obstacleAvoidDistance)
            {
                WallController _wall = building.GetComponent<WallController>();
                if (_wall != null)
                {
                    if (_wall.ClampToBoundary(checkPosition, _wall.IsHasGate(), false))
                    {
                        break;
                    }
                }
                else
                {
                    Vector3 toBuilding = (building.position - transform.position).normalized;
                    Vector3 rightDirection = Vector3.Cross(toBuilding, Vector3.up);

                    Vector3 leftAvoid = transform.position - rightDirection * obstacleAvoidDistance;
                    Vector3 rightAvoid = transform.position + rightDirection * obstacleAvoidDistance;

                    if (path != null && path.Length > 0)
                    {
                        float leftDistance = Vector3.Distance(leftAvoid, path[0].position);
                        float rightDistance = Vector3.Distance(rightAvoid, path[0].position);

                        avoidance = leftDistance < rightDistance ? -rightDirection : rightDirection;
                    }
                    else
                    {
                        avoidance = rightDirection;
                    }

                    break;                   
                }
            
            }

            //if (distanceToBuilding < obstacleAvoidDistance)
            //{
            //    Vector3 toBuilding = (building.position - transform.position).normalized;
            //    Vector3 rightDirection = Vector3.Cross(toBuilding, Vector3.up);

            //    Vector3 leftAvoid = transform.position - rightDirection * obstacleAvoidDistance;
            //    Vector3 rightAvoid = transform.position + rightDirection * obstacleAvoidDistance;

            //    if (path != null && path.Length > 0)
            //    {
            //        float leftDistance = Vector3.Distance(leftAvoid, path[0].position);
            //        float rightDistance = Vector3.Distance(rightAvoid, path[0].position);

            //        avoidance = leftDistance < rightDistance ? -rightDirection : rightDirection;
            //    }
            //    else
            //    {
            //        avoidance = rightDirection;
            //    }

            //    break;
            //}
        }

        return avoidance;
    }

    // 개선된 건물 탐지
    void CheckForBuildingsImproved()
    {
        // 타겟 갱신 주기 체크
        if (Time.time - lastTargetCheckTime < targetRefreshRate)
        {
            return;
        }

        lastTargetCheckTime = Time.time;

        // 건물 캐시 갱신
        if (Time.time - lastBuildingCacheTime > buildingCacheInterval)
        {
            RefreshBuildingCache();
            lastBuildingCacheTime = Time.time;
        }

        if (cachedBuildings.Count == 0) return;

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (var building in cachedBuildings)
        {
            if (!IsValidTarget(building)) continue;

            float distance = Vector3.Distance(transform.position, building.position);

            if (distance <= buildingDetectionRange)
            {
                // 우선순위 기반 점수 계산
                float priorityScore = GetTargetPriorityScore(building, distance);

                if (priorityScore < bestScore)
                {
                    bestScore = priorityScore;
                    bestTarget = building;
                }
            }
        }

        // 새로운 타겟 설정
        if (bestTarget != null && bestTarget != targetBuildingTransform)
        {
            SetNewTarget(bestTarget);
        }
        else if (bestTarget == null && isAttackingBuilding)
        {
            // 공격할 대상이 없으면 공격 중단
            EndBuildingAttack();
        }
    }

    // 타겟 우선순위 점수 계산
    float GetTargetPriorityScore(Transform target, float distance)
    {
        float priorityMultiplier = 1f;

        // 건물 타입별 우선순위 (낮을수록 우선순위 높음)
        if (target.GetComponent<WallController>() != null)
        {
            priorityMultiplier = 1f; // 벽 최우선
        }
        else if (target.GetComponent<TurretController>() != null)
        {
            priorityMultiplier = 2f; // 터렛 두 번째
        }
        else if (target.GetComponent<MineController>() != null)
        {
            priorityMultiplier = 2.5f; // 광산 세 번째
        }
        else if (target.GetComponent<MainCenterController>() != null)
        {
            priorityMultiplier = 3f; // 메인센터 네 번째
        }
        else if (target.GetComponent<PlayerController>() != null)
        {
            priorityMultiplier = 4f; // 플레이어 최후
        }

        return distance * priorityMultiplier;
    }

    // 새로운 타겟 설정
    void SetNewTarget(Transform newTarget)
    {
        targetBuildingTransform = newTarget;
        targetBuilding = newTarget.GetComponent<IHealth>();

        if (targetBuilding == null || (!targetBuilding.IsDead()&& targetBuilding.CanTargetable()))
        {
            isAttackingBuilding = true;
            hasAssignedPosition = false;
#if !PLAYABLE_AD
            Debug.Log($"Enemy {gameObject.name} targeting {newTarget.name} at distance {Vector3.Distance(transform.position, newTarget.position):F1}");
#endif
        }
    }

    Vector3 CalculateAvoidanceDirection()
    {
        if (Time.time - lastAvoidanceTime < avoidanceUpdateInterval)
        {
            return lastAvoidanceDirection;
        }

        lastAvoidanceTime = Time.time;
        Vector3 avoidance = Vector3.zero;
        int neighborCount = 0;

        EnemyController[] nearbyEnemies = FindObjectsOfType<EnemyController>();

        foreach (var enemy in nearbyEnemies)
        {
            if (enemy == this || enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);

            if (distance < avoidanceRange && distance > 0.1f)
            {
                Vector3 avoidDirection = (transform.position - enemy.transform.position).normalized;
                float avoidWeight = (avoidanceRange - distance) / avoidanceRange;
                avoidance += avoidDirection * avoidWeight;
                neighborCount++;
            }
        }

        if (neighborCount > 0)
        {
            avoidance = avoidance.normalized * avoidanceStrength;
        }

        lastAvoidanceDirection = avoidance;
        return avoidance;
    }
#if !PLAYABLE_AD
    // 기존 메서드들 유지
    void CheckForBuildingsWebGL()
    {
        CheckForBuildingsImproved(); // 개선된 버전으로 대체
    }
#endif
    IEnumerator AttackBuilding()
    {
        if (targetBuildingTransform == null || (targetBuilding != null && targetBuilding.IsDead()))
        {
            EndBuildingAttack();
            yield break;
        }

        if (!hasAssignedPosition)
        {
            Vector3 directionToTarget = (transform.position - targetBuildingTransform.position).normalized;
            assignedAttackPosition = transform.position + directionToTarget * 0.3f;//CalculateAttackPosition(targetBuildingTransform);

            hasAssignedPosition = true;
        }

        yield return StartCoroutine(MoveToAttackPosition());

        while (isAttackingBuilding && targetBuildingTransform != null && (targetBuilding == null || !targetBuilding.IsDead()))
        {
            // 간단한 유효성 체크
            if (!IsValidTarget(targetBuildingTransform))
            {
                EndBuildingAttack();
                yield break;
            }

            float distanceToTarget = Vector3.Distance(transform.position, targetBuildingTransform.position);

            if (distanceToTarget > buildingDetectionRange)
            {
                EndBuildingAttack();
                yield break;
            }

            if (distanceToTarget <= buildingDetectionRange)
            {
                Vector3 directionToBuilding = (targetBuildingTransform.position - transform.position).normalized;

                // 공격할 때 회전
                if (enableSmoothRotation)
                {
                    SmoothRotateTowards(directionToBuilding);
                }
                else
                {
                    transform.rotation = Quaternion.LookRotation(directionToBuilding);
                }

                if (Time.time >= nextAttackTime)
                {
                    AttackTargetBuilding();
                    nextAttackTime = Time.time + buildingAttackRate;
                }
            }

            yield return null;
        }

        EndBuildingAttack();
    }

    Vector3 CalculateAttackPosition(Transform targetBuilding)
    {
        if (targetBuilding == null) return transform.position;

        Vector3 directionToTarget = (transform.position - targetBuilding.position).normalized;
        Vector3 attackPos = targetBuilding.position + directionToTarget * (attackPositionRadius * 0.8f);

        // 다른 적들과의 겹침 방지
        int attempts = 0;
        while (attempts < 8)
        {
            bool positionClear = true;
            EnemyController[] nearbyEnemies = FindObjectsOfType<EnemyController>();

            foreach (var enemy in nearbyEnemies)
            {
                if (enemy == this || !enemy.isAttackingBuilding) continue;

                float distance = Vector3.Distance(attackPos, enemy.assignedAttackPosition);
                if (distance < enemySpacing)
                {
                    positionClear = false;
                    break;
                }
            }

            if (positionClear) break;

            float angle = attempts * 45f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * enemySpacing;
            attackPos = targetBuilding.position + directionToTarget * attackPositionRadius + offset;
            attempts++;
        }

        return attackPos;
    }

    IEnumerator MoveToAttackPosition()
    {
        float arrivalDistance = 0.5f;

        //TODO거리 값은 맞지만 Wall의 경우 범위에 포함된 거리여야 됨
        //현재 같은경우 Wall의 중심지에서 일정한 범위에서 원으로 둘러쌈
        if(targetBuildingTransform!=null)
        {
            WallController _wall = targetBuildingTransform.gameObject.GetComponent<WallController>();

            if (_wall!=null)
            {
                while (/*Vector3.Distance(transform.position, assignedAttackPosition) > arrivalDistance*/_wall.ClampToBoundary(assignedAttackPosition, _wall.IsHasGate(),false) && isAttackingBuilding)
                {
                    Vector3 moveDirection = (assignedAttackPosition - transform.position).normalized;
                    Vector3 avoidanceDirection = CalculateAvoidanceDirection();
                    Vector3 finalDirection = (moveDirection + avoidanceDirection).normalized;

                    Vector3 targetPosition = transform.position + finalDirection * moveSpeed * Time.deltaTime;
                    transform.position = targetPosition;

                    // 공격 위치로 이동할 때 회전
                    if (enableSmoothRotation)
                    {
                        SmoothRotateTowards(moveDirection);
                    }

                    yield return null;
                }
            }
        }   
#if !PLAYABLE_AD
        Debug.Log($"Enemy {gameObject.name} reached attack position");
#endif
    }

    void EndBuildingAttack()
    {
#if !PLAYABLE_AD
        Debug.Log($"Enemy {gameObject.name} ended building attack");
#endif

        isAttackingBuilding = false;
        targetBuilding = null;
        targetBuildingTransform = null;
        hasAssignedPosition = false;


        ResumePath();
    }

    void ResumePath()
    {
        if (path == null || path.Length == 0) return;

        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = currentWaypointIndex; i < path.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, path[i].position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        currentWaypointIndex = Mathf.Max(currentWaypointIndex, nearestIndex);
#if !PLAYABLE_AD
        Debug.Log($"Enemy {gameObject.name} resumed path at waypoint {currentWaypointIndex}");
#endif

    }

    void AttackTargetBuilding()
    {
        if (targetBuilding != null && !targetBuilding.IsDead() && targetBuilding.CanTargetable())
        {
            targetBuilding.TakeDamage(buildingDamage);
#if !PLAYABLE_AD
            Debug.Log($"Enemy {gameObject.name} attacked building for {buildingDamage} damage!");
#endif
        }
    }

    void ReachEnd()
    {
        GameManager.Instance.ReSearchTarget(this);
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, BulletOwner.Player, null);
    }

    public void TakeDamage(int damage, BulletOwner killer, TurretController killerTurret = null)
    {
        health -= damage;

        EffectController _effect = ObjectPool.Instance.SpawnFromPool("HittedEffect", EffectPos.position , Quaternion.identity, ObjectPool.Instance.transform).GetComponent<EffectController>();
        if (_effect)
            _effect.Init(EffectType.Hit);

        if (health <= 0)
        {
            Die(killer, killerTurret);
        }
    }

    void Die(BulletOwner killer, TurretController killerTurret = null)
    {
        StopAllCoroutines();
        StartCoroutine(ShowDeadAnim(killer, killerTurret));
    }

    void DropGoldAtLocation()
    {
        float _offsetSize = Random.Range(0, 1.1f);
        Vector3 _offset = new Vector3(_offsetSize, 0, _offsetSize);
        GameObject gold = ObjectPool.Instance.SpawnFromPool("Gold", transform.position + _offset, Quaternion.identity);
        AudioManager.Instance.PlayGoldSpawnSound();
        if (gold != null)
        {
            GoldPickup goldPickup = gold.GetComponent<GoldPickup>();
            goldPickup.Initialize(1);
        }
    }

    IEnumerator ShowDeadAnim(BulletOwner killer, TurretController killerTurret = null)
    {
        if (m_skinMeshRender != null && m_DeadMaterial != null)
            m_skinMeshRender.material = m_DeadMaterial;

        if (m_crownMeshRender != null && m_DeadCrownMaterial != null)
            m_crownMeshRender.material = m_DeadCrownMaterial;

        if (anim != null)
        {
            anim.SetTrigger("Dead");
        }

        for (int i = 0; i < goldValue; i++)
        {
            if (killer == BulletOwner.Player)
            {
                DropGoldAtLocation();
            }
            else if (killer == BulletOwner.Turret && killerTurret != null)
            {
                GoldStorage turretStorage = killerTurret.GetgoldStorage();
                if (turretStorage != null)
                    turretStorage.AddGold(1);
                else
                    DropGoldAtLocation();
            }
        }
        yield return new WaitForSeconds(1f);

        gameObject.SetActive(false);


        GameManager.Instance.OnEnemyKilled();
        ObjectPool.Instance.ReturnToPool(gameObject);

    }
#region IHealth Implementation
    public int GetCurrentHealth()
    {
        return health;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }

    public void Heal(int amount)
    {
        health += amount;
        if (health > maxHealth) health = maxHealth;
        OnHealthChanged(health, maxHealth);
    }

    public bool IsDead()
    {
        return health <= 0;
    }

    public float GetHealthRatio()
    {
        return maxHealth > 0 ? (float)health / maxHealth : 0f;
    }

    public void OnHealthChanged(int currentHealth, int maxHealth)
    {
        // HP바 UI 업데이트 등 (필요시 구현)
    }

    public void OnDeath()
    {
        Die(BulletOwner.Player, null);
    }
    public bool CanTargetable()
    {
        return targetable;
    }
#endregion

    public void IncreaseHealth(int newMaxHealth)
    {
        if (newMaxHealth > health)
        {
            float healthRatio = (float)health / maxHealth;
            maxHealth = newMaxHealth;
            health = Mathf.RoundToInt(maxHealth * healthRatio);
#if !PLAYABLE_AD
            Debug.Log($"{gameObject.name} health increased: {health}/{maxHealth}");
#endif
        }
    }

    public void IncreaseSpeed(float newSpeed)
    {
        if (newSpeed > moveSpeed)
        {
            moveSpeed = newSpeed;
#if !PLAYABLE_AD
            Debug.Log($"{gameObject.name} speed increased to: {moveSpeed:F1}");
#endif
        }
    }

    public void SetStatsForPhase(int phaseHealth, float phaseSpeed)
    {
        maxHealth = phaseHealth;
        health = phaseHealth;
        moveSpeed = phaseSpeed;
#if !PLAYABLE_AD
        Debug.Log($"{gameObject.name} spawned with Phase stats - Health: {health}, Speed: {moveSpeed:F1}");
#endif
    }
}