using System.Collections;
using UnityEngine;
using TMPro;

public class MainCenterController : MonoBehaviour, IHealth
{
    [Header("Center Building Structure")]
    public GameObject centerOBJ; // 실제 건물 모델
    public GameObject shadow;    // 그림자
    public GameObject outLine;   // 아웃라인
    public GameObject coin;      // 골드 아이콘
    public TextMeshPro txtRemainGold; // 필요 골드 표시
    [Header("Can Targetable")]
    public bool targetable = true;
    [Header("Upgrade Settings")]
    [SerializeField] private int upgradeCost = 50;           // 업그레이드 비용
    [SerializeField] private int currentPaidGold = 0;        // 현재 지불한 골드
    private bool isUpgraded = false;                         // 업그레이드 완료 여부
    private bool isUpgradeInProgress = false;                // 업그레이드 진행중 여부
    private bool isAvailableForUpgrade = false;              // 업그레이드 가능 상태

    [Header("Building Models")]
    [SerializeField] private GameObject preUpgradeModel;     // 업그레이드 전 모델
    [SerializeField] private GameObject postUpgradeModel;    // 업그레이드 후 모델

    [Header("Animation Settings")]
    [SerializeField] private float downAnimationTime = 0.8f;       // 내려가는 시간
    [SerializeField] private float upAnimationTime = 1.2f;         // 올라오는 시간
    [SerializeField] private AnimationCurve buildCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Gold Collection")]
    [SerializeField] private GameObject goldCollectionPoint;  // 골드 수집 지점
    [SerializeField] private float goldCollectionRange = 3.0f; // 골드 수집 범위

    [Header("Text Settings")]
    [SerializeField] private Vector3 textOffset = new Vector3(0, 2.5f, 0);
    [SerializeField] private float textSize = 1.5f;
    [SerializeField] private Color textColor = Color.yellow;

    [Header("Health Settings")]
    public int maxHealth = 300; // 메인센터 최대 체력 (업그레이드 전)
    public int upgradedMaxHealth = 500; // 업그레이드 후 최대 체력
    private int currentHealth;

    [Header("Damage Visual Effects")]
    [SerializeField] private float damageFlashDuration = 0.15f;
    [SerializeField] private float damageScaleDuration = 0.2f;
    [SerializeField] private float damageScaleMultiplier = 1.1f;
    private Renderer[] buildingRenderers;
    private Color[] originalColors;
    private Vector3 originalBuildingScale;
    private bool isFlashingDamage = false;

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

    // 참조
    private PlayerController _player;
    private Vector3 originalCenterPosition;
    private bool isInitialized = false;

    public void Init()
    {
        if (isInitialized) return;

        _player = GameManager.Instance?.m_Player;

        // 체력 초기화
        currentHealth = maxHealth;

        // HP 바 초기화
        InitializeHealthBar();
        InitializeDamageEffect();

        // 중앙 건물 원본 위치 저장
        if (centerOBJ != null)
            originalCenterPosition = centerOBJ.transform.localPosition;

        // 골드 수집 포인트 설정 및 초기 숨김
        SetupGoldCollectionPoint();

        SetupModels();
        SetupTextDisplay();
        SetUpgradeState(true);

        // 처음에는 업그레이드 불가능 상태로 설정
        SetAvailableForUpgrade(false);

        isInitialized = true;
#if !PLAYABLE_AD
        Debug.Log($"MainCenterController {gameObject.name} initialized successfully");
#endif
    }

    /// <summary>
    /// 피격 연출을 위한 초기화 (렌더러와 원본 색상/크기 저장)
    /// </summary>
    void InitializeDamageEffect()
    {
        // centerOBJ 하위의 모든 렌더러 수집
        if (centerOBJ != null)
        {
            buildingRenderers = centerOBJ.GetComponentsInChildren<Renderer>();
            originalColors = new Color[buildingRenderers.Length];

            for (int i = 0; i < buildingRenderers.Length; i++)
            {
                if (buildingRenderers[i].material != null)
                {
                    originalColors[i] = buildingRenderers[i].material.color;
                }
            }

            // 원본 크기 저장
            originalBuildingScale = centerOBJ.transform.localScale;
        }
    }

