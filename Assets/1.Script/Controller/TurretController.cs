using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TurretController : MonoBehaviour, IHealth , ICollectable
{
    [Header("Turret Settings")]
    public int turretCost = 20; // 터렛 건설 비용
    public float detectionRange = 8f; // 적 감지 범위
    public int turretDamage = 25; // 터렛 공격력
    public float fireRate = 1f; // 발사 속도 (초당)
    public int currPaidCost = 0; // 현재 지불된 비용
    public float DitectDistance = 2f; // 플레이어 감지 거리
    public bool canCollectGold = true;
    [Header("Can Targetable")]
    public bool targetable = true;
    [Header("Upgrade Settings")]
    public bool isUpgraded = false; // 업그레이드 여부
    public int maxBulletCount = 2; // 업그레이드 후 최대 발사 개수
    public float multiShotAngle = 15f; // 멀티샷 사이 각도 (degree)
    public float upgradedFireRate = 1.5f; // 업그레이드된 터렛의 발사 간격

    [Header("Health Settings")]
    public int maxHealth = 100; // 터렛의 체력
    private int currentHealth;

    [Header("Turret Parts")]
    public GameObject turretOBJ; // Turret_OBJ
    public GameObject outLine; // OutLine
    public GameObject coin; // Coin
    public GameObject goldStoragePoint; // 골드 저장소 위치
    public GameObject goldslider;
    private float origoldSliderSize = 1.9f;

    [Header("Visual Effects")]
    [SerializeField] private float damageFlashDuration = 0.15f;
    [SerializeField] private float damageScaleDuration = 0.2f;
    [SerializeField] private float damageScaleMultiplier = 1.1f;

    [Header("Animation")]
    [SerializeField] private float AnimationDuration = 1f;
    [SerializeField] private float WaitTimeBeforeRising = 0.15f;
    [Header("HP Display (SpriteRenderer)")]
    public Transform hpBarParent; // HP 바 부모 오브젝트
    public SpriteRenderer hpBackgroundRenderer; // HP 배경 스프라이트 (HPBack)
    public SpriteRenderer hpFillRenderer; // HP 채우기 스프라이트 (HP)
    public Sprite hpBackgroundSprite; // HP 배경 스프라이트
    public Sprite hpFillSprite; // HP 채우기 스프라이트
    public Vector3 hpBarOffset = new Vector3(0, 3f, 0); // 메인센터 위 HP 바 위치
    public Vector2 hpBarSize = new Vector2(2f, 0.3f); // HP 바 크기 (World Space)
    public Color hpFullColor = Color.green; // 체력 100% 색상
    public Color hpLowColor = Color.red; // 체력 낮을 때 색상
    // 피격 연출용 원본 데이터 저장
    private Renderer[] buildingRenderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private bool isFlashingDamage = false;

    private bool isBuilt = false;
    private bool isVisible = true; // 순차 건설의 가시성 관리
    private Vector3 originalScale2;
    private bool originalScaleSaved = false;
    private GoldStorage goldStorage;

    private CombatComponent combatComponent;
    private PlayerController _player;
    public TextMeshPro textMeshPro; // 비용 표시용 텍스트

    // 업그레이드된 터렛 전용 변수들
    private bool isUpgradedCombatRunning = false;
    private float lastUpgradedAttackTime = 0f;

    public void Init()
    {
        currentHealth = maxHealth;
        _player = GameManager.Instance.m_Player;

        // Combat Component 초기화
        combatComponent = GetComponent<CombatComponent>();
        if (combatComponent == null)
        {
            combatComponent = gameObject.AddComponent<CombatComponent>();
        }

        // 원본 크기와 피격 연출 초기화
        SaveOriginalScale();
        InitializeDamageEffect();

        InitializeGoldSlider();
        InitializeGoldStorage();
        InitializeHPBar();
        InitializeCostDisplay(); // 비용 표시 초기화
        SetBuildState(false);

        if (isVisible)
        {
            StartCoroutine(CheckAndBuildBehavior());
        }
    }

    void InitializeGoldSlider()
    {
        if (goldslider != null)
        {
            goldslider.transform.localScale = new Vector3(0, origoldSliderSize, 0);
        }
    }

    void InitializeHPBar()
    {
        if (hpBackgroundRenderer != null)
            hpBackgroundRenderer.gameObject.SetActive(false);
    }
    /// <summary>
    /// 피격 연출을 위한 초기화 (렌더러와 원본 색상 저장)
    /// </summary>
    void InitializeDamageEffect()
    {
        // turretOBJ 하위의 모든 렌더러 수집
        if (turretOBJ != null)
        {
            buildingRenderers = turretOBJ.GetComponentsInChildren<Renderer>();
            originalColors = new Color[buildingRenderers.Length];

            for (int i = 0; i < buildingRenderers.Length; i++)
            {
                if (buildingRenderers[i].material != null)
                {
                    originalColors[i] = buildingRenderers[i].material.color;
                }
            }
        }
    }

    /// <summary>
    /// turretOBJ의 원본 크기를 저장합니다
    /// </summary>
    void SaveOriginalScale()
    {
        if (turretOBJ != null && !originalScaleSaved)
        {
            originalScale = turretOBJ.transform.localScale;
            originalScale2 = originalScale; // 백업용
            originalScaleSaved = true;
        }
    }

    void InitializeGoldStorage()
    {
        if (canCollectGold)
        {

            // GoldStorage 컴포넌트 확인 및 추가
            goldStorage = goldStoragePoint.GetComponent<GoldStorage>();
            if (goldStorage == null)
            {
                goldStorage = goldStoragePoint.AddComponent<GoldStorage>();
            }

            // 골드 저장소 초기화 및 중심점 설정
            goldStorage.storageCenter = goldStoragePoint.transform;
            goldStorage.Init();
#if !PLAYABLE_AD
            Debug.Log($"TurretController: GoldStorage initialized on {goldStoragePoint.name}");
#endif

        }
        else
        {
            if (goldStoragePoint != null)
            {
                goldStoragePoint.SetActive(false);
#if !PLAYABLE_AD
                Debug.Log($"TurretController: goldStoragePoint is Disabled {goldStoragePoint.name}");
#endif
            }
        }
    }

    void InitializeCostDisplay()
    {
#if !PLAYABLE_AD
       // 비용 표시용 TextMeshPro 자동 생성
        if (textMeshPro == null)
        {
            GameObject textObj = new GameObject("CostText");

            // 부모를 outLine으로 설정 (outLine이 있을 때만)
            if (outLine != null)
                textObj.transform.SetParent(outLine.transform);
            else
                textObj.transform.SetParent(transform);

            textObj.transform.localPosition = new Vector3(0, 2f, 0); // 터렛 위쪽에 표시

            // TextMeshPro 컴포넌트 추가 및 설정
            textMeshPro = textObj.AddComponent<TextMeshPro>();
            textMeshPro.fontSize = 1.5f;
            textMeshPro.color = Color.yellow;
            textMeshPro.alignment = TextAlignmentOptions.Center;
            textMeshPro.sortingOrder = 10; // 다른 UI보다 위에 표시

            // 카메라를 향하도록 설정 (Billboard 효과)
            if (Camera.main != null)
            {
                textMeshPro.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
            }
        }
#endif
 

        UpdateCostDisplay();
    }

    void UpdateCostDisplay()
    {
        if (textMeshPro == null) return;

        // 터렛이 이미 건설되었거나 보이지 않으면 텍스트 숨김
        if (isBuilt || !isVisible)
        {
            textMeshPro.gameObject.SetActive(false);
            // 건설 완료되면 슬라이더도 숨김
            UpdateGoldSlider(1.0f); // 100% 채운 후
            if (goldslider != null)
            {
                StartCoroutine(HideSliderAfterCompletion());
            }
        }
        else
        {
            // 남은 비용 표시
            int remainingCost = turretCost - currPaidCost;
            if (remainingCost > 0)
            {
                textMeshPro.gameObject.SetActive(true);
                textMeshPro.text = $"{remainingCost}";

                // 건설 진행도에 따른 슬라이더 업데이트
                float progress = (float)currPaidCost / turretCost;
                UpdateGoldSlider(progress);
            }
            else
            {
                textMeshPro.gameObject.SetActive(false);
                UpdateGoldSlider(1.0f); // 100% 완료
            }
        }
    }

    /// <summary>
    /// 골드 슬라이더 업데이트 (건설 진행도 표시)
    /// </summary>
    /// <param name="progress">진행도 (0.0 ~ 1.0)</param>
    void UpdateGoldSlider(float progress)
    {
        if (goldslider == null) return;

        // 진행도에 따른 Y축 스케일 조정
        progress = Mathf.Clamp01(progress);
        float targetScaleX = origoldSliderSize * progress;

        Vector3 currentScale = goldslider.transform.localScale;
        Vector3 targetScale = new Vector3(targetScaleX, currentScale.y, currentScale.z);

        goldslider.transform.localScale = targetScale;

        // 슬라이더가 보이도록 활성화
        if (!goldslider.activeInHierarchy && progress > 0 && !isBuilt)
        {
            goldslider.SetActive(true);
        }
    }

    /// <summary>
    /// 건설 완료 후 슬라이더를 부드럽게 숨기는 코루틴
    /// </summary>
    IEnumerator HideSliderAfterCompletion()
    {
        // 1초 대기 후 슬라이더 숨김
        yield return new WaitForSeconds(1.0f);

        if (goldslider != null && isBuilt)
        {
            // 부드럽게 사라지는 애니메이션
            float duration = 0.5f;
            float elapsedTime = 0f;
            Vector3 startScale = goldslider.transform.localScale;
            Vector3 endScale = new Vector3(startScale.x, 0, startScale.z);

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;

                goldslider.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }

            goldslider.SetActive(false);
        }
    }

    public void SetVisibility(bool visible,bool _ruleHide = false)
    {
        isVisible = visible;

        if (visible)
        {
            // 터렛을 보이게 할 때
            gameObject.SetActive(true);
            SetBuildState(isBuilt); // 현재 건설 상태에 맞게 UI 업데이트

            // 아직 건설되지 않았고 건설 체크가 실행 중이 아니라면 시작
            if (!isBuilt)
            {
                StartCoroutine(CheckAndBuildBehavior());
            }
#if !PLAYABLE_AD
            Debug.Log($"Turret {gameObject.name} is now visible and available for construction");
#endif

        }
        else
        {
            // 터렛을 숨길 때
            if (outLine != null) outLine.SetActive(false);
            if (coin != null) coin.SetActive(false);
            if (_ruleHide && turretOBJ != null)
                turretOBJ.SetActive(false);
            // 건설되지 않은 터렛은 완전히 비활성화
            if (!isBuilt)
            {
                gameObject.SetActive(false);
            }
#if !PLAYABLE_AD
            Debug.Log($"Turret {gameObject.name} is now hidden");
#endif

        }
    }

    void SetBuildState(bool built)
    {
        isBuilt = built;

        if (turretOBJ != null)
            turretOBJ.SetActive(built);

        if (outLine != null)
            outLine.SetActive(!built && isVisible); // 가시성도 고려

        if (coin != null)
            coin.SetActive(!built && isVisible); // 가시성도 고려

        if (goldStoragePoint != null)
            goldStoragePoint.SetActive(built);
        // 건설 완료 시 올라오는 애니메이션 및 채굴 시작
        if (built)
        {
            StartCoroutine(TurretRiseAnimation());
        }
    }

    IEnumerator TurretRiseAnimation()
    {
        if (turretOBJ == null) yield break;

        Vector3 originalTurretScale = turretOBJ.transform.localScale;
        Vector3 originalGoldScale = Vector3.one;

        if (goldStoragePoint != null)
            originalGoldScale = goldStoragePoint.transform.localScale;

        turretOBJ.transform.localScale = Vector3.zero;
        if (goldStoragePoint != null)
            goldStoragePoint.transform.localScale = Vector3.zero;

        //TODO CHECK
        //2025-12-04
        yield return new WaitForSeconds(WaitTimeBeforeRising);

        EffectController _effect = ObjectPool.Instance.SpawnFromPool("Effect", this.transform.position, Quaternion.identity, ObjectPool.Instance.transform).GetComponent<EffectController>();
        if (_effect)
            _effect.Init(EffectType.Building, this.transform.position.x, this.transform.position.z, turretOBJ.transform.localRotation.y, 0.3f);

        if (float.IsNaN(originalTurretScale.x) || float.IsNaN(originalTurretScale.y) || float.IsNaN(originalTurretScale.z))
        {
            originalTurretScale = Vector3.one;
        }

        if (float.IsNaN(originalGoldScale.x) || float.IsNaN(originalGoldScale.y) || float.IsNaN(originalGoldScale.z))
        {
            originalGoldScale = Vector3.one;
        }

        // 터렛을 땅 아래로 이동하고 작은 크기로 설정
        turretOBJ.transform.localScale = originalTurretScale * 0.3f;
        if (goldStoragePoint != null)
            goldStoragePoint.transform.localScale = originalGoldScale * 0.3f;

        float _animationDuration = AnimationDuration; // 1초로 단축
        float elapsedTime = 0f;

        //TODO TEST
        //2025-12-04
        //일단 80퍼센트 정도 됐을때 건설됬다는 신호 보내기
        bool _sendQue = false;
        // 땅 아래서 위로 올라오면서 크기 변화하는 애니메이션
        while (elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / _animationDuration); // NaN 방지

            // 크기: 단순하고 안전한 탄성 애니메이션
            float scaleMultiplier = 1.0f;

            if (progress <= 0.5f)
            {
                // 0~50%: 0.3 → 1.15 (빠르게 커짐)
                float t = progress / 0.5f;
                t = Mathf.Clamp01(t);
                scaleMultiplier = Mathf.Lerp(0.3f, 1.15f, t);
            }
            else if (progress <= 0.8f)
            {
                //TODO TEST
                //2025-12-04
                //일단 80퍼센트 정도 됐을때 건설됬다는 신호 보내기
                if (!_sendQue)
                {
                    // 건설 완료 알림을 GameManager에 전송
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.OnTurretBuilt(this);
                        _sendQue = true;
                    }
                }
                // 50~80%: 1.15 → 0.9 (탄성 수축)
                float t = (progress - 0.5f) / 0.3f;
                t = Mathf.Clamp01(t);
                scaleMultiplier = Mathf.Lerp(1.15f, 0.9f, t);
            }
            else
            {
                // 80~100%: 0.9 → 1.0 (최종 크기)
                float t = (progress - 0.8f) / 0.2f;
                t = Mathf.Clamp01(t);
                scaleMultiplier = Mathf.Lerp(0.9f, 1.0f, t);
            }

            // 안전한 스케일 계산
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.1f, 2.0f); // 안전 범위 제한
            Vector3 currentTurretScale = originalTurretScale * scaleMultiplier;
            Vector3 currentGoldScale = originalGoldScale * scaleMultiplier;

            if (!float.IsNaN(currentTurretScale.x) && !float.IsNaN(currentTurretScale.y) && !float.IsNaN(currentTurretScale.z))
            {
                turretOBJ.transform.localScale = currentTurretScale;
            }

            if (goldStoragePoint != null && !float.IsNaN(currentGoldScale.x) && !float.IsNaN(currentGoldScale.y) && !float.IsNaN(currentGoldScale.z))
            {
                goldStoragePoint.transform.localScale = currentGoldScale;
            }

            yield return null;
        }

        if (!float.IsNaN(originalTurretScale.x) && !float.IsNaN(originalTurretScale.y) && !float.IsNaN(originalTurretScale.z))
        {
            turretOBJ.transform.localScale = originalTurretScale;
        }

        if (!float.IsNaN(originalGoldScale.x) && !float.IsNaN(originalGoldScale.y) && !float.IsNaN(originalGoldScale.z))
        {
            goldStoragePoint.transform.localScale = originalGoldScale;
        }

        // 애니메이션 완료 후 전투 시작
        StartCombat();

        //// 건설 완료 알림을 GameManager에 전송
        //if (GameManager.Instance != null)
        //{
        //    GameManager.Instance.OnTurretBuilt(this);
        //}
    }

    void StartCombat()
    {
        if (combatComponent != null)
        {
            combatComponent.Initialize();
#if !PLAYABLE_AD
            Debug.Log($"Turret {gameObject.name} started combat (Upgraded: {isUpgraded})");
#endif

        }
    }

    IEnumerator CheckAndBuildBehavior()
    {
        while (!isBuilt && isVisible) // 가시성 체크 추가
        {
            if (_player != null)
            {
                float distance = Vector3.Distance(_player.transform.position, transform.position);
                if (distance <= DitectDistance) // 건설 범위
                {
                    // 플레이어 골드를 가지고 있고 아직 건설이 완료되지 않은 경우
                    if (GameManager.Instance.GetCurrentGold() > 0 && currPaidCost < turretCost)
                    {
                        SendGoldToTurret();
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void SendGoldToTurret()
    {
        if (GameManager.Instance.SpendGold(1))
        {
            // 플레이어로부터 골드를 시각적으로 보내는 메서드 호출
            _player.OnSendGoldToTurret(transform);
        }
    }

    public void CollectGold()
    {
        currPaidCost++;
        // 비용 표시 업데이트 (슬라이더 포함)
        UpdateCostDisplay();
#if !PLAYABLE_AD
        Debug.Log($"Turret received gold: {currPaidCost}/{turretCost}");
#endif


        // 목표 비용에 도달하면 건설 완료
        if (currPaidCost == turretCost)
        {
            BuildTurret();
        }
    }
    void BuildTurret()
    {
#if !PLAYABLE_AD
        Debug.Log($"Turret {gameObject.name} construction completed!");
#endif

        SetBuildState(true);
    }

    // 골드 저장소 관련 API
    public int GetStoredGold()
    {
        return goldStorage != null ? goldStorage.GetStoredGoldCount() : 0;
    }

    public bool CollectGold(int amount)
    {
        if (goldStorage != null && goldStorage.GetStoredGoldCount() >= amount)
        {
            goldStorage.TransferGoldTo(_player.transform, amount);
#if !PLAYABLE_AD
            Debug.Log($"Turret transferred {amount} gold to player");
#endif

            return true;
        }
        return false;
    }

    public GoldStorage GetgoldStorage() => goldStorage == null ? null : goldStorage;

    // 순차 건설 시스템용 확인 메서드들
    public bool IsBuilt() => isBuilt;
    public bool IsVisible() => isVisible;
    public int GetPaidCost() => currPaidCost;
    public float GetBuildProgress() => (float)currPaidCost / turretCost;

    /// <summary>
    /// 터렛이 업그레이드되었는지 확인
    /// </summary>
    /// <returns>업그레이드 여부</returns>
    public bool IsUpgraded() => isUpgraded;

    /// <summary>
    /// NPC에 의한 터렛 업그레이드 (화살 개수 증가)
    /// </summary>
    public void UpgradeTurret()
    {
        if (!isUpgraded && isBuilt)
        {
            isUpgraded = true;
#if !PLAYABLE_AD
            Debug.Log($"Turret {gameObject.name} has been upgraded! Now fires {maxBulletCount} bullets!");
#endif


            // 업그레이드된 전투 시작 (중복 실행 방지)
            if (!isUpgradedCombatRunning)
            {
                StartCoroutine(UpgradedTurretCombat());
            }

            // 업그레이드 시각적 효과
            StartCoroutine(UpgradeEffect());
        }
    }

    /// <summary>
    /// 업그레이드 시각적 효과
    /// </summary>
    IEnumerator UpgradeEffect()
    {
        if (turretOBJ == null) yield break;

        // 간단한 스케일 애니메이션
        Vector3 originalScale = turretOBJ.transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;

        // 커지기
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            turretOBJ.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        // 원래 크기로
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            turretOBJ.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        turretOBJ.transform.localScale = originalScale;
    }

    /// <summary>
    /// 업그레이드된 터렛 전용 전투 로직 (멀티샷) - 무한 루프 방지
    /// </summary>
    IEnumerator UpgradedTurretCombat()
    {
        isUpgradedCombatRunning = true;

        while (isBuilt && !IsDead() && isUpgraded && gameObject.activeInHierarchy)
        {
            // 시간 기반 공격 체크 (무한 루프 방지)
            if (Time.time - lastUpgradedAttackTime >= upgradedFireRate)
            {
                // 적 찾기
                List<Transform> nearbyEnemies = FindNearbyEnemies();

                if (nearbyEnemies.Count > 0)
                {
                    PerformMultiShot(nearbyEnemies);
                    lastUpgradedAttackTime = Time.time; // 공격 시간 업데이트
                }
            }

            // 프레임 레이트 제한 (CPU 사용량 절약)
            yield return new WaitForSeconds(0.1f);
        }

        isUpgradedCombatRunning = false;
#if !PLAYABLE_AD
        Debug.Log($"Upgraded turret combat stopped for {gameObject.name}");
#endif

    }

    /// <summary>
    /// 멀티샷 공격 (여러 개의 총알을 서로 다른 타겟에 발사) - 안전성 강화
    /// </summary>
    void PerformMultiShot(List<Transform> availableTargets)
    {
        if (availableTargets == null || availableTargets.Count == 0) return;
        if (combatComponent == null || combatComponent.weaponComponent == null) return;

        Vector3 firePosition = combatComponent.weaponComponent.GetFirePoint().position;

        // 발사할 총알 개수 결정 (최대 maxBulletCount개, 실제 타겟 수에 맞춤)
        int bulletsToFire = Mathf.Min(maxBulletCount, availableTargets.Count);

        int successfulShots = 0; // 성공적으로 발사된 총알 수 추적

        for (int i = 0; i < bulletsToFire && i < availableTargets.Count; i++)
        {
            Transform target = availableTargets[i];
            if (target == null || !target.gameObject.activeInHierarchy) continue;

            // 총알 생성 및 발사 (ObjectPool 실패 시 무한 루프 방지)
            string bulletType = combatComponent.weaponComponent.GetBulletType();

            // 멀티샷일 때 약간의 각도 조정
            Quaternion fireRotation = combatComponent.weaponComponent.GetFirePoint().rotation;
            if (bulletsToFire > 1)
            {
                float angleOffset = (i - (bulletsToFire - 1) * 0.5f) * multiShotAngle;
                fireRotation *= Quaternion.Euler(0, angleOffset, 0);
            }

            GameObject bullet = ObjectPool.Instance.SpawnFromPool(bulletType, firePosition, fireRotation);

            if (bullet != null)
            {
                BulletController bulletController = bullet.GetComponent<BulletController>();
                if (bulletController != null)
                {
                    bulletController.SetTarget(target, BulletOwner.Turret, this);
                    AudioManager.Instance.PlayArrowAttackSound();
                    successfulShots++;
                }
            }
            else
            {
#if !PLAYABLE_AD
                // ObjectPool에서 총알을 가져오지 못한 경우 경고 후 중단 (무한 생성 방지)
                Debug.LogWarning($"Failed to spawn bullet from ObjectPool for upgraded turret {gameObject.name}. Pool may be exhausted.");
#endif

                break;
            }
        }
#if !PLAYABLE_AD
        Debug.Log($"Upgraded turret {gameObject.name} fired {successfulShots} bullets at {bulletsToFire} targets");
#endif

    }

    /// <summary>
    /// 주변의 적들을 찾아서 거리순으로 정렬된 리스트 반환 (성능 최적화)
    /// </summary>
    List<Transform> FindNearbyEnemies()
    {
        List<Transform> enemies = new List<Transform>();

        // 성능 최적화: 모든 적을 찾는 대신 combatComponent의 타겟팅 시스템 활용
        if (combatComponent != null)
        {
            Transform primaryTarget = combatComponent.GetCurrentTarget();
            if (primaryTarget != null && IsTargetValid(primaryTarget))
            {
                enemies.Add(primaryTarget);
            }
        }

        // 추가 타겟들 찾기 (최대 5개까지만 - 성능 제한)
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();
        int foundCount = 0;
        const int maxSearch = 10; // 성능을 위해 최대 10개까지만 검색

        foreach (EnemyController enemy in allEnemies)
        {
            if (foundCount >= maxSearch) break; // 성능 제한
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            // 이미 추가된 타겟은 건너뛰기
            if (enemies.Contains(enemy.transform)) continue;

            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance <= detectionRange && IsTargetValid(enemy.transform))
            {
                enemies.Add(enemy.transform);
                foundCount++;

                // 멀티샷에 필요한 만큼만 찾기
                if (enemies.Count >= maxBulletCount) break;
            }
        }

        // 거리 순으로 정렬 (가까운 적부터)
        enemies.Sort((a, b) =>
        {
            if (a == null || b == null) return 0;
            float distA = Vector3.Distance(transform.position, a.position);
            float distB = Vector3.Distance(transform.position, b.position);
            return distA.CompareTo(distB);
        });

        return enemies;
    }

    /// <summary>
    /// 타겟이 유효한지 확인 (안전성 체크)
    /// </summary>
    bool IsTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        // 거리 체크
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > detectionRange) return false;

        // 생존 체크
        IHealth health = target.GetComponent<IHealth>();
        if (health != null && health.IsDead()) return false;

        return true;
    }

    /// <summary>
    /// 피격 시 화이트 플래시 + 크기 변화 연출
    /// </summary>
    void StartDamageFlashEffect()
    {
        if (!isFlashingDamage)
        {
            StartCoroutine(DamageFlashEffect());
        }
    }

    IEnumerator DamageFlashEffect()
    {
        if (buildingRenderers == null || turretOBJ == null) yield break;

        isFlashingDamage = true;

        // 1. 색상 레드 플래시
        for (int i = 0; i < buildingRenderers.Length; i++)
        {
            if (buildingRenderers[i] != null && buildingRenderers[i].material != null)
            {
                buildingRenderers[i].material.color = Color.red;
            }
        }

        // 2. 크기 증가
        Vector3 scaledSize = originalScale * damageScaleMultiplier;
        turretOBJ.transform.localScale = scaledSize;

        // 3. 화이트 플래시 지속 시간
        yield return new WaitForSeconds(damageFlashDuration);

        // 4. 원본 색상으로 복귀
        for (int i = 0; i < buildingRenderers.Length; i++)
        {
            if (buildingRenderers[i] != null && buildingRenderers[i].material != null)
            {
                buildingRenderers[i].material.color = originalColors[i];
            }
        }

        // 5. 크기 복귀 애니메이션
        float elapsedTime = 0f;
        while (elapsedTime < damageScaleDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / damageScaleDuration;

            // Ease-out 효과
            progress = 1f - Mathf.Pow(1f - progress, 2f);

            turretOBJ.transform.localScale = Vector3.Lerp(scaledSize, originalScale, progress);
            yield return null;
        }

        // 6. 정확한 원본 크기로 복귀
        turretOBJ.transform.localScale = originalScale;
        isFlashingDamage = false;
    }

    #region IHealth Implementation
    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead()) return;

        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;

        OnHealthChanged(currentHealth, maxHealth);

        // 피격 연출 효과
        StartDamageFlashEffect();

        if (currentHealth <= 0)
        {
            OnDeath();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead()) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        OnHealthChanged(currentHealth, maxHealth);
    }

    public bool IsDead()
    {
        return currentHealth <= 0;
    }

    public float GetHealthRatio()
    {
        return maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }

    public void OnHealthChanged(int currentHealth, int maxHealth)
    {
        // HP바 UI 업데이트 등 (필요시 구현)
        // Debug.Log($"Turret Health: {currentHealth}/{maxHealth}");
        if (hpFillRenderer == null) return;

        if (hpFillRenderer.gameObject.activeInHierarchy == false)
            return;

        float healthRatio = (float)currentHealth / GetMaxHealth();
        // HP 바 크기 조정 (왼쪽에서부터 채워지도록)
        Vector2 fillSize = hpBarSize;
        fillSize.x *= healthRatio;
        hpFillRenderer.gameObject.transform.localScale = new Vector3(fillSize.x, fillSize.y, 1);

        // 색상 변경
        Color targetColor = Color.Lerp(hpLowColor, hpFullColor, healthRatio);
        hpFillRenderer.color = targetColor;
    }

    public void OnDeath()
    {
        // 업그레이드된 전투 중지
        isUpgradedCombatRunning = false;

        if (goldStorage != null && goldStorage.GetStoredGoldCount() > 0)
        {
            int goldToDrop = goldStorage.GetStoredGoldCount() / 2;
            for (int i = 0; i < goldToDrop; i++)
            {
                GameObject gold = ObjectPool.Instance.SpawnFromPool("Gold", transform.position + Random.insideUnitSphere * 1f, Quaternion.identity);
                if (gold != null)
                {
                    GoldPickup goldPickup = gold.GetComponent<GoldPickup>();
                    goldPickup.Initialize(1);
                }
            }
        }

        DestroyTurret();
    }

    void DestroyTurret()
    {
        gameObject.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.EndGame(false);
    }

    public bool IsBuild => isBuilt;
    #endregion
    public int GetRemainPaidGold()
    {
        return turretCost - currPaidCost;
    }
    public bool CanTargetable()
    {
        return targetable;
    }
#if !PLAYABLE_AD
    #region Gizmos (기즈모)
    void OnDrawGizmosSelected()
    {
        // 터렛 감지 범위 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 원본 스케일 영역 표시
        if (originalScaleSaved && turretOBJ != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = turretOBJ.transform.position;
            Gizmos.DrawWireCube(center, originalScale);
        }
    }
    #endregion
#endif
}