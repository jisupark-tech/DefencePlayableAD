using System.Collections;
using UnityEngine;

public class CombatComponent : MonoBehaviour, ICombat
{
    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private bool autoTarget = true;
    [SerializeField] private float targetUpdateRate = 0.2f;
    [SerializeField] private bool turnOnRotation = false;

    [Header("WebGL Optimization")]
    [SerializeField] private bool usePhysicsDetection = false; // WebGL에서는 false로 설정
    [SerializeField] private float maxTargetSearchDistance = 12f; // 최대 타겟 검색 거리

    [Header("Safety Settings")]
    [SerializeField] private float maxCombatTime = 300f; // 최대 전투 시간 (5분) - 무한루프 방지
    [SerializeField] private int maxAttacksPerSecond = 10; // 초당 최대 공격 횟수 제한

    public WeaponComponent weaponComponent; // Public으로 변경 (TurretController에서 접근용)
    private IWeapon currentWeapon;
    private Transform currentTarget;
    private bool isInitialized = false;

    // 자체적인 쿨다운 관리
    private float lastAttackTime = 0f;
    private float attackCooldown = 1f;

    // 안전성 추가 변수들
    private float combatStartTime = 0f;
    private int attacksThisSecond = 0;
    private float secondStartTime = 0f;
    private bool isCombatActive = true;

    void Awake()
    {
        weaponComponent = GetComponent<WeaponComponent>();
        if (weaponComponent == null)
        {
            weaponComponent = gameObject.AddComponent<WeaponComponent>();
        }
        SetWeapon(weaponComponent);
    }

    public void Initialize()
    {
        if (isInitialized) return;

        combatStartTime = Time.time;
        secondStartTime = Time.time;
        isCombatActive = true;

        if (autoTarget)
        {
            StartCoroutine(AutoTargeting());
        }

        // 쿨다운 기반 공격 코루틴 (업그레이드되지 않은 터렛용)
        StartCoroutine(AttackCooldownCoroutine());

        isInitialized = true;
    }

    // 쿨다운 기반 공격 시스템 - 안전성 강화
    IEnumerator AttackCooldownCoroutine()
    {
        while (isCombatActive && gameObject.activeInHierarchy)
        {
            // 최대 전투 시간 체크 (무한 루프 방지)
            if (Time.time - combatStartTime > maxCombatTime)
            {
#if !PLAYABLE_AD
                Debug.LogWarning($"Combat time limit reached for {gameObject.name}. Stopping combat.");
#endif
                break;
            }

            // 업그레이드된 터렛인지 확인 (중복 공격 방지)
            TurretController turret = GetComponent<TurretController>();
            if (turret != null && turret.IsUpgraded())
            {
                // 업그레이드된 터렛은 자체 전투 시스템 사용하므로 일반 공격 중지
                yield return new WaitForSeconds(1f); // 1초 대기 후 다시 체크
                continue;
            }

            if (currentTarget != null && IsValidTarget(currentTarget) && CanAttack())
            {
                // 초당 공격 횟수 제한 체크
                if (CheckAttackRateLimit())
                {
                    Attack(currentTarget);
                    lastAttackTime = Time.time; // 공격 시간 업데이트

                    // 공격 후 쿨다운만큼 대기
                    yield return new WaitForSeconds(attackCooldown);
                }
                else
                {
                    // 공격 제한에 걸린 경우 잠시 대기
                    yield return new WaitForSeconds(0.1f);
                }
            }
            else
            {
                // 타겟이 없으면 짧게 대기 후 다시 체크
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    /// <summary>
    /// 초당 공격 횟수 제한 체크 (무한 공격 방지)
    /// </summary>
    bool CheckAttackRateLimit()
    {
        float currentTime = Time.time;

        // 새로운 1초가 시작되면 카운터 리셋
        if (currentTime - secondStartTime >= 1f)
        {
            secondStartTime = currentTime;
            attacksThisSecond = 0;
        }

        // 이번 초에 너무 많이 공격했는지 체크
        if (attacksThisSecond >= maxAttacksPerSecond)
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"Attack rate limit reached for {gameObject.name}. Attacks this second: {attacksThisSecond}");
#endif
            return false;
        }

        attacksThisSecond++;
        return true;
    }

    bool IsValidTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > attackRange)
            return false;

        // 안전한 IHealth 체크
        IHealth health = target.GetComponent<IHealth>();
        if (health != null && health.IsDead())
            return false;