    void InitializeHealthBar()
    {
        if (hpBarParent == null)
        {
            // HP 바 부모 오브젝트가 없으면 자동 생성
            GameObject hpBarObj = new GameObject("HPBar");
            hpBarObj.transform.SetParent(transform);
            hpBarObj.transform.localPosition = hpBarOffset;
            hpBarParent = hpBarObj.transform;
        }

        // HP 배경 설정
        if (hpBackgroundRenderer == null)
        {
            GameObject hpBg = new GameObject("HPBackground");
            hpBg.transform.SetParent(hpBarParent);
            hpBg.transform.localPosition = Vector3.zero;
            hpBackgroundRenderer = hpBg.AddComponent<SpriteRenderer>();
        }

        // HP 채우기 설정
        if (hpFillRenderer == null)
        {
            GameObject hpFill = new GameObject("HPFill");
            hpFill.transform.SetParent(hpBarParent);
            hpFill.transform.localPosition = Vector3.zero;
            hpFillRenderer = hpFill.AddComponent<SpriteRenderer>();
        }

        // 스프라이트 설정
        if (hpBackgroundSprite != null)
            hpBackgroundRenderer.sprite = hpBackgroundSprite;
        if (hpFillSprite != null)
            hpFillRenderer.sprite = hpFillSprite;

        // 크기 설정
        hpBackgroundRenderer.size = hpBarSize;
        hpFillRenderer.size = hpBarSize;

        // 정렬 순서
        hpBackgroundRenderer.sortingOrder = 1;
        hpFillRenderer.sortingOrder = 2;

        // HP 바 표시 (메인센터는 항상 표시)
        if (hpBarParent != null)
            hpBarParent.gameObject.SetActive(true);

        UpdateHealthBar();
    }

    void UpdateHealthBar()
    {
        if (hpFillRenderer == null) return;

        float healthRatio = (float)currentHealth / GetMaxHealth();
        // HP 바 크기 조정 (왼쪽에서부터 채워지도록)
        Vector2 fillSize = hpBarSize;
        fillSize.x *= healthRatio;
        hpFillRenderer.gameObject.transform.localScale = new Vector3(fillSize.x, fillSize.y,1);

        // 색상 변경
        Color targetColor = Color.Lerp(hpLowColor, hpFullColor, healthRatio);
        hpFillRenderer.color = targetColor;
    }

    void SetupGoldCollectionPoint()
    {
        // goldCollectionPoint가 설정되지 않은 경우 자동 생성
        if (goldCollectionPoint == null)
        {
            GameObject collectionPoint = new GameObject("GoldCollectionPoint");
            collectionPoint.transform.SetParent(transform);
            collectionPoint.transform.localPosition = Vector3.zero;
            goldCollectionPoint = collectionPoint;
        }

        // 초기에는 골드 수집 기능 비활성화 (UI 요소들 숨김)
        if (outLine != null) outLine.SetActive(false);
        if (coin != null) coin.SetActive(false);
#if !PLAYABLE_AD
        Debug.Log("GoldCollectionPoint setup completed (initially hidden)");
#endif

    }

    // GameManager에서 호출되는 메서드 - 모든 Mine 건설 완료 후 활성화
    public void SetAvailableForUpgrade(bool available)
    {
        isAvailableForUpgrade = available;
#if !PLAYABLE_AD
        Debug.Log($"MainCenter upgrade availability changed: {available}");
#endif


        if (available && !isUpgraded && !isUpgradeInProgress)
        {
            // 업그레이드 가능 상태가 되면 UI 요소들 표시하고 골드 수집 시작
            if (outLine != null) outLine.SetActive(true);
            if (coin != null) coin.SetActive(true);

            SetupTextDisplay();
            StartCoroutine(CheckPlayerProximity());
#if !PLAYABLE_AD
            Debug.Log("MainCenter is now available for upgrade!");
#endif

        }
        else if (!available)
        {
            // 업그레이드 불가능 상태면 UI 숨김
            if (outLine != null) outLine.SetActive(false);
            if (coin != null) coin.SetActive(false);
            if (txtRemainGold != null) txtRemainGold.gameObject.SetActive(false);
#if !PLAYABLE_AD
            Debug.Log("MainCenter upgrade disabled");
#endif

        }
    }

