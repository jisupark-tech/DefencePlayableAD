using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public enum GimmicKType
{
    None,
    Arrow,
    NPC,
    Pet,
}

public class EnhanceController : MonoBehaviour, IHealth , ICollectable
{
    [Header("Enhance Settings")]
    public int enhanceCost = 20;
    public int currPaidCost = 0;
    public float ditectDistance = 2f;
    public GimmicKType m_type = GimmicKType.None;
    [Header("Can Targetable")]
    public bool targetable = true;
    [Header("Health Settings")]
    public int maxHealth = 80;
    private int currentHealth;

    [Header("Enhance Parts")]
    public GameObject enhanceOBJ; // Mine_OBJ
    public GameObject outLine; // OutLine
    public GameObject coin; // Coin
    public TextMeshPro TxtGold;
    public GameObject BuildingSlider;
    public GameObject QuestSlider;
    private float oriSliderSize = 1.7f;

    [Header("Visual Effects")]
    [SerializeField] private float damageFlashDuration = 0.15f;
    [SerializeField] private float damageScaleDuration = 0.2f;
    [SerializeField] private float damageScaleMultiplier = 1.1f;
    [Header("Animation")]
    [SerializeField] private float AnimationDuration = 1f;
    private bool isBuilt = false;
    private bool isVisible = true;

    [Header("Quest")]
    [SerializeField] private GameObject QuestContent;
    [SerializeField] private Transform m_spawnPos;
    [SerializeField] private TextMeshPro TxtQuestGold;
    [SerializeField] private TextMeshPro TxtMaxQuestCnt;
    [SerializeField] private TextMeshPro TxtCurQuestCnt;
    [SerializeField] private int MaxQuestCnt;
    private int CurQuestCnt;
    [SerializeField] private int QuestPrice;
    private int CurQuestPaid;
    private bool isWaitForSpawning = false;
    [SerializeField] private float m_interverSpawnTime = 5f;
    private Renderer[] buildingRenderers;
    private Color[] originalColors;
    private Vector3 originalScale;
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
    private PlayerController _player;
    public void Init()
    {
        currentHealth = maxHealth;

        _player = GameManager.Instance.m_Player;

        InitializeDamageEffect();
        InitializeSlider();
        SetBuildState(false);

        if (isVisible && this.gameObject.activeInHierarchy)
        {
            StartCoroutine(CheckAndBuildBehavior());
        }
    }

    void InitializeDamageEffect()
    {

        if (enhanceOBJ != null)
        {
            buildingRenderers = enhanceOBJ.GetComponentsInChildren<Renderer>();
            originalColors = new Color[buildingRenderers.Length];

            for (int i = 0; i < buildingRenderers.Length; i++)
            {
                if (buildingRenderers[i].material != null)
                {
                    originalColors[i] = buildingRenderers[i].material.color;
                }
            }

            originalScale = enhanceOBJ.transform.localScale;
        }
    }
    void InitializeSlider()
    {
        if (BuildingSlider != null)
        {
            BuildingSlider.transform.localScale = new Vector3(0, oriSliderSize, 0);
        }

        if(QuestSlider != null)
        {
            QuestSlider.transform.localScale = new Vector3(0, oriSliderSize, 0);
        }
    }
    void SetBuildState(bool built)
    {
        isBuilt = built;

        if (enhanceOBJ != null)
            enhanceOBJ.SetActive(built);

        if (outLine != null)
            outLine.SetActive(!built && isVisible); 

        if (coin != null)
            coin.SetActive(!built && isVisible);

        if (QuestContent != null)
            QuestContent.SetActive(built);

        if (QuestContent != null && QuestContent.activeInHierarchy)
        {
            if (TxtMaxQuestCnt != null)
                TxtMaxQuestCnt.text = MaxQuestCnt.ToString();
            if (TxtCurQuestCnt != null)
                TxtCurQuestCnt.text = CurQuestCnt.ToString();
            if (TxtQuestGold != null)
                TxtQuestGold.text = CurQuestPaid.ToString();
        }

        if (built)
        {
            StartCoroutine(EnhanceRiseAnimation());
        }
    }

