using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour,IHealth
{
    [Header("Can Targetable")]
    public bool targetable = true;
    [Header("Gold Bag")]
    public Transform GoldBag;
    public Stack<GoldPickup> m_golds = new Stack<GoldPickup>();

    [Header("Range Indicator")]
    public bool showRangeIndicator = true;
    public int triangleCount = 10;
    public float indicatorRotationSpeed = 90f;

    [Header("Interaction Settings")]
    public float interactionRange = 2f;
    public LayerMask turretLayer = 1 << 9;
    [Header("WebGL Optimization")]
    public bool usePhysicsDetection = false; // WebGL에서는 false로 설정
    public float turretCheckInterval = 0.2f; // 터렛 체크 간격

    [Header("Camera")]
    public Camera mainCam;
    public Vector3 offset;
    public float cameraFollowSpeed = 5f; // 카메라 추적 속도

    [Header("Movement Boundary")]
    public bool enableBoundary = true; // 경계 제한 활성화
    public Vector2 boundaryMin = new Vector2(-15f, -15f); // 최소 경계
    public Vector2 boundaryMax = new Vector2(15f, 15f);   // 최대 경계
    public bool showBoundaryGizmos = true; // 씬 뷰에서 경계 표시

    [Header("Animation")]
    public Animator animator; // 애니메이터 컴포넌트 참조
    public bool enableAnimation = true; // 애니메이션 활성화 여부
    [Range(0f, 2f)]
    public float animationSpeedMultiplier = 1f; // 애니메이션 속도 배율

    // 애니메이션 파라미터 이름들 (Inspector에서 수정 가능)
    [Header("Animation Parameters")]
    public string moveParameterName = "Move"; // BlendTree용 float 파라미터
    public string attackTriggerName = "Attack"; // 공격 트리거 파라미터

    [Header("Movement Control")]
    public bool enableInstantStop = true; // 즉시 정지 기능 활성화
    public float stopDeceleration = 20f; // 정지 시 감속도 (높을수록 빨리 멈춤)

    // 컴포넌트 참조
    private MovementComponent movementComponent;
    private CombatComponent combatComponent;
    private List<GameObject> rangeTriangles = new List<GameObject>();
    private float currentRotationAngle = 0f;
    private VirtualPad VirtualPad;
    private bool m_init = false;

    // 애니메이션 관련 변수들
    private float currentMoveValue = 0f;
    private bool wasAttacking = false;

    // 이동 제어 관련 변수들
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 lastInputDirection = Vector3.zero;
    private bool wasMovingLastFrame = false;

    // WebGL 최적화를 위한 터렛 캐싱
    private List<TurretController> cachedTurrets = new List<TurretController>();
    private float lastTurretCacheTime = 0f;
    private float turretCacheInterval = 1f; // 1초마다 터렛 목록 갱신

    public void Init()
    {
        // Movement Component 가져오기
        movementComponent = GetComponent<MovementComponent>();
        if (movementComponent == null)
        {
            movementComponent = gameObject.AddComponent<MovementComponent>();
        }

        // Combat Component 가져오기
        combatComponent = GetComponent<CombatComponent>();
        if (combatComponent == null)
        {
            combatComponent = gameObject.AddComponent<CombatComponent>();
        }
        combatComponent.Initialize();

        // Animator 자동 할당 (Inspector에서 설정하지 않은 경우)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        // 애니메이터 유효성 검사
        ValidateAnimator();

        VirtualPad = GameManager.Instance.GetVirtualPad();
        m_golds.Clear();

        InitializeRangeIndicator();

        // 플레이어 초기 위치를 경계 내로 제한
        if (enableBoundary)
        {
            ClampToBoundary();
        }

        // WebGL용 터렛 캐시 초기화
        RefreshTurretCache();

        StartCoroutine(RangeIndicatorRotation());
        StartCoroutine(CheckTurretInteraction());

        m_init = true;
    }

    void Update()
    {
        if (!m_init)
            return;

        HandleMovement();
        HandleRotation();
        HandleCam(); // 카메라 추적 활성화
        HandleAnimation(); // 애니메이션 처리 추가
    }

    void HandleMovement()
    {
        Vector2 inputVector = VirtualPad.GetInputDirection();
       
        float angle = /*-45f * */Mathf.Deg2Rad;
        float rotatedX = inputVector.x * Mathf.Cos(angle) - inputVector.y * Mathf.Sin(angle);
        float rotatedY = inputVector.x * Mathf.Sin(angle) + inputVector.y * Mathf.Cos(angle);

        Vector3 moveDirection = new Vector3(rotatedX ,0, rotatedY);

        // 핵심 수정: VirtualPad의 즉시 정지 기능과 연동
        bool hasInput = moveDirection.magnitude > 0.01f;
        bool padActive = VirtualPad.IsPressed() || VirtualPad.IsActive();

        if (hasInput && padActive)
        {
            // 입력이 있을 때: 일반적인 이동
            movementComponent.Move(moveDirection);
            lastInputDirection = moveDirection;
            wasMovingLastFrame = true;

            // 이동 후 경계 체크
            if (enableBoundary)
            {
                ClampToBoundary();
            }
        }
        else
        {
            // 🔥 입력이 없거나 패드가 비활성화되었을 때: 즉시 정지 또는 감속 정지
            HandleMovementStop();
        }
    }

    /// <summary>
    /// 이동 정지 처리 (즉시 정지 또는 감속 정지)
    /// </summary>
    void HandleMovementStop()
    {
        if (enableInstantStop)
        {
            // 즉시 정지 모드
            movementComponent.Move(Vector3.zero);
            currentVelocity = Vector3.zero;
            lastInputDirection = Vector3.zero;
        }
        else
        {
            // 감속 정지 모드 (부드러운 정지)
            if (wasMovingLastFrame)
            {
                // 현재 속도를 점진적으로 감소
                currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, stopDeceleration * Time.deltaTime);

                // 속도가 충분히 작아지면 완전 정지
                if (currentVelocity.magnitude < 0.01f)
                {
                    currentVelocity = Vector3.zero;
                    lastInputDirection = Vector3.zero;
                }

                // 감속된 속도로 이동
                movementComponent.Move(currentVelocity.normalized);
            }
            else
            {
                movementComponent.Move(Vector3.zero);
            }
        }

        wasMovingLastFrame = false;
    }

    void HandleRotation()
    {
        Transform currentTarget = combatComponent.GetCurrentTarget();
        if (currentTarget != null)
        {
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }
    }

    void HandleCam()
    {
        if (mainCam != null)
        {
            // 목표 카메라 위치 계산
            Vector3 targetPosition = transform.position + offset;

            // 부드러운 카메라 추적
            mainCam.transform.position = Vector3.Lerp(
                mainCam.transform.position,
                targetPosition,
                Time.deltaTime * cameraFollowSpeed
            );
        }
    }

    /// <summary>
    /// 애니메이션 처리 메서드
    /// </summary>
    void HandleAnimation()
    {
        if (!enableAnimation || animator == null) return;

        // 이동 애니메이션 처리
        HandleMoveAnimation();

        // 공격 애니메이션 처리
        HandleAttackAnimation();

        // 애니메이션 속도 조정
        if (animator.speed != animationSpeedMultiplier)
        {
            animator.speed = animationSpeedMultiplier;
        }
    }

    /// <summary>
    /// 이동 애니메이션 처리 (즉시 정지 반영)
    /// </summary>
    void HandleMoveAnimation()
    {
        if (!HasParameter(moveParameterName, AnimatorControllerParameterType.Float)) return;

        bool isMoving = movementComponent != null && movementComponent.IsMoving();
        bool hasInputOrVelocity = VirtualPad.IsActive() || currentVelocity.magnitude > 0.01f;

        // 🔥 즉시 정지 시 애니메이션도 즉시 반영
        float targetMoveValue;
        if (enableInstantStop)
        {
            targetMoveValue = (isMoving && hasInputOrVelocity) ? 1f : 0f;
        }
        else
        {
            targetMoveValue = isMoving ? 1f : 0f;
        }

        // 애니메이션 전환 속도 조정
        float animationSpeed = enableInstantStop ? 15f : 5f; // 즉시 정지 모드에서는 더 빠른 전환
        currentMoveValue = Mathf.Lerp(currentMoveValue, targetMoveValue, Time.deltaTime * animationSpeed);

        // 애니메이터에 값 전달
        animator.SetFloat(moveParameterName, currentMoveValue);
    }

    /// <summary>
    /// 공격 애니메이션 처리 (Trigger 파라미터)
    /// </summary>
    void HandleAttackAnimation()
    {
        if (!HasParameter(attackTriggerName, AnimatorControllerParameterType.Trigger)) return;

        // 현재 공격 중인지 확인
        bool isCurrentlyAttacking = IsCurrentlyAttacking();

        // 공격이 시작된 순간에만 트리거 발동
        if (isCurrentlyAttacking && !wasAttacking)
        {
            TriggerAttackAnimation();
        }

        wasAttacking = isCurrentlyAttacking;
    }

    /// <summary>
    /// 현재 플레이어가 공격 중인지 확인
    /// </summary>
    bool IsCurrentlyAttacking()
    {
        if (combatComponent == null) return false;

        // 타겟이 있고 공격 가능한 상태인지 확인
        Transform currentTarget = combatComponent.GetCurrentTarget();
        return currentTarget != null && combatComponent.CanAttack();
    }

    /// <summary>
    /// 공격 애니메이션 트리거 발동
    /// </summary>
    public void TriggerAttackAnimation()
    {
        if (!enableAnimation || animator == null) return;

        if (HasParameter(attackTriggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(attackTriggerName);
#if !PLAYABLE_AD
            Debug.Log("Player attack animation triggered");
#endif

        }
    }

    /// <summary>
    /// 애니메이터 파라미터 존재 여부 확인
    /// </summary>
    bool HasParameter(string paramName, AnimatorControllerParameterType paramType)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName && param.type == paramType)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 애니메이터 설정 유효성 검사 및 경고
    /// </summary>
    void ValidateAnimator()
    {
        if (!enableAnimation) return;

        if (animator == null)
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"PlayerController: Animator가 할당되지 않았습니다. {gameObject.name}");
#endif

            return;
        }

        // 파라미터 존재 여부 확인 및 경고
        if (!HasParameter(moveParameterName, AnimatorControllerParameterType.Float))
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"PlayerController: Animator에 '{moveParameterName}' Float 파라미터가 없습니다.");
#endif
        }

        if (!HasParameter(attackTriggerName, AnimatorControllerParameterType.Trigger))
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"PlayerController: Animator에 '{attackTriggerName}' Trigger 파라미터가 없습니다.");
#endif
        }
    }

    /// <summary>
    /// WebGL 환경을 위한 터렛 목록 캐싱
    /// </summary>
    void RefreshTurretCache()
    {
        cachedTurrets.Clear();

        // GameManager에서 터렛들을 직접 가져오기
        if (GameManager.Instance != null && GameManager.Instance.m_Turrets != null)
        {
            foreach (var turret in GameManager.Instance.m_Turrets)
            {
                if (turret != null && turret.gameObject.activeInHierarchy && turret.IsBuilt())
                {
                    cachedTurrets.Add(turret);
                }
            }
        }
#if !PLAYABLE_AD
        Debug.Log($"Player cached {cachedTurrets.Count} built turrets for interaction");
#endif

    }

    /// <summary>
    /// 플레이어 위치를 설정된 경계 내로 제한합니다
    /// </summary>
    void ClampToBoundary()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, boundaryMin.x, boundaryMax.x);
        pos.z = Mathf.Clamp(pos.z, boundaryMin.y, boundaryMax.y);
        transform.position = pos;
    }
    IEnumerator CheckTurretInteraction()
    {
        while (true)
        {
            if (usePhysicsDetection)
            {
                CheckNearbyTurretsPhysics(); // 기존 물리 방식
            }
            else
            {
                CheckNearbyTurretsWebGL(); // WebGL용 거리 기반 방식
            }

            yield return new WaitForSeconds(turretCheckInterval);
        }
    }

    /// <summary>
    /// 기존 물리 기반 터렛 감지 (WebGL이 아닌 환경용)
    /// </summary>
    void CheckNearbyTurretsPhysics()
    {
        Collider[] turrets = Physics.OverlapSphere(transform.position, interactionRange, turretLayer);

        foreach (Collider turretCollider in turrets)
        {
            TurretController turret = turretCollider.GetComponent<TurretController>();
            if (turret != null && turret.GetStoredGold() > 0)
            {
                AutoCollectTurretGold(turret);
            }
        }
    }

    /// <summary>
    /// WebGL 환경을 위한 터렛 감지 시스템 (Physics 대신 거리 계산 사용)
    /// </summary>
    void CheckNearbyTurretsWebGL()
    {
        // 주기적으로 터렛 캐시 갱신
        if (Time.time - lastTurretCacheTime > turretCacheInterval)
        {
            RefreshTurretCache();
            lastTurretCacheTime = Time.time;
        }

        // 캐시된 터렛들과의 거리 체크
        foreach (TurretController turret in cachedTurrets)
        {
            if (turret == null || !turret.gameObject.activeInHierarchy || !turret.IsBuilt()) continue;

            float distance = Vector3.Distance(transform.position, turret.transform.position);

            if (distance <= interactionRange)
            {
                if (turret.GetStoredGold() > 0)
                {
                    AutoCollectTurretGold(turret);
#if !PLAYABLE_AD
                    Debug.Log($"Player collected {turret.GetStoredGold()} gold from turret at distance {distance:F2}");
#endif

                }
            }
        }
    }

    void AutoCollectTurretGold(TurretController turret)
    {
        int storedGold = turret.GetStoredGold();
        if (storedGold > 0)
        {
            turret.CollectGold(storedGold);
        }
    }

    public void OnSendGoldToTurret(Transform turretTransform)
    {
        if (m_golds.Count > 0)
        {
            GoldPickup goldToSend = m_golds.Pop();
            goldToSend.Initialize(1, AnimationType.Fly, turretTransform);
        }
    }

    public void OnGoldStackCall(int _currentGold)
    {
        float _spacing = 0.2f;
        if (GoldBag)
        {
            GameObject _gold = ObjectPool.Instance.SpawnFromPool("Gold", GoldBag.position + new Vector3(0, _currentGold * _spacing, 0), Quaternion.Euler(90, 0, 0), GoldBag);
            GoldPickup _goldPickUp = _gold.GetComponent<GoldPickup>();
            if (_goldPickUp != null)
            {
                _goldPickUp.Initialize(1, AnimationType.Stack);
                m_golds.Push(_goldPickUp);
            }
        }
    }

    public Transform GetGoldStackTopPos()
    {
        if(m_golds.Count>0)
        {
            return m_golds.Peek().transform;
        }

        return GoldBag;
    }
    #region Range Indicator
    void InitializeRangeIndicator()
    {
        rangeTriangles.Clear();

        for (int i = 0; i < triangleCount; i++)
        {
            GameObject triangle = ObjectPool.Instance.SpawnFromPool("RangeTriangle", transform.position, Quaternion.identity, this.transform);
            if (triangle != null)
            {
                triangle.SetActive(false);
                rangeTriangles.Add(triangle);
            }
        }
    }

    IEnumerator RangeIndicatorRotation()
    {
        while (true)
        {
            currentRotationAngle += indicatorRotationSpeed * Time.deltaTime;
            if (currentRotationAngle >= 360f)
                currentRotationAngle -= 360f;

            UpdateTrianglePositions();
            UpdateRangeIndicatorVisibility();

            yield return null;
        }
    }

    void UpdateRangeIndicatorVisibility()
    {
        bool hasTarget = combatComponent.GetCurrentTarget() != null;
        SetRangeIndicatorActive(showRangeIndicator /*&& hasTarget*/);
    }

    void SetRangeIndicatorActive(bool active)
    {
        for (int i = 0; i < rangeTriangles.Count; i++)
        {
            if (rangeTriangles[i] != null)
                rangeTriangles[i].SetActive(active);
        }
    }

    void UpdateTrianglePositions()
    {
        if (rangeTriangles.Count == 0) return;

        float attackRange = combatComponent.GetAttackRange();
        float angleStep = 360f / triangleCount;

        for (int i = 0; i < rangeTriangles.Count; i++)
        {
            if (rangeTriangles[i] != null && rangeTriangles[i].activeInHierarchy)
            {
                float angle = (angleStep * i + currentRotationAngle) * Mathf.Deg2Rad;

                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * attackRange,
                    0.1f,
                    Mathf.Sin(angle) * attackRange
                );

                rangeTriangles[i].transform.position = transform.position + offset;

                Vector3 lookDirection = (transform.position - rangeTriangles[i].transform.position).normalized;
                rangeTriangles[i].transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
    #endregion
#if !PLAYABLE_AD
    #region Gizmos (씬 뷰에서 경계 표시)
    void OnDrawGizmosSelected()
    {
        if (!showBoundaryGizmos || !enableBoundary) return;

        // 경계 박스 그리기
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(
            (boundaryMin.x + boundaryMax.x) * 0.5f,
            transform.position.y,
            (boundaryMin.y + boundaryMax.y) * 0.5f
        );
        Vector3 size = new Vector3(
            boundaryMax.x - boundaryMin.x,
            0.1f,
            boundaryMax.y - boundaryMin.y
        );
        Gizmos.DrawWireCube(center, size);

        // 경계선 그리기
        Gizmos.color = Color.red;
        Vector3 bottomLeft = new Vector3(boundaryMin.x, transform.position.y, boundaryMin.y);
        Vector3 bottomRight = new Vector3(boundaryMax.x, transform.position.y, boundaryMin.y);
        Vector3 topLeft = new Vector3(boundaryMin.x, transform.position.y, boundaryMax.y);
        Vector3 topRight = new Vector3(boundaryMax.x, transform.position.y, boundaryMax.y);

        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);

        // 상호작용 범위 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
    #endregion
#endif
    /// <summary>
    /// 플레이어 이동 시 건물 충돌 체크 (GameManager의 시스템 사용)
    /// </summary>
    public Vector3 GetValidPlayerPosition(Vector3 currentPos, Vector3 targetPos)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetValidMovePosition(currentPos, targetPos, transform,true);
        }
        return targetPos; // 폴백
    }

    public int GetCurrentHealth()
    {
        throw new System.NotImplementedException();
    }

    public int GetMaxHealth()
    {
        throw new System.NotImplementedException();
    }

    public void TakeDamage(int damage)
    {
#if !PLAYABLE_AD
        Debug.Log("======Player Took Damage!");
#endif

    }

    public void Heal(int amount)
    {
        throw new System.NotImplementedException();
    }

    public bool IsDead()
    {
        //플레이어는 무조건 살이있게
        return false;
    }
    public bool CanTargetable()
    {
        return targetable;
    }
    public void OnHealthChanged(int currentHealth, int maxHealth)
    {
        //throw new System.NotImplementedException();
    }

    public void OnDeath()
    {
        //throw new System.NotImplementedException();
    }

    public float GetHealthRatio()
    {
        throw new System.NotImplementedException();
    }
}