    void SetupModels()
    {
        // 모델이 설정되지 않은 경우 centerOBJ 하위에서 찾기
        if (preUpgradeModel == null || postUpgradeModel == null)
        {
            if (centerOBJ != null)
            {
                // centerOBJ 하위에서 모델들 찾기
                Transform[] children = centerOBJ.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in children)
                {
                    if (child.name.Contains("PreUpgrade") || child.name.Contains("Wood"))
                    {
                        preUpgradeModel = child.gameObject;
                    }
                    else if (child.name.Contains("PostUpgrade") || child.name.Contains("Stone"))
                    {
                        postUpgradeModel = child.gameObject;
                    }
                }
            }
        }

        // 초기 상태: 업그레이드 전 모델만 활성화
        if (preUpgradeModel != null)
            preUpgradeModel.SetActive(true);

        if (postUpgradeModel != null)
            postUpgradeModel.SetActive(false);
    }

    void SetupTextDisplay()
    {
#if !PLAYABLE_AD
        // TextMeshPro 자동 생성
        if (txtRemainGold == null)
        {
            GameObject textObj = new GameObject("TxtRemainGold");

            // 부모 설정 (outLine이 있으면 outLine에, 없으면 자기 자신에)
            if (outLine != null)
                textObj.transform.SetParent(outLine.transform);
            else
                textObj.transform.SetParent(transform);

            textObj.transform.localPosition = textOffset;

            // TextMeshPro 컴포넌트 추가
            txtRemainGold = textObj.AddComponent<TextMeshPro>();

            // 설정
            txtRemainGold.fontSize = textSize;
            txtRemainGold.color = textColor;
            txtRemainGold.alignment = TextAlignmentOptions.Center;
            txtRemainGold.sortingOrder = 10;

            // 카메라를 향하도록 설정 (Billboard 효과)
            if (Camera.main != null)
            {
                txtRemainGold.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Camera.main.transform.up);
            }
        }