    public void SetVisibility(bool visible)
    {
        isVisible = visible;

        if (visible)
        {
            gameObject.SetActive(true);
            SetBuildState(isBuilt); 

            if (!isBuilt)
            {
                StartCoroutine(CheckAndBuildBehavior());
            }
#if !PLAYABLE_AD
            Debug.Log($"Enhance {gameObject.name} is now visible and available for construction");
#endif
        }
        else
        {
            if (outLine != null) outLine.SetActive(false);
            if (coin != null) coin.SetActive(false);

            if (!isBuilt)
            {
                gameObject.SetActive(false);
            }
#if !PLAYABLE_AD
            Debug.Log($"Enhance {gameObject.name} is now hidden");
#endif
        }
    }

    IEnumerator EnhanceRiseAnimation()
    {
        if (enhanceOBJ == null) yield break;

        Vector3 originalenhnaceScale = enhanceOBJ.transform.localScale;

        enhanceOBJ.transform.localScale = Vector3.zero;

        yield return new WaitForSeconds(0.5f);

        EffectController _effect = ObjectPool.Instance.SpawnFromPool("Effect", this.transform.position, Quaternion.identity, ObjectPool.Instance.transform).GetComponent<EffectController>();
        if (_effect)
            _effect.Init(EffectType.Building, this.transform.position.x, this.transform.position.z, enhanceOBJ.transform.localRotation.y,0.3f);

        if (float.IsNaN(originalenhnaceScale.x) || float.IsNaN(originalenhnaceScale.y) || float.IsNaN(originalenhnaceScale.z))
        {
            originalenhnaceScale = Vector3.one;
        }



        enhanceOBJ.transform.localScale = originalenhnaceScale * 0.3f;

        float _animationDuration = AnimationDuration; 
        float elapsedTime = 0f;

        while (elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / _animationDuration); 

            float scaleMultiplier = 1.0f;

            if (progress <= 0.5f)
            {
                float t = progress / 0.5f;
                t = Mathf.Clamp01(t);
                scaleMultiplier = Mathf.Lerp(0.3f, 1.15f, t);
            }
            else if (progress <= 0.8f)
            {
                float t = (progress - 0.5f) / 0.3f;
                t = Mathf.Clamp01(t);
                scaleMultiplier = Mathf.Lerp(1.15f, 0.9f, t);
            }
            else
            {
                float t = (progress - 0.8f) / 0.2f;
                t = Mathf.Clamp01(t);
                scaleMultiplier = Mathf.Lerp(0.9f, 1.0f, t);
            }

            scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.1f, 2.0f);
            Vector3 currentEnhanceScale = originalenhnaceScale * scaleMultiplier;


            if (!float.IsNaN(currentEnhanceScale.x) && !float.IsNaN(currentEnhanceScale.y) && !float.IsNaN(currentEnhanceScale.z))
            {
                enhanceOBJ.transform.localScale = currentEnhanceScale;
            }