        return true;
    }

    public void Attack(Transform target)
    {
        if (currentWeapon == null || target == null) return;

        Vector3 firePosition = weaponComponent.GetFirePoint().position;

        // 타겟을 향해 회전
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            if (turnOnRotation)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        // 무기 발사 (쿨다운 체크 없이 강제 발사)
        BulletOwner owner = GetBulletOwner();
        ForceFireWeapon(firePosition, target, owner);
    }

    // 쿨다운 무시하고 강제 발사 - 안전성 강화
    void ForceFireWeapon(Vector3 startPosition, Transform target, BulletOwner owner)
    {
        if (weaponComponent == null) return;

        string bulletType = weaponComponent.GetBulletType();
        GameObject bullet = ObjectPool.Instance.SpawnFromPool(bulletType, startPosition, weaponComponent.GetFirePoint().rotation);

        if (bullet != null)
        {
            BulletController bulletController = bullet.GetComponent<BulletController>();
            if (bulletController != null)
            {
                if (owner == BulletOwner.Turret && GetComponent<TurretController>() != null)
                {
                    bulletController.SetTarget(target, owner, GetComponent<TurretController>());
                }
                else
                {
                    bulletController.SetTarget(target, owner);
                }
                AudioManager.Instance.PlayArrowAttackSound();
            }
        }
        else
        {
#if !PLAYABLE_AD
            // 오브젝트 풀에서 총알을 가져오지 못한 경우 디버그
            Debug.LogWarning($"Failed to spawn bullet: {bulletType} from ObjectPool. Pool may be exhausted.");
#endif
        }
    }

    public void SetWeapon(IWeapon weapon)
    {
        currentWeapon = weapon;
        if (weapon != null)
        {
            attackRange = weapon.GetRange();
            attackCooldown = weapon.GetFireRate(); // 쿨다운 시간 설정
        }
    }

    public void SetTarget(Transform target)
    {
        currentTarget = target;
    }

    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    public bool CanAttack()
    {
        return currentTarget != null && Time.time >= lastAttackTime + attackCooldown;
    }

    public float GetAttackRange()
    {
        return attackRange;
    }

    public void SetAttackRange(float range)
    {
        attackRange = Mathf.Max(0f, range);
    }

    /// <summary>
    /// 전투 시스템 중지 (안전성)
    /// </summary>
    public void StopCombat()
    {
        isCombatActive = false;
        StopAllCoroutines(); // 모든 코루틴 중지
#if !PLAYABLE_AD
        Debug.Log($"Combat stopped for {gameObject.name}");
#endif

    }

    IEnumerator AutoTargeting()
    {
        while (isCombatActive && gameObject.activeInHierarchy)
        {
            // 최대 전투 시간 체크
            if (Time.time - combatStartTime > maxCombatTime)
            {
#if !PLAYABLE_AD
                Debug.LogWarning($"Auto targeting time limit reached for {gameObject.name}");
#endif
                break;
            }

            if (autoTarget)
            {
                FindNearestTargetWebGL();
            }

            yield return new WaitForSeconds(targetUpdateRate);
        }
    }

    /// <summary>
    /// WebGL 최적화된 타겟 찾기 (물리체 없이 거리 기반)
    /// </summary>
    void FindNearestTargetWebGL()
    {
#if !PLAYABLE_AD
        if (usePhysicsDetection)
        {
            // 레거시 물리 기반 방식 (WebGL에서는 사용하지 않음)
            FindNearestTargetPhysics();
            return;
        }
#endif
        // WebGL 최적화: 거리 계산 기반
        Transform nearestTarget = null;
        float nearestDistance = float.MaxValue;

        // 모든 적들을 거리 기반으로 검사 (성능 제한 추가)
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();

        int checkedCount = 0;
        const int maxCheckPerFrame = 20; // 프레임당 최대 20개만 체크 (성능 보호)

        foreach (EnemyController enemy in allEnemies)
        {
            if (checkedCount >= maxCheckPerFrame) break; // 성능 제한

            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
            checkedCount++;

            // 최대 검색 거리로 먼저 필터링 (성능 최적화)
            float roughDistance = Vector3.Distance(transform.position, enemy.transform.position);
            if (roughDistance > maxTargetSearchDistance) continue;

            // 사거리 내에 있는지 정확히 체크
            if (roughDistance <= attackRange)
            {
                // IHealth 체크 (안전하게)
                IHealth health = enemy.GetComponent<IHealth>();
                if (health == null || !health.IsDead())
                {
                    if (roughDistance < nearestDistance)
                    {
                        nearestDistance = roughDistance;
                        nearestTarget = enemy.transform;
                    }
                }
            }
        }

        // 이전 타겟과 다른 경우에만 로그 출력
        if (currentTarget != nearestTarget)
        {
            currentTarget = nearestTarget;

            if (currentTarget != null)
            {
#if !PLAYABLE_AD
                Debug.Log($"{gameObject.name} found new target: {currentTarget.name} (Distance: {nearestDistance:F1})");
#endif
            }
        }
    }
#if !PLAYABLE_AD
    /// <summary>
    /// 레거시 물리 기반 타겟 찾기 (WebGL에서는 사용하지 않음)
    /// </summary>
    void FindNearestTargetPhysics()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, attackRange, targetLayer);

        if (targets.Length == 0)
        {
            currentTarget = null;
            return;
        }

        Transform nearestTarget = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider target in targets)
        {
            // 기본 유효성 체크
            if (!target.gameObject.activeInHierarchy) continue;

            // 안전한 IHealth 컴포넌트 체크
            IHealth health = target.GetComponent<IHealth>();
            if (health != null && health.IsDead())
            {
                continue; // 죽은 적은 타겟에서 제외
            }

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = target.transform;
            }
        }

        currentTarget = nearestTarget;
    }
#endif
    private BulletOwner GetBulletOwner()
    {
        if (GetComponent<PlayerController>() != null)
            return BulletOwner.Player;
        else if (GetComponent<TurretController>() != null)
            return BulletOwner.Turret;
        else
            return BulletOwner.Player;
    }
#if !PLAYABLE_AD
    public WeaponComponent GetWeaponComponent() => weaponComponent;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // WebGL 모드에서 최대 검색 거리 표시
        if (!usePhysicsDetection)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, maxTargetSearchDistance);
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
#endif
    void OnDestroy()
    {
        // 오브젝트가 파괴될 때 전투 중지
        isCombatActive = false;
    }

    void OnDisable()
    {
        // 오브젝트가 비활성화될 때 전투 중지
        isCombatActive = false;
    }
}