#endif

        UpdateGoldDisplay();
    }

    void SetUpgradeState(bool upgraded)
    {
        bool wasUpgraded = isUpgraded;
        isUpgraded = upgraded;

        // 업그레이드 상태가 변경되면 최대 체력도 변경
        if (wasUpgraded != isUpgraded)
        {
            int newMaxHealth = isUpgraded ? upgradedMaxHealth : maxHealth;
            // 현재 체력 비율 유지하면서 최대 체력 변경
            float healthRatio = GetHealthRatio();
            currentHealth = Mathf.RoundToInt(newMaxHealth * healthRatio);
            UpdateHealthBar();
        }

        UpdateGoldDisplay();
    }

    void UpdateGoldDisplay()
    {
        if (txtRemainGold == null) return;

        // 업그레이드 가능 상태가 아니거나 이미 업그레이드되었으면 텍스트 숨김
        if (!isAvailableForUpgrade || isUpgraded)
        {
            txtRemainGold.gameObject.SetActive(false);
        }
        else
        {
            int remainingCost = upgradeCost - currentPaidGold;
            if (remainingCost > 0)
            {
                txtRemainGold.gameObject.SetActive(true);
                txtRemainGold.text = $"{remainingCost}";
            }
            else
            {
                txtRemainGold.gameObject.SetActive(false);
            }
        }
    }

    IEnumerator CheckPlayerProximity()
    {
        while (isAvailableForUpgrade && !isUpgraded && !isUpgradeInProgress)
        {
            if (_player != null)
            {
                float distance = Vector3.Distance(_player.transform.position, transform.position);
                if (distance <= goldCollectionRange)
                {
                    // 플레이어 골드를 가지고 있고 아직 업그레이드가 완료되지 않은 경우
                    if (GameManager.Instance.GetCurrentGold() > 0 && currentPaidGold < upgradeCost)
                    {
                        SendGoldToMainCenter();
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void SendGoldToMainCenter()
    {
        if (GameManager.Instance.SpendGold(1))
        {
            // 플레이어로부터 골드를 시각적으로 보내는 메서드 호출
            _player.OnSendGoldToTurret(transform); // 기존 메서드 재사용

            currentPaidGold++;
            UpdateGoldDisplay();
#if !PLAYABLE_AD
            Debug.Log($"MainCenter received gold: {currentPaidGold}/{upgradeCost}");
#endif


            // 업그레이드 비용이 모두 지불되면 업그레이드 시작
            if (currentPaidGold >= upgradeCost)
            {
                StartUpgrade();
            }
        }
    }

    public void StartUpgrade()
    {
        if (isUpgraded || isUpgradeInProgress) return;

        isUpgradeInProgress = true;
#if !PLAYABLE_AD
        Debug.Log("MainCenter upgrade started!");
#endif


        StartCoroutine(UpgradeAnimation());
    }

    IEnumerator UpgradeAnimation()
    {
        // 1. 기존 건물이 내려가는 애니메이션
        yield return StartCoroutine(BuildingDownAnimation());

        // 2. 모델 교체
        if (preUpgradeModel != null)
            preUpgradeModel.SetActive(false);

        if (postUpgradeModel != null)
            postUpgradeModel.SetActive(true);

        // 3. 새 건물이 올라오는 애니메이션
        yield return StartCoroutine(BuildingUpAnimation());

        // 4. 업그레이드 완료 처리
        CompleteUpgrade();
    }

    IEnumerator BuildingDownAnimation()
    {
        if (centerOBJ == null) yield break;

        Vector3 startPosition = originalCenterPosition;
        Vector3 endPosition = originalCenterPosition + Vector3.down * 3f; // 3유닛 아래로

        float elapsedTime = 0f;
        while (elapsedTime < downAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / downAnimationTime;
            float curveValue = buildCurve.Evaluate(t);

            centerOBJ.transform.localPosition = Vector3.Lerp(startPosition, endPosition, curveValue);
            yield return null;
        }

        centerOBJ.transform.localPosition = endPosition;
    }

    IEnumerator BuildingUpAnimation()
    {
        if (centerOBJ == null) yield break;

        Vector3 startPosition = originalCenterPosition + Vector3.down * 3f; // 아래에서 시작
        Vector3 endPosition = originalCenterPosition;

        float elapsedTime = 0f;
        while (elapsedTime < upAnimationTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / upAnimationTime;
            float curveValue = buildCurve.Evaluate(t);

            centerOBJ.transform.localPosition = Vector3.Lerp(startPosition, endPosition, curveValue);
            yield return null;
        }

        centerOBJ.transform.localPosition = endPosition;
    }

    void CompleteUpgrade()
    {
        SetUpgradeState(true);
        isUpgradeInProgress = false;

        // UI 요소들 숨김
        if (outLine != null) outLine.SetActive(false);
        if (coin != null) coin.SetActive(false);

        // 오디오 재생
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayArrowAttackSound(); // 업그레이드 완료 사운드
        }

        // GameManager에게 업그레이드 완료 알림 (벽 업그레이드 트리거)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnMainCenterUpgraded();
        }
#if !PLAYABLE_AD
        Debug.Log("MainCenter upgrade completed!");
#endif

    }
#if !PLAYABLE_AD
    // 강제 업그레이드 (치트/테스트용)
    [ContextMenu("Force Upgrade")]
    public void ForceUpgrade()
    {
        if (Application.isPlaying && !isUpgraded)
        {
            currentPaidGold = upgradeCost;
            StartUpgrade();
        }
    }

    // 업그레이드 상태 리셋 (테스트용)
    [ContextMenu("Reset Upgrade")]
    public void ResetUpgrade()
    {
        if (Application.isPlaying)
        {
            isUpgraded = false;
            isUpgradeInProgress = false;
            currentPaidGold = 0;

            if (preUpgradeModel != null)
                preUpgradeModel.SetActive(true);

            if (postUpgradeModel != null)
                postUpgradeModel.SetActive(false);

            if (centerOBJ != null)
                centerOBJ.transform.localPosition = originalCenterPosition;

            SetUpgradeState(false);
        }
    }

    // 테스트용 메서드 - 업그레이드 가능 상태로 강제 설정
    [ContextMenu("Enable Upgrade")]
    public void EnableUpgrade()
    {
        if (Application.isPlaying)
        {
            SetAvailableForUpgrade(true);
        }
    }
#endif
    // 공개 API
    public bool IsUpgraded() => isUpgraded;
#if !PLAYABLE_AD
    public bool IsUpgradeInProgress() => isUpgradeInProgress;
    public bool IsAvailableForUpgrade() => isAvailableForUpgrade;
    public int GetPaidGold() => currentPaidGold;
    public int GetUpgradeCost() => upgradeCost;
    public float GetUpgradeProgress() => (float)currentPaidGold / upgradeCost;

    // 설정 변경 메서드들
    public void SetUpgradeCost(int cost)
    {
        upgradeCost = Mathf.Max(1, cost);
        UpdateGoldDisplay();
    }

    public void SetTextColor(Color color)
    {
        textColor = color;
        if (txtRemainGold != null)
            txtRemainGold.color = textColor;
    }

    public void SetTextSize(float size)
    {
        textSize = size;
        if (txtRemainGold != null)
            txtRemainGold.fontSize = textSize;
    }

    public void SetGoldCollectionRange(float range)
    {
        goldCollectionRange = Mathf.Max(1f, range);
    }
#endif
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
        if (buildingRenderers == null || centerOBJ == null) yield break;

        isFlashingDamage = true;

        // 1. 색상 화이트 플래시
        for (int i = 0; i < buildingRenderers.Length; i++)
        {
            if (buildingRenderers[i] != null && buildingRenderers[i].material != null)
            {
                buildingRenderers[i].material.color = Color.red;
            }
        }

        // 2. 크기 증가
        Vector3 scaledSize = originalBuildingScale * damageScaleMultiplier;
        centerOBJ.transform.localScale = scaledSize;

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

            centerOBJ.transform.localScale = Vector3.Lerp(scaledSize, originalBuildingScale, progress);
            yield return null;
        }

        // 6. 정확한 원본 크기로 복귀
        centerOBJ.transform.localScale = originalBuildingScale;
        isFlashingDamage = false;
    }

#region IHealth Implementation
    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return isUpgraded ? upgradedMaxHealth : maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead()) return;

        // 안전한 데미지 계산 (오버플로우 방지)
        damage = Mathf.Clamp(damage, 0, 999999);
        int newHealth = currentHealth - damage;
        currentHealth = Mathf.Max(0, newHealth >= 0 ? newHealth : 0);

        OnHealthChanged(currentHealth, GetMaxHealth());
        UpdateHealthBar();
        StartDamageFlashEffect();