            yield return null;
        }

        if (!float.IsNaN(originalenhnaceScale.x) && !float.IsNaN(originalenhnaceScale.y) && !float.IsNaN(originalenhnaceScale.z))
        {
            enhanceOBJ.transform.localScale = originalenhnaceScale;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnEnhanceBuilt(this);
            //TODO 여기서 UI 수정 
            StartCoroutine(CheckAndQuestBehavior());
        }
    }
    IEnumerator CheckAndQuestBehavior()
    {
        while (isBuilt && isVisible)
        {
            if (_player != null)
            {
                if(!isWaitForSpawning)
                {
                    if (CurQuestCnt <= MaxQuestCnt)
                    {
                        float distance = Vector3.Distance(_player.transform.position, transform.position);
                        if (distance <= ditectDistance)
                        {
                            if (GameManager.Instance.GetCurrentGold() > 0 && CurQuestPaid <QuestPrice)
                            {
                                SendGoldToQuest();
                            }
                        } 
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    void SendGoldToQuest()
    {
        if (GameManager.Instance.SpendGold(1))
        {
            _player.OnSendGoldToTurret(QuestContent.transform);

            CurQuestPaid++;

            if (TxtQuestGold != null)
            {
                int _remainGold = QuestPrice - CurQuestPaid;
                TxtQuestGold.text = $"{_remainGold}";
            }
            if (TxtMaxQuestCnt != null)
                TxtMaxQuestCnt.text = MaxQuestCnt.ToString();
            if (TxtCurQuestCnt != null)
                TxtCurQuestCnt.text = CurQuestCnt.ToString();
            
            float progress = (float)CurQuestPaid / QuestPrice;
            //TODO 수정 2025-12-10
            //완료시 슬라이더 0으로 
            if (CurQuestCnt == MaxQuestCnt)
                progress = 0;

            UpdateQuestSlider(progress);

            if (CurQuestPaid >= QuestPrice)
            {
                SpawnNPC();
            }
        }
    }
   
    IEnumerator CheckAndBuildBehavior()
    {
        while (!isBuilt && isVisible) 
        {
            if (_player != null)
            {
                float distance = Vector3.Distance(_player.transform.position, transform.position);
                if (distance <= ditectDistance) 
                {
                    if (GameManager.Instance.GetCurrentGold() > 0 && currPaidCost < enhanceCost)
                    {
                        SendGoldToEnhance();
                    }
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
    void SendGoldToEnhance()
    {
        if (GameManager.Instance.SpendGold(1))
        {
            _player.OnSendGoldToTurret(transform);  
        }
    }
    public void CollectGold()
    {
        currPaidCost++;
        if (!isBuilt && isVisible)
        {
            int remainingCost = enhanceCost - currPaidCost;
            if (remainingCost > 0)
            {
                if (TxtGold != null)
                {
                    int _remainGold = enhanceCost - currPaidCost;
                    TxtGold.text = $"{_remainGold}";
                }

                // 건설 진행도에 따른 슬라이더 업데이트
                float progress = (float)currPaidCost / enhanceCost;
                UpdateGoldSlider(progress);
            }
        }
#if !PLAYABLE_AD
        Debug.Log($"enhanceCost received gold: {currPaidCost}/{enhanceCost}");
#endif

        if (currPaidCost == enhanceCost)
        {
            BuildEnhance();
        }
    }
    void UpdateGoldSlider(float progress)
    {
        if (BuildingSlider == null) return;

        // 진행도에 따른 Y축 스케일 조정
        progress = Mathf.Clamp01(progress);
        float targetScaleX = oriSliderSize * progress;

        Vector3 currentScale = BuildingSlider.transform.localScale;
        Vector3 targetScale = new Vector3(targetScaleX, currentScale.y, currentScale.z);

        BuildingSlider.transform.localScale = targetScale;

        // 슬라이더가 보이도록 활성화
        if (!BuildingSlider.activeInHierarchy && progress > 0 && !isBuilt)
        {
            BuildingSlider.SetActive(true);
        }
    }
    void UpdateQuestSlider(float progress)
    {
        if (QuestSlider == null) return;

        // 진행도에 따른 Y축 스케일 조정
        progress = Mathf.Clamp01(progress);
        float targetScaleX = oriSliderSize * progress;
        Vector3 currentScale = QuestSlider.transform.localScale;
        Vector3 targetScale = new Vector3(targetScaleX, currentScale.y, currentScale.z);

        QuestSlider.transform.localScale = targetScale;

        // 슬라이더가 보이도록 활성화
        if (!QuestSlider.activeInHierarchy && progress > 0)
        {
            QuestSlider.SetActive(true);
        }
    }
    void BuildEnhance()
    {
#if !PLAYABLE_AD
        Debug.Log($"Enhance {gameObject.name} construction completed!");
#endif
        SetBuildState(true);
    }
    void SpawnNPC()
    {
        Vector3 _spawnPos = m_spawnPos != null ? m_spawnPos.position : this.transform.position;
        NPCController _npc = ObjectPool.Instance.SpawnFromPool("NPC", _spawnPos, Quaternion.identity).GetComponent<NPCController>();
        if (_npc != null)
        {
            //TODO TEST

            // 업그레이드 가능한 터렛들을 인덱스 순서대로 가져오기
            List<TurretController> upgradeableTurrets = GameManager.Instance.GetUpgradeableTurrets();

            if (upgradeableTurrets.Count > 0)
            {
                // 첫 번째 업그레이드 가능한 터렛을 타겟으로 설정
                TurretController targetTurret = upgradeableTurrets[0];
                _npc.Init(targetTurret.transform);
#if !PLAYABLE_AD
                Debug.Log($"NPC spawned and targeting turret: {targetTurret.name} for upgrade");
#endif
            }
            else
            {
#if !PLAYABLE_AD
                // 업그레이드 가능한 터렛이 없으면 NPC를 다시 풀에 반환
                Debug.LogWarning("No upgradeable turrets available. NPC returned to pool.");
#endif
                ObjectPool.Instance.ReturnToPool(_npc.gameObject);
                return;
            }

            CurQuestPaid = 0;
            CurQuestCnt++;
            StartCoroutine(WaitInterverSpawnTime());
        }
    }
    IEnumerator WaitInterverSpawnTime()
    {
        float _curTime = 0f;
        isWaitForSpawning = true;
        while (_curTime <= m_interverSpawnTime)
        {
            _curTime += Time.deltaTime;
            yield return null;
        }

        isWaitForSpawning = false;
    }
    public bool IsBuilt() => isBuilt;

    void StartDamageFlashEffect()
    {
        if (!isFlashingDamage)
        {
            StartCoroutine(DamageFlashEffect());
        }
    }
    IEnumerator DamageFlashEffect()
    {
        if (buildingRenderers == null || enhanceOBJ == null) yield break;

        isFlashingDamage = true;

        for (int i = 0; i < buildingRenderers.Length; i++)
        {
            if (buildingRenderers[i] != null && buildingRenderers[i].material != null)
            {
                buildingRenderers[i].material.color = Color.red;
            }
        }

        Vector3 scaledSize = originalScale * damageScaleMultiplier;
        enhanceOBJ.transform.localScale = scaledSize;

        yield return new WaitForSeconds(damageFlashDuration);
        for (int i = 0; i < buildingRenderers.Length; i++)
        {
            if (buildingRenderers[i] != null && buildingRenderers[i].material != null)
            {
                buildingRenderers[i].material.color = originalColors[i];
            }
        }
        float elapsedTime = 0f;
        while (elapsedTime < damageScaleDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / damageScaleDuration;

            progress = 1f - Mathf.Pow(1f - progress, 2f);

            enhanceOBJ.transform.localScale = Vector3.Lerp(scaledSize, originalScale, progress);
            yield return null;
        }

        enhanceOBJ.transform.localScale = originalScale;
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
        //TODO 2025-12-03
        //아마 다음 빌드던 다음 버전에서는 체력이 줄어들거나 파괴되는 걸 넣어야하기 때문에 일단 기능은 살려두기 
        if (hpFillRenderer == null) return;

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
        DestroyEnhance();
    }

    void DestroyEnhance()
    {

        gameObject.SetActive(false);
        //TODO 파괴 되고 나면 어떻게?
        //지금 빌드에서는 파괴 고민 X 
    }
    #endregion
    public int GetRemainPaidGold()
    {
        return enhanceCost - currPaidCost;
    }
    public bool CanTargetable()
    {
        return targetable;
    }
}