#if !PLAYABLE_AD
        Debug.Log($"MainCenter took {damage} damage. Health: {currentHealth}/{GetMaxHealth()}");
#endif


        if (currentHealth <= 0)
        {
            OnDeath();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead()) return;

        // 안전한 힐링 계산
        amount = Mathf.Max(0, amount);
        int maxHP = GetMaxHealth();
        currentHealth = Mathf.Min(maxHP, currentHealth + amount);

        OnHealthChanged(currentHealth, maxHP);
        UpdateHealthBar();
#if !PLAYABLE_AD
        Debug.Log($"MainCenter healed {amount}. Health: {currentHealth}/{maxHP}");
#endif

    }

    public bool IsDead()
    {
        return currentHealth <= 0;
    }

    public void OnHealthChanged(int currentHealth, int maxHealth)
    {
#if !PLAYABLE_AD
        // 체력 변화 시 호출되는 이벤트
        // UI 업데이트나 다른 시스템에 알림을 보낼 수 있음
        Debug.Log($"MainCenter Health changed: {currentHealth}/{maxHealth} ({GetHealthRatio():F2})");
#endif

    }

    public void OnDeath()
    {
#if !PLAYABLE_AD
        // 메인센터가 파괴되었을 때의 처리
        Debug.Log("MainCenter has been destroyed!");

        // 추가 사망 처리 로직은 여기에 넣을 수 있음
        // 예: 게임 오버, 파티클 이펙트, 사운드 등
        // 이 부분은 사용자가 알아서 처리한다고 하셨으므로 기본적인 로그만 출력
#endif

    }

    public float GetHealthRatio()
    {
        int maxHP = GetMaxHealth();
        return maxHP > 0 ? (float)currentHealth / maxHP : 0f;
    }
    public bool CanTargetable()
    {
        return targetable;
    }
    #endregion
#if !PLAYABLE_AD
    #region Gizmos
    void OnDrawGizmosSelected()
    {
        // 골드 수집 범위 표시 (업그레이드 가능 상태일 때만)
        if (isAvailableForUpgrade)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, goldCollectionRange);
        }
        else
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, goldCollectionRange);
        }

        // 원본 위치 표시
        if (centerOBJ != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + originalCenterPosition, Vector3.one * 0.5f);
        }

        // HP 바 위치 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + hpBarOffset, new Vector3(hpBarSize.x, hpBarSize.y, 0.1f));
    }
    #endregion
#endif
}