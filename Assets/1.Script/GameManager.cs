using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Paths
{
    public Transform SpawnPos;
    public Transform[] Roots;
}
[System.Serializable]
public class BuildingPhase
{
    [Tooltip("단계 이름 (예: '초기 방어', '고급 방어')")]
    public string phaseName;

    [Tooltip("m_Turrets 리스트의 인덱스들 (예: 0,1,2 → Turret_01, Turret_02, Turret_03)")]
    public List<int> turretIndices = new List<int>();

    [Tooltip("m_Mines 리스트의 인덱스들 (예: 0,1 → Mine_01, Mine_02)")]
    public List<int> mineIndices = new List<int>();

    [Tooltip("m_walls 리스트의 인덱스들 (예: 0,2,3 → Wall_01, Wall_03, Wall_04)")]
    public List<int> wallIndices = new List<int>();

    [Tooltip("m_Enhance 리스트의 인덱스들 (예: 0,1 → Enhanc_01, Enhanc_02)")]
    public List<int> enhances = new List<int>();
    [Tooltip("이 단계에서 건설해야 할 최소 건물 수 (나머지는 선택사항)")]
    public int requiredCompletions = 1;

    [Tooltip("true: 벽의 기존 requiredTurrets 조건 무시, false: 기존 조건 유지")]
    public bool ignoreWallRequirements = false;
}

[System.Serializable]
public class BuildingRule
{
    [Tooltip("규칙 이름 (예: '메인센터 업그레이드 시 기본 터렛 제한')")]
    public string ruleName;

    [Tooltip("이 건물들이 조건을 만족하면 규칙 발동")]
    public List<GameObject> triggerBuildings = new List<GameObject>();

    [Tooltip("true: 모든 건물 완성 필요, false: 하나만 완성되면 발동")]
    public bool requireAllBuildings = true;

    [Tooltip("비활성화할 터렛들의 m_Turrets 인덱스")]
    public List<int> disableTurretIndices = new List<int>();

    [Tooltip("비활성화할 광산들의 m_Mines 인덱스")]
    public List<int> disableMineIndices = new List<int>();

    [Tooltip("비활성화할 강화건물들의 m_Enhance 인덱스")]
    public List<int> disableEnhanceIndices = new List<int>();

    [Tooltip("비활성화할 벽들의 m_walls 인덱스")]
    public List<int> disableWallIndices = new List<int>();
}

[System.Serializable]
public class SpawnMonsterRuleByBuilding
{
    public List<MonsterTypePerCntPack> LinePerMonsterCnt = new List<MonsterTypePerCntPack>();
}

[System.Serializable]
public class MonsterTypePerCntPack
{
    [Tooltip("몬스터를 소환할 장소 번호,GameManager 상단에 있는 Roots에 들어있는 경로 Index 번호(현재 0~5까지 있음)")]
    public int RootIdx;
    [Tooltip("해당 트리거에 소환될 일반 몬스터 숫자")]
    public int NormalMonsterCnt;
    [Tooltip("해당 트리거에 소환될 보스 몬스터 숫자")]
    public int BossMonsterCnt;
}

public enum EnemySpawnWay
{
    Road,
    Seperate,
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;


    [Header("TEST")]
    public bool M_TEST;


    [Header("Game Settings")]
    public float gameTime = 30f;
    public int goldStartAmount = 0;
    public int killTarget = 10;
    public int FirstSpawnCnt = 7;
    public float FirstSpawnSpread = 3f;
    private List<EnemyController> m_firstEnemies = new List<EnemyController>();
    [Header("UI References")]
    public Text goldText;
    public GameObject winUI;

    public string downloadUrl = "https://play.google.com/store/apps/details?id=com.Eclipse.Neuphoria";

    [Header("Virtual Pad")]
    public VirtualPad m_VirtualPad;

    [Header("Enemy Spawning")]
    public EnemySpawnWay m_spawnWay = EnemySpawnWay.Road;
    public int m_PerSpawnEnemyCnt = 5;
    public List<Paths> m_Roots = new List<Paths>();
    [Tooltip("Spawn Way 가 Load일때 1.2f, Seperate 일떄 4.5f")]
    public float enemySpawnInterval = 2f;
    public float m_enemySpreadVal = 4.5f;
    [Header("Enemy Lane Activation")]
    public int baseActiveRoots = 1;
    private int activeRootCount = 1;

    [Header("Player")]
    public PlayerController m_Player;

    [Header("Main Center Building")]
    public MainCenterController m_MainCenter;

    [Header("Turret")]
    public List<TurretController> m_Turrets = new List<TurretController>();

    [Header("Mine")]
    public List<MineController> m_Mines = new List<MineController>();

    [Header("Wall")]
    public List<WallController> m_walls = new List<WallController>();

    [Header("Enhance")]
    public List<EnhanceController> m_Enhances = new List<EnhanceController>();

    [Header("Sequential Building")]
    [SerializeField] private int currentAvailableTurretIndex = 0;
    [SerializeField] private int currentAvailableMineIndex = 0;
    [SerializeField] private int currentAvailableEnhanceIndex = 0;
    public bool enableSequentialBuilding = true;

    [Header("Phase Building Configuration")]
    public List<BuildingPhase> buildingPhases = new List<BuildingPhase>();
    public List<BuildingRule> buildingRules = new List<BuildingRule>();

    //TODO 2025-12-09
    [Header("Spawn Monster Trigger Event")]
    private int MonsterSpawnEventIdx = 0;
    //해당 리스트의 인덱스는 포탑의 총갯수와 같아야 함
    public  List<SpawnMonsterRuleByBuilding> MonsterSpawnTriggerRules = new List<SpawnMonsterRuleByBuilding>();

    // Phase Building System 내부 변수들
    private int currentPhaseIndex = 0;
    private int completedBuildingsInPhase = 0;
    private HashSet<int> disabledTurretIndices = new HashSet<int>();
    private HashSet<int> disabledMineIndices = new HashSet<int>();
    private HashSet<int> disabledWallIndices = new HashSet<int>();
    private HashSet<int> disabledEnhances = new HashSet<int>();
    #region First Touch Guide
    [Header("INIT")]
    [SerializeField] private bool m_IsTouched = false;

    [Header("GUIDE UI")]
    [SerializeField] private GameObject UiGuide;
    #endregion
    [Header("Guide Line")]
    public bool enableGuideLine = true;
    private GuideController _currentGuide;

    [Header("Building Collision Settings")]
    [SerializeField] private float buildingCollisionRadius = 1.5f; // 건물 충돌 반경
    [SerializeField] private bool enableBuildingCollision = true; // 건물 충돌 활성화

    [Header("Enemy Scaling Settings")]
    [SerializeField] private float enemyHealthMultiplier = 1.2f; // 페이즈당 체력 증가율
    [SerializeField] private float enemySpeedMultiplier = 1.1f;  // 페이즈당 속도 증가율
    [Tooltip("Spawn Way 가 Load일때 0.5f, Seperate 일떄 0")]
    [SerializeField] private int enemyCountIncrease = 1;         // 페이즈당 적 수 증가
    [SerializeField] private float spawnIntervalDecrease = 0.1f; // 페이즈당 스폰 간격 감소
    [SerializeField] private bool bossModeEnabled = false;       // 보스 모드 활성화 여부
    [Range(0, 1)]
    [SerializeField] private float bossPercent = 0;

    [Header("Boss Setting")]
    [Tooltip("Boss 몬스터 체력 세팅")]
    [SerializeField] private float BossHpMultiple = 8.5f;
    // Game State
    private int currentGold;
    private float currentTime;
    private int enemyKilled = 0;
    private bool gameEnded = false;

    // New Input System용 첫 클릭 액션
    //private InputAction _firstClickAction;

    // 적 강화 통계
    private int baseEnemyHealth = 1;
    private float baseEnemySpeed = 3f;
    private float baseSpawnInterval = 2f;

    //=====================================================
    // Lifecycle
    //=====================================================
    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Start()
    {
        ObjectPool.Instance.Init();

        currentGold = goldStartAmount;
        currentTime = gameTime;

        // 적 기본 스탯 설정
        SetBaseEnemyStats(1, 3f, enemySpawnInterval);

        if (m_Player)
            m_Player.Init();

        if (m_MainCenter != null)
        {
            m_MainCenter.Init();
#if !PLAYABLE_AD
            Debug.Log("MainCenter initialized and set as attack target");
#endif

        }
        else
        {
#if !PLAYABLE_AD
            Debug.LogWarning("MainCenter not assigned in GameManager!");
#endif

        }

        if (m_Roots != null && m_Roots.Count > 0)
            activeRootCount = Mathf.Clamp(baseActiveRoots, 1, m_Roots.Count);
        else
            activeRootCount = 0;

        if (buildingPhases.Count > 0)
        {
            InitializePhaseBuilding();
        }
        else
        {
            InitializeSequentialBuilding();
        }

        RefreshUI();

        m_IsTouched = false;

        if (UiGuide != null)
            UiGuide.SetActive(true);



        for (int i = 0; i < FirstSpawnCnt; i++)
        {
            Vector2 _randomOffset = Random.insideUnitCircle * FirstSpawnSpread;

            // 3D 위치 (Vector3)로 변환 (y값은 고정)
            //TODO 거리
            //상수로 박아놓은것은 플레이어와의 거리값
            Vector3 _randomPos = m_Player.transform.position + new Vector3(_randomOffset.x, 0, _randomOffset.y - 8);
            GameObject _enemy = ObjectPool.Instance.SpawnFromPool("Enemy", _randomPos, Quaternion.identity);
            m_firstEnemies.Add(_enemy.GetComponent<EnemyController>());
        }

        UpdateGuideLine();
    }

    void Update()
    {
        if (M_TEST)
            return;

        if (!m_IsTouched && !gameEnded)
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                StartGameplay();
            }
        }


        if (!m_IsTouched)
            return;

        if (!gameEnded && enemyKilled >= killTarget)
        {
            EndGame(true);
        }
    }


    void StartGameplay()
    {
        if (m_IsTouched)
            return;

        m_IsTouched = true;

        if (UiGuide != null)
            UiGuide.SetActive(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.EnableAudio();

        for (int i = 0; i < m_firstEnemies.Count; i++)
        {
            if (m_firstEnemies[i] != null)
            {
                EnemyController _enemyController = m_firstEnemies[i];
                _enemyController.Initialize(m_MainCenter.transform);

                // Phase에 맞는 스탯 적용
                int phaseHealth = GetEnemyHealthForCurrentPhase();
                float phaseSpeed = GetEnemySpeedForCurrentPhase();

                _enemyController.SetStatsForPhase(phaseHealth, phaseSpeed);
            }
        }


        StartCoroutine(CheckAllBuildingBuilt());
        StartCoroutine(SpawnEnemies());
    }

    //=====================================================
    // Timer & Enemy Spawning
    //=====================================================
    IEnumerator CheckAllBuildingBuilt()
    {
        while (!gameEnded)
        {
            bool _completed = true;
            int _builtTurretCnt = 0;
            int _builtMineCnt = 0;
            int _builtEnhanceCnt = 0;
            foreach (TurretController _turret in m_Turrets)
            {
                if (_turret.IsBuilt())
                {
                    _builtTurretCnt++;
                }

                if (_turret.IsBuilt() == false)
                {
                    _completed = false;
                    break;
                }

            }

            foreach (MineController _mine in m_Mines)
            {
                if (_mine.IsBuilt())
                {
                    _builtMineCnt++;
                }

                if (_mine.IsBuilt() == false)
                {
                    _completed = false;
                    break;
                }

            }

            foreach (EnhanceController _enhance in m_Enhances)
            {
                if (_enhance.IsBuilt())
                {
                    _builtEnhanceCnt++;
                }

                if (_enhance.IsBuilt() == false)
                {
                    _completed = false;
                    break;
                }

            }

            yield return new WaitForSeconds(0.01f);

            if (_completed)
            {
                EndGame(true);
            }

        }


    }

    IEnumerator SpawnEnemies()
    {
        while (!gameEnded)
        {
            yield return new WaitForSeconds(enemySpawnInterval);
            switch (m_spawnWay)
            {
                case EnemySpawnWay.Road:
                    if (m_Roots.Count > 0)
                    {
                        int maxRoot = Mathf.Clamp(activeRootCount, 1, m_Roots.Count);

                        for (int i = 0; i < maxRoot; i++)
                        {
                            if (m_Roots[i].Roots.Length > 0)
                            {
                                Transform spawnPoint = m_Roots[i].SpawnPos;

                                // Phase 1 클리어 후에는 Boss도 스폰 가능
                                string enemyType = "Enemy";
                                if (bossModeEnabled && Random.Range(0f, 1f) < bossPercent) // 30% 확률로 보스
                                {
                                    enemyType = "Boss";
                                }

                                GameObject enemy = ObjectPool.Instance
                                    .SpawnFromPool(enemyType, spawnPoint.position, spawnPoint.rotation);

                                if (enemy != null)
                                {
                                    EnemyController enemyController = enemy.GetComponent<EnemyController>();
                                    enemyController.Initialize(m_Roots[i].Roots);

                                    // Phase에 맞는 스탯 적용
                                    int phaseHealth = GetEnemyHealthForCurrentPhase();
                                    float phaseSpeed = GetEnemySpeedForCurrentPhase();

                                    // 보스인 경우 스탯을 더 강화
                                    if (enemyType == "Boss")
                                    {
                                        phaseHealth = Mathf.RoundToInt(phaseHealth * BossHpMultiple); // 보스는 2.5배 체력
                                        phaseSpeed *= 0.8f; // 보스는 80% 속도
                                    }

                                    enemyController.SetStatsForPhase(phaseHealth, phaseSpeed);
                                }
                            }
                        }
                    }
                    break;
                case EnemySpawnWay.Seperate:
                    if (m_Roots.Count > 0)
                    {
                        int maxRoot = Mathf.Clamp(activeRootCount, 1, m_Roots.Count);

                        for (int i = 0; i < maxRoot; i++)
                        {
                            for (int j = 0; j < m_PerSpawnEnemyCnt; j++)
                            {
                                Transform _spawnPos = m_Roots[i].SpawnPos;
                                Vector2 _randomOffset = Random.insideUnitCircle * m_enemySpreadVal;

                                // 3D 위치 (Vector3)로 변환 (y값은 고정)
                                Vector3 _randomPos = _spawnPos.position + new Vector3(_randomOffset.x, 0, _randomOffset.y);

                                // Phase 1 클리어 후에는 Boss도 스폰 가능
                                string enemyType = "Enemy";
                                if (bossModeEnabled && Random.Range(0f, 1f) < bossPercent) // 30% 확률로 보스
                                {
                                    enemyType = "Boss";
                                }

                                GameObject _enemy = ObjectPool.Instance.SpawnFromPool(enemyType, _randomPos, _spawnPos.rotation);

                                if (_enemy != null)
                                {
                                    EnemyController _enemyController = _enemy.GetComponent<EnemyController>();
                                    //_enemyController.Initialize(m_Roots[i].Roots[m_Roots[i].Roots.Length - 1]);
                                    _enemyController.Initialize(m_MainCenter.transform);
                                    // Phase에 맞는 스탯 적용
                                    int phaseHealth = GetEnemyHealthForCurrentPhase();
                                    float phaseSpeed = GetEnemySpeedForCurrentPhase();

                                    // 보스인 경우 스탯을 더 강화
                                    if (enemyType == "Boss")
                                    {
                                        phaseHealth = Mathf.RoundToInt(phaseHealth * BossHpMultiple); // 보스는 2.5배 체력
                                        phaseSpeed *= 0.8f; // 보스는 80% 속도
                                    }

                                    _enemyController.SetStatsForPhase(phaseHealth, phaseSpeed);
                                }
                            }
                        }
                    }
                    break;
            }
        }
    }

    //TODO 
    //건물이 건설 되었을때, 추가로 소환하는 특수 소환 기술
    public void SpawnEnemiesByBuildingComplete()
    {
        if (MonsterSpawnTriggerRules.Count > 0)
        {
            for(int i=0; i < MonsterSpawnTriggerRules[MonsterSpawnEventIdx].LinePerMonsterCnt.Count; i++ )
            {
                for(int j= 0;  j < MonsterSpawnTriggerRules[MonsterSpawnEventIdx].LinePerMonsterCnt[i].BossMonsterCnt; j++)
                {
                    Vector2 _randomOffset = Random.insideUnitCircle * m_enemySpreadVal;
                    Vector3 _randomPos = m_Roots[MonsterSpawnTriggerRules[MonsterSpawnEventIdx].LinePerMonsterCnt[i].RootIdx].SpawnPos.position + new Vector3(_randomOffset.x, 0, _randomOffset.y);
                    EnemyController _enemyController = ObjectPool.Instance.SpawnFromPool("Boss", _randomPos, Quaternion.identity).GetComponent<EnemyController>();
                    _enemyController.Initialize(m_MainCenter.transform);

                    // Phase에 맞는 스탯 적용
                    int phaseHealth = GetEnemyHealthForCurrentPhase();
                    float phaseSpeed = GetEnemySpeedForCurrentPhase();

                    phaseHealth = Mathf.RoundToInt(phaseHealth * BossHpMultiple); // 보스는 n배 체력
                    phaseSpeed *= 0.8f; // 보스는 80% 속도

                    _enemyController.SetStatsForPhase(phaseHealth, phaseSpeed);
                }

                for(int k = 0; k < MonsterSpawnTriggerRules[MonsterSpawnEventIdx].LinePerMonsterCnt[i].NormalMonsterCnt; k++)
                {
                    Vector2 _randomOffset = Random.insideUnitCircle * m_enemySpreadVal;
                    Vector3 _randomPos = m_Roots[MonsterSpawnTriggerRules[MonsterSpawnEventIdx].LinePerMonsterCnt[i].RootIdx].SpawnPos.position + new Vector3(_randomOffset.x, 0, _randomOffset.y);
                    EnemyController _enemyController = ObjectPool.Instance.SpawnFromPool("Enemy", _randomPos, Quaternion.identity).GetComponent<EnemyController>();
                    _enemyController.Initialize(m_MainCenter.transform);

                    // Phase에 맞는 스탯 적용
                    int phaseHealth = GetEnemyHealthForCurrentPhase();
                    float phaseSpeed = GetEnemySpeedForCurrentPhase();

                    _enemyController.SetStatsForPhase(phaseHealth, phaseSpeed);
                }
            }
            MonsterSpawnEventIdx++;
            Debug.Log($"=====MonsterSpawnEventIdx : {MonsterSpawnEventIdx}");
        }
    }
    //=====================================================
    // Refresh Enemy Attack System
    //=====================================================
    public void ReSearchTarget(EnemyController _enemy)
    {
        Transform _targetPos = null;
        bool _search = false;

        if (!_search)
        {
            foreach (var _turret in m_Turrets)
            {
                if (_turret.IsBuilt() && _turret.gameObject.activeInHierarchy)
                {
                    _targetPos = _turret.transform;
                    _search = true;
                    break;
                }
            }
        }

        if (!_search)
        {
            foreach (var _wall in m_walls)
            {
                if (_wall.IsBuilt() && _wall.gameObject.activeInHierarchy)
                {
                    _targetPos = _wall.transform;
                    _search = true;
                    break;
                }
            }
        }

        if (!_search)
        {
            foreach (var _mine in m_Mines)
            {
                if (_mine.IsBuilt() && _mine.gameObject.activeInHierarchy)
                {
                    _targetPos = _mine.transform;
                    _search = true;
                    break;
                }
            }
        }

        if (!_search)
        {
            foreach (var _enhance in m_Enhances)
            {
                if (_enhance.IsBuilt() && _enhance.gameObject.activeInHierarchy)
                {
                    _targetPos = _enhance.transform;
                    _search = true;
                    break;
                }
            }
        }
        if (!_search)
            ObjectPool.Instance.ReturnToPool(_enemy.gameObject);
        else
            _enemy.Initialize(_targetPos);
    }
    //=====================================================
    // Main Center Upgrade System
    //=====================================================
    public void OnMainCenterUpgraded()
    {
#if !PLAYABLE_AD
        Debug.Log("MainCenter upgrade completed! Upgrading all walls...");
#endif

        StartCoroutine(UpgradeAllWalls());

        // Phase Building System 체크
        CheckPhaseBuildingCompletion("MainCenter", m_MainCenter.gameObject);
    }

    IEnumerator UpgradeAllWalls()
    {
        List<WallController> wallsToUpgrade = new List<WallController>();

        foreach (WallController wall in m_walls)
        {
            if (wall != null && wall.IsBuilt() && !wall.IsUpgraded())
            {
                wallsToUpgrade.Add(wall);
            }
        }
#if !PLAYABLE_AD
        Debug.Log($"Upgrading {wallsToUpgrade.Count} walls");
#endif


        foreach (WallController wall in wallsToUpgrade)
        {
            wall.UpgradeWall();
            yield return new WaitForSeconds(0.2f);
        }
#if !PLAYABLE_AD
        Debug.Log("All walls upgrade initiated");
#endif

    }

    //=====================================================
    // Phase Building System (단계별 건설)
    //=====================================================
    public void CheckPhaseBuildingCompletion(string buildingType, GameObject building)
    {
        // 먼저 Building Rules 적용
        ApplyBuildingRules(building);

        // Phase System이 활성화되지 않았으면 종료
        if (buildingPhases.Count == 0 || currentPhaseIndex >= buildingPhases.Count)
        {
#if !PLAYABLE_AD
            Debug.Log("Phase Building System not active, skipping phase check");
#endif

            return;
        }

        BuildingPhase currentPhase = buildingPhases[currentPhaseIndex];
        bool isBuildingInCurrentPhase = false;

        // 현재 단계에서 완성된 건물인지 확인 (인덱스 기반)
        if (buildingType == "Turret" && building.GetComponent<TurretController>() != null)
        {
            TurretController turret = building.GetComponent<TurretController>();
            int turretIndex = m_Turrets.IndexOf(turret);
            if (turretIndex >= 0 && currentPhase.turretIndices.Contains(turretIndex))
            {
                isBuildingInCurrentPhase = true;
#if !PLAYABLE_AD
                Debug.Log($"Turret[{turretIndex}] {turret.name} completed in Phase {currentPhaseIndex}");
#endif

            }
        }
        else if (buildingType == "Mine" && building.GetComponent<MineController>() != null)
        {
            MineController mine = building.GetComponent<MineController>();
            int mineIndex = m_Mines.IndexOf(mine);
            if (mineIndex >= 0 && currentPhase.mineIndices.Contains(mineIndex))
            {
                isBuildingInCurrentPhase = true;
#if !PLAYABLE_AD
                Debug.Log($"Mine[{mineIndex}] {mine.name} completed in Phase {currentPhaseIndex}");
#endif

            }
        }
        else if (buildingType == "Wall" && building.GetComponent<WallController>() != null)
        {
            WallController wall = building.GetComponent<WallController>();
            int wallIndex = m_walls.IndexOf(wall);
            if (wallIndex >= 0 && currentPhase.wallIndices.Contains(wallIndex))
            {
                isBuildingInCurrentPhase = true;
#if !PLAYABLE_AD
                Debug.Log($"Wall[{wallIndex}] {wall.name} completed in Phase {currentPhaseIndex}");
#endif

            }
        }
        else if (buildingType == "Enhance" && building.GetComponent<EnhanceController>() != null)
        {
            EnhanceController enhance = building.GetComponent<EnhanceController>();
            int enhanceIndex = m_Enhances.IndexOf(enhance);
            if (enhanceIndex >= 0 && currentPhase.enhances.Contains(enhanceIndex))
            {
                isBuildingInCurrentPhase = true;
#if !PLAYABLE_AD
                Debug.Log($"enhance[{enhanceIndex}] {enhance.name} completed in Phase {currentPhaseIndex}");
#endif

            }
        }
        else if (buildingType == "MainCenter")
        {
            // MainCenter 업그레이드는 항상 카운트 (모든 단계에 유효)
            isBuildingInCurrentPhase = true;
#if !PLAYABLE_AD
            Debug.Log($"MainCenter upgrade completed in Phase {currentPhaseIndex}");
#endif

        }

        // 현재 단계의 건물이면 카운트 증가
        if (isBuildingInCurrentPhase)
        {
            completedBuildingsInPhase++;
#if !PLAYABLE_AD
            Debug.Log($"Phase {currentPhaseIndex} progress: {completedBuildingsInPhase}/{currentPhase.requiredCompletions}");
#endif

            // 필요한 건설 수가 충족되면 다음 단계로
            if (completedBuildingsInPhase == currentPhase.requiredCompletions)
            {
                AdvanceToNextPhase();
            }
        }
    }

    void AdvanceToNextPhase()
    {
#if !PLAYABLE_AD
        Debug.Log($"Advancing from Phase {currentPhaseIndex}");
#endif


        currentPhaseIndex++;
        completedBuildingsInPhase = 0;

        // Phase 클리어 시 적 시스템 강화
        IncreaseEnemyDifficulty();

        if (currentPhaseIndex < buildingPhases.Count)
        {
#if !PLAYABLE_AD
            Debug.Log($"Starting Phase {currentPhaseIndex}: {buildingPhases[currentPhaseIndex].phaseName}");
#endif

            ShowCurrentPhaseBuildings();
            UpdateGuideLine();
        }
        else
        {
#if !PLAYABLE_AD
            Debug.Log("All building phases completed!");
#endif

        }
    }

    void ShowCurrentPhaseBuildings()
    {
        if (currentPhaseIndex >= buildingPhases.Count)
        {
#if !PLAYABLE_AD
            Debug.LogWarning("Cannot show buildings: Phase index out of range");
#endif

            return;
        }

        BuildingPhase currentPhase = buildingPhases[currentPhaseIndex];
#if !PLAYABLE_AD
        Debug.Log($"Showing buildings for Phase {currentPhaseIndex}: {currentPhase.phaseName}");
#endif


        // 터렛들 활성화 (인덱스 기반)
        foreach (int turretIndex in currentPhase.turretIndices)
        {
            if (IsValidIndex(turretIndex, m_Turrets.Count) && !disabledTurretIndices.Contains(turretIndex))
            {
                TurretController turret = m_Turrets[turretIndex];
                turret.SetVisibility(true);
                turret.Init();
#if !PLAYABLE_AD
                Debug.Log($"Enabled turret[{turretIndex}]: {turret.name}");
#endif

            }
        }

        // 광산들 활성화 (인덱스 기반)
        foreach (int mineIndex in currentPhase.mineIndices)
        {
            if (IsValidIndex(mineIndex, m_Mines.Count) && !disabledMineIndices.Contains(mineIndex))
            {
                MineController mine = m_Mines[mineIndex];
                mine.Init();
                mine.SetVisibility(true);
#if !PLAYABLE_AD
                Debug.Log($"Enabled mine[{mineIndex}]: {mine.name}");
#endif

            }
        }

        // 벽들 활성화 (인덱스 기반, WallController가 자체 조건과 함께 작동)
        foreach (int wallIndex in currentPhase.wallIndices)
        {
            if (IsValidIndex(wallIndex, m_walls.Count) && !disabledWallIndices.Contains(wallIndex))
            {
                WallController wall = m_walls[wallIndex];
                wall.RecheckCondition();
#if !PLAYABLE_AD
                Debug.Log($"Wall[{wallIndex}] condition rechecked: {wall.name}");
#endif

            }
        }

        foreach (int enhanceIndex in currentPhase.enhances)
        {
            if (IsValidIndex(enhanceIndex, m_Enhances.Count) && !disabledEnhances.Contains(enhanceIndex))
            {
                EnhanceController enhance = m_Enhances[enhanceIndex];
                enhance.Init();
                enhance.SetVisibility(true);
#if !PLAYABLE_AD
                Debug.Log($"enhance[{enhanceIndex}] condition rechecked: {enhance.name}");
#endif

            }
        }
    }

    void ApplyBuildingRules(GameObject builtBuilding)
    {
        foreach (BuildingRule rule in buildingRules)
        {
            if (ShouldApplyRule(rule, builtBuilding))
            {
#if !PLAYABLE_AD
                Debug.Log($"Applying building rule: {rule.ruleName}");
#endif


                // 터렛들 비활성화 (인덱스 기반)
                foreach (int turretIndex in rule.disableTurretIndices)
                {
                    if (IsValidIndex(turretIndex, m_Turrets.Count))
                    {
                        disabledTurretIndices.Add(turretIndex);
                        m_Turrets[turretIndex].SetVisibility(false,true);
#if !PLAYABLE_AD
                        Debug.Log($"Disabled turret[{turretIndex}]: {m_Turrets[turretIndex].name}");
#endif

                    }
                }

                // 광산들 비활성화 (인덱스 기반)
                foreach (int mineIndex in rule.disableMineIndices)
                {
                    if (IsValidIndex(mineIndex, m_Mines.Count))
                    {
                        disabledMineIndices.Add(mineIndex);
                        m_Mines[mineIndex].SetVisibility(false);
#if !PLAYABLE_AD
                        Debug.Log($"Disabled mine[{mineIndex}]: {m_Mines[mineIndex].name}");
#endif

                    }
                }

                // 벽들 비활성화 (인덱스 기반)
                foreach (int wallIndex in rule.disableWallIndices)
                {
                    if (IsValidIndex(wallIndex, m_walls.Count))
                    {
                        disabledWallIndices.Add(wallIndex);
                        m_walls[wallIndex].gameObject.SetActive(false);
#if !PLAYABLE_AD
                        Debug.Log($"Disabled wall[{wallIndex}]: {m_walls[wallIndex].name}");
#endif

                    }
                }

                foreach (int enhanceIndex in rule.disableEnhanceIndices)
                {
                    if (IsValidIndex(enhanceIndex, m_Enhances.Count))
                    {
                        disabledEnhances.Add(enhanceIndex);
                        m_Enhances[enhanceIndex].SetVisibility(false);
#if !PLAYABLE_AD
                        Debug.Log($"Disabled enhance[{enhanceIndex}]: {m_Enhances[enhanceIndex].name}");
#endif

                    }
                }
            }
        }
    }

    bool ShouldApplyRule(BuildingRule rule, GameObject builtBuilding)
    {
        if (rule.triggerBuildings.Count == 0) return false;

        int completedTriggers = 0;

        foreach (GameObject triggerBuilding in rule.triggerBuildings)
        {
            if (triggerBuilding == builtBuilding)
            {
                completedTriggers++;
                continue;
            }

            // 이미 완성된 건물인지 확인
            if (IsBuildingCompleted(triggerBuilding))
            {
                completedTriggers++;
            }
        }

        if (rule.requireAllBuildings)
        {
            // 모든 건물이 완성되어야 함
            return completedTriggers >= rule.triggerBuildings.Count;
        }
        else
        {
            // 하나만 완성되면 됨
            return completedTriggers > 0;
        }
    }

    bool IsBuildingCompleted(GameObject building)
    {
        if (building == null) return false;

        // 터렛 체크
        TurretController turret = building.GetComponent<TurretController>();
        if (turret != null) return turret.IsBuilt();

        // 광산 체크
        MineController mine = building.GetComponent<MineController>();
        if (mine != null) return mine.IsBuilt();

        // 벽 체크
        WallController wall = building.GetComponent<WallController>();
        if (wall != null) return wall.IsBuilt();

        EnhanceController enhance = building.GetComponent<EnhanceController>();
        if (enhance != null) return enhance.IsBuilt();
        // 메인센터 체크
        MainCenterController mainCenter = building.GetComponent<MainCenterController>();
        if (mainCenter != null) return mainCenter.IsUpgraded();

        return false;
    }

    bool IsValidIndex(int index, int listCount)
    {
        return index >= 0 && index < listCount;
    }

    void InitializePhaseBuilding()
    {
#if !PLAYABLE_AD
        Debug.Log("Initializing Phase Building System");
#endif


        // 모든 건물을 먼저 비활성화
        foreach (TurretController turret in m_Turrets)
        {
            if (turret != null)
                turret.SetVisibility(false);
        }

        foreach (MineController mine in m_Mines)
        {
            if (mine != null)
                mine.SetVisibility(false);
        }

        // 벽들은 강제로 비활성화하지 않음 (WallController가 자체적으로 requiredTurrets 조건 관리)
        foreach (WallController wall in m_walls)
        {
            if (wall != null)
            {
                wall.Init();
#if !PLAYABLE_AD
                Debug.Log($"Wall initialized: {wall.name}");
#endif

            }
        }
        foreach (EnhanceController enhance in m_Enhances)
        {
            if (enhance != null)
            {
                enhance.SetVisibility(false);
            }
        }
        // 첫 번째 단계 시작
        currentPhaseIndex = 0;
        completedBuildingsInPhase = 0;
        ShowCurrentPhaseBuildings();
    }

    //=====================================================
    // Sequential Building System (기존 시스템)
    //=====================================================
    public void OnTurretBuilt(TurretController builtTurret)
    {
#if !PLAYABLE_AD
        Debug.Log($"Turret built: {builtTurret.name}");
#endif


        // Phase Building System 체크 (우선순위)
        CheckPhaseBuildingCompletion("Turret", builtTurret.gameObject);

        // 기존 Sequential Building 로직도 유지
        if (!enableSequentialBuilding)
        {
            UpdateActiveRootsByTurrets();
            CheckWallConditions();
            UpdateGuideLine();
            SpawnEnemiesByBuildingComplete();
            return;
        }

        int builtIndex = m_Turrets.IndexOf(builtTurret);
        if (builtIndex == currentAvailableTurretIndex)
        {
#if !PLAYABLE_AD
            Debug.Log($"Sequential: Turret {builtIndex} construction completed!");
#endif


            currentAvailableTurretIndex++;

            if (currentAvailableTurretIndex < m_Turrets.Count)
            {
                m_Turrets[currentAvailableTurretIndex].SetVisibility(true);
                m_Turrets[currentAvailableTurretIndex].Init();
#if !PLAYABLE_AD
                Debug.Log($"Sequential: Next turret {currentAvailableTurretIndex} available");
#endif

            }
            else
            {
#if !PLAYABLE_AD
                Debug.Log("Sequential: All turrets unlocked!");
#endif
            }
        }
        SpawnEnemiesByBuildingComplete();
        UpdateActiveRootsByTurrets();
        CheckWallConditions();
        UpdateGuideLine();
    }

    public void OnMineBuilt(MineController builtMine)
    {
#if !PLAYABLE_AD
        Debug.Log($"Mine built: {builtMine.name}");
#endif


        // Phase Building System 체크 (우선순위)
        CheckPhaseBuildingCompletion("Mine", builtMine.gameObject);

        //TODO CHECK
        //2025-12-04
        // 기존 Sequential Building 로직도 유지
        //if (!enableSequentialBuilding)
        //{
        //    CheckWallConditions();
        //    return;
        //}

        int builtIndex = m_Mines.IndexOf(builtMine);
        if (builtIndex == currentAvailableMineIndex)
        {
#if !PLAYABLE_AD
            Debug.Log($"Sequential: Mine {builtIndex} construction completed!");
#endif


            currentAvailableMineIndex++;

            if (currentAvailableMineIndex < m_Mines.Count)
            {
                //m_Mines[currentAvailableMineIndex].Init();
                //m_Mines[currentAvailableMineIndex].SetVisibility(true);
#if !PLAYABLE_AD
                Debug.Log($"Sequential: Next mine {currentAvailableMineIndex} available");
#endif

            }
            else
            {
#if !PLAYABLE_AD
                Debug.Log("Sequential: All mines unlocked!");
#endif

            }
        }
        //UpdateGuideLine();
        //CheckWallConditions();
    }

    public void OnWallBuilt(WallController builtWall)
    {
#if !PLAYABLE_AD
        Debug.Log($"Wall built: {builtWall.name}");
#endif


        // Phase Building System 체크
        CheckPhaseBuildingCompletion("Wall", builtWall.gameObject);
    }

    public void OnEnhanceBuilt(EnhanceController builtEnhance)
    {
#if !PLAYABLE_AD
        Debug.Log($"builtEnhance built: {builtEnhance.name}");
#endif


        // Phase Building System 체크 (우선순위)
        CheckPhaseBuildingCompletion("Enhance", builtEnhance.gameObject);

        //if (!enableSequentialBuilding)
        //{
        //    CheckWallConditions();
        //    return;
        //}

        int builtIndex = m_Enhances.IndexOf(builtEnhance);
        if (builtIndex == currentAvailableEnhanceIndex)
        {
#if !PLAYABLE_AD
            Debug.Log($"Sequential: Enhancce {builtIndex} construction completed!");
#endif


            if (currentAvailableEnhanceIndex < m_Enhances.Count)
            {
                //m_Enhances[currentAvailableEnhanceIndex].Init();
                //m_Enhances[currentAvailableEnhanceIndex].SetVisibility(true);
                currentAvailableEnhanceIndex++;
#if !PLAYABLE_AD
                Debug.Log($"Sequential: Next enhance {currentAvailableEnhanceIndex} available");
#endif

            }
            else
            {
#if !PLAYABLE_AD
                Debug.Log("Sequential: All enhance unlocked!");
#endif

            }
        }

        //UpdateGuideLine();
        //CheckWallConditions();
    }

    void UpdateActiveRootsByTurrets()
    {
        int builtTurretCount = 0;

        foreach (TurretController turret in m_Turrets)
        {
            if (turret.IsBuilt())
            {
                builtTurretCount++;
            }
        }

        int newActiveRootCount = baseActiveRoots + builtTurretCount;
        activeRootCount = Mathf.Clamp(newActiveRootCount, baseActiveRoots, m_Roots.Count);
#if !PLAYABLE_AD
        Debug.Log($"Active roots updated: {activeRootCount} (Built turrets: {builtTurretCount})");
#endif

    }

    Transform GetNextBuildableBuilding()
    {
        BuildingPhase _curPhase = buildingPhases[currentPhaseIndex];
        if (_curPhase.turretIndices.Count > 0)
        {
            for(int i= 0; i < _curPhase.turretIndices.Count; i++)
            {
                TurretController _turret = m_Turrets[_curPhase.turretIndices[i]];

                if (_turret!=null && !_turret.IsBuilt() && _turret.gameObject.activeInHierarchy)
                {
#if !PLAYABLE_AD
                    Debug.Log("====Turret is Not Null");
#endif

                    return _turret.transform;
                }
            }
        }
        else if(_curPhase.mineIndices.Count > 0)
        {
#if !PLAYABLE_AD
            Debug.Log($"====GetNextBuildableBuilding() mineIndices.count :  {_curPhase.mineIndices.Count} Current Phase Index  : {currentPhaseIndex}");
#endif

            for (int i = 0; i < _curPhase.mineIndices.Count; i++)
            {
                MineController _mine = m_Mines[_curPhase.mineIndices[i]];
                if (_mine != null && !_mine.IsBuilt() && _mine.gameObject.activeInHierarchy)
                {
                    return _mine.transform;
                }
            }
        }
        else if(_curPhase.enhances.Count > 0)
        {
#if !PLAYABLE_AD
            Debug.Log($"====GetNextBuildableBuilding() mineIndices.count :  {_curPhase.enhances.Count} Current Phase Index  : {currentPhaseIndex}");
#endif

            for (int i = 0; i < _curPhase.enhances.Count; i++)
            {
                EnhanceController _enhance = m_Enhances[_curPhase.enhances[i]];
                if (_enhance != null && !_enhance.IsBuilt() && _enhance.gameObject.activeInHierarchy)
                {
                    return _enhance.transform;
                }
            }
        }

        //TODO 변경 필요함 
        //foreach (TurretController turret in m_Turrets)
        //{
        //    if (turret != null && !turret.IsBuilt() && turret.gameObject.activeInHierarchy)
        //    {
        //        return turret.transform;
        //    }
        //}

        //foreach (MineController mine in m_Mines)
        //{
        //    if (mine != null && !mine.IsBuilt() && mine.gameObject.activeInHierarchy)
        //    {
        //        return mine.transform;
        //    }
        //}

        //foreach (EnhanceController enhance in m_Enhances)
        //{
        //    if (enhance != null && !enhance.IsBuilt() && enhance.gameObject.activeInHierarchy)
        //    {
        //        return enhance.transform;
        //    }
        //}

        return null;
    }

    void UpdateGuideLine()
    {
        if (!enableGuideLine)
        {
            ClearGuideLine();
            return;
        }

        Transform target = GetNextBuildableBuilding();

        if (target == null)
        {
            ClearGuideLine();
            return;
        }

        TurretController _turret = target.GetComponent<TurretController>();
        if(_turret!=null)
        {
            int _cost = _turret.GetRemainPaidGold();
            if (_cost > currentGold)
            {
                ClearGuideLine();
                return;
            }
        }

        MineController _mine = target.GetComponent<MineController>();
        if (_mine != null)
        {
            int _cost = _mine.GetRemainPaidGold();
            if (_cost > currentGold)
            {
                ClearGuideLine();
                return;
            }
        }

        EnhanceController _enhance = target.GetComponent<EnhanceController>();
        if (_enhance != null)
        {
            int _cost = _enhance.GetRemainPaidGold();
            if (_cost > currentGold)
            {
                ClearGuideLine();
                return;
            }
        }

        if (_currentGuide == null)
        {
            GameObject guideObj = ObjectPool.Instance
                .SpawnFromPool("GuideController", Vector3.zero, Quaternion.identity, transform);

            if (guideObj == null)
                return;

            _currentGuide = guideObj.GetComponent<GuideController>();
        }

        if (_currentGuide != null)
        {
            _currentGuide.gameObject.SetActive(true);
            _currentGuide.Init(m_Player.transform, target.transform);
        }
    }

    void ClearGuideLine()
    {
        if (_currentGuide != null)
        {
            _currentGuide.StopGuide();
            _currentGuide = null;
        }
    }

    void InitializeSequentialBuilding()
    {
#if !PLAYABLE_AD
        Debug.Log("Initializing Sequential Building System");
#endif


        if (!enableSequentialBuilding)
        {
            InitializeAllBuildings();
            return;
        }

        for (int i = 0; i < m_Turrets.Count; i++)
        {
            if (i == currentAvailableTurretIndex)
            {
                m_Turrets[i].SetVisibility(true);
                m_Turrets[i].Init();
#if !PLAYABLE_AD
                Debug.Log($"Sequential: Turret {i} available");
#endif

            }
            else
            {
                m_Turrets[i].SetVisibility(false);
            }
        }

        for (int i = 0; i < m_Mines.Count; i++)
        {
            if (i == currentAvailableMineIndex)
            {
                m_Mines[i].Init();
                m_Mines[i].SetVisibility(true);
#if !PLAYABLE_AD
                Debug.Log($"Sequential: Mine {i} available");
#endif

            }
            else
            {
                m_Mines[i].SetVisibility(false);
            }
        }

        for (int i = 0; i < m_Enhances.Count; i++)
        {
            if (i == currentAvailableEnhanceIndex)
            {
                m_Enhances[i].Init();
                m_Enhances[i].SetVisibility(true);
#if !PLAYABLE_AD
                Debug.Log($"Sequential: m_Enhances {i} available");
#endif

            }
            else
            {
                m_Enhances[i].SetVisibility(false);
            }
        }
    }

    void InitializeAllBuildings()
    {
        foreach (var turret in m_Turrets)
        {
            if (turret != null)
            {
                turret.SetVisibility(true);
                turret.Init();
            }
        }

        foreach (var mine in m_Mines)
        {
            if (mine != null)
            {
                mine.Init();
                mine.SetVisibility(true);
            }
        }

        foreach (var enhance in m_Enhances)
        {
            if (enhance != null)
            {
                enhance.Init();
                enhance.SetVisibility(true);
            }
        }
        UpdateActiveRootsByTurrets();
        UpdateGuideLine();
    }

    void CheckWallConditions()
    {
        //TODO CHECK
        //2025-12-04
        //foreach 삭제
        foreach (WallController wall in m_walls)
        {
            if (wall != null)
            {
                //Debug.Log($"벽 {wall.name} 조건 재검사");
                wall.RecheckCondition();
            }
        }

        //Debug.Log("모든 벽 조건 재검사 완료");
    }

    //=====================================================
    // UI & Gold & Game End
    //=====================================================
    void RefreshUI()
    {
        UpdateGoldUI();
    }

    void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = "Gold: " + currentGold;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        UpdateGoldUI();
        UpdateGuideLine();

        if (m_Player)
            m_Player.OnGoldStackCall(currentGold);
    }

    public int GetCurrentGold()
    {
        return currentGold;
    }

    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            UpdateGoldUI();
            return true;
        }
        return false;
    }

    public void OnEnemyKilled()
    {
        enemyKilled++;
    }

    public VirtualPad GetVirtualPad() => m_VirtualPad;

    public void EndGame(bool isWin)
    {
        gameEnded = true;

        StartCoroutine(ShowWinUI());
        

        ClearGuideLine();
    }

    IEnumerator ShowWinUI()
    {
        GetVirtualPad().OnGettouchOverlay().SetActive(false);
        yield return new WaitForSeconds(0.2f);
        if (winUI != null)
            winUI.SetActive(true);
    }

    //=====================================================
    // Public API for other systems
    //=====================================================
    public MainCenterController GetMainCenter() => m_MainCenter;

    public bool IsMainCenterUpgraded()
    {
        return m_MainCenter != null && m_MainCenter.IsUpgraded();
    }

    public List<WallController> GetWalls() => m_walls;

    public List<TurretController> GetTurrets() => m_Turrets;

    /// <summary>
    /// 업그레이드 가능한 터렛들을 인덱스 순서대로 반환 (IsBuilt이고 아직 업그레이드되지 않은 터렛들)
    /// </summary>
    /// <returns>업그레이드 가능한 터렛 리스트</returns>
    public List<TurretController> GetUpgradeableTurrets()
    {
        List<TurretController> upgradeableTurrets = new List<TurretController>();

        for (int i = 0; i < m_Turrets.Count; i++)
        {
            TurretController turret = m_Turrets[i];

            // 터렛이 존재하고, 건설되었고, 아직 업그레이드되지 않은 경우만 추가
            if (turret != null && turret.gameObject.activeInHierarchy &&
                turret.IsBuilt() && !turret.IsUpgraded())
            {
                upgradeableTurrets.Add(turret);
            }
        }

        //Debug.Log($"Found {upgradeableTurrets.Count} upgradeable turrets out of {m_Turrets.Count} total turrets");
        return upgradeableTurrets;
    }

    public List<MineController> GetMines() => m_Mines;

    //=====================================================
    // Phase Building System Public API
    //=====================================================
    public int GetCurrentPhaseIndex()
    {
        return currentPhaseIndex;
    }

    public bool IsPhaseBuilding()
    {
        return buildingPhases.Count > 0;
    }

    public bool ShouldIgnoreWallRequirements(int wallIndex)
    {
        if (buildingPhases.Count == 0 || currentPhaseIndex >= buildingPhases.Count)
            return false;

        var currentPhase = buildingPhases[currentPhaseIndex];

        // 이 벽이 현재 Phase에 포함되어 있고 ignoreWallRequirements가 true인지 확인
        if (currentPhase.wallIndices.Contains(wallIndex))
        {
            return currentPhase.ignoreWallRequirements;
        }

        return false;
    }

    //=====================================================
    // MainCenter Attack Target System
    //=====================================================
    /// <summary>
    /// 적이 공격할 수 있는 모든 건물들을 반환 (MainCenter 포함)
    /// </summary>
    public List<Transform> GetAllAttackableBuildings()
    {
        List<Transform> attackableBuildings = new List<Transform>();

        // MainCenter 추가 (항상 공격 가능)
        if (m_MainCenter != null && m_MainCenter.gameObject.activeInHierarchy)
        {
            attackableBuildings.Add(m_MainCenter.transform);
        }

        // 건설된 터렛들 추가
        foreach (var turret in m_Turrets)
        {
            if (turret != null && turret.gameObject.activeInHierarchy && turret.IsBuilt())
            {
                attackableBuildings.Add(turret.transform);
            }
        }

        // 건설된 벽들 추가  
        foreach (var wall in m_walls)
        {
            if (wall != null && wall.gameObject.activeInHierarchy && wall.IsBuilt())
            {
                attackableBuildings.Add(wall.transform);
            }
        }

        // 건설된 광산들 추가
        foreach (var mine in m_Mines)
        {
            if (mine != null && mine.gameObject.activeInHierarchy && mine.IsBuilt())
            {
                attackableBuildings.Add(mine.transform);
            }
        }

        return attackableBuildings;
    }

    /// <summary>
    /// MainCenter가 공격받을 수 있는지 확인
    /// </summary>
    public bool IsMainCenterAttackable()
    {
        return m_MainCenter != null &&
               m_MainCenter.gameObject.activeInHierarchy &&
               !m_MainCenter.IsDead();
    }

    //=====================================================
    // Enemy Difficulty Scaling System
    //=====================================================

    /// <summary>
    /// Phase 클리어 시 적 난이도 증가 (Phase별 다른 전략)
    /// </summary>
    void IncreaseEnemyDifficulty()
    {
#if !PLAYABLE_AD
        Debug.Log($"=== Phase {currentPhaseIndex} Enemy Difficulty Update ===");
#endif


        // Phase별 다른 강화 방식 적용
        if (currentPhaseIndex == 1)
        {
            // Phase 1 클리어: Boss 몬스터 베리에이션 추가
            AddBossVariation();
#if !PLAYABLE_AD
            Debug.Log("Phase 1 Cleared: Boss enemies unlocked!");
#endif

        }
        else
        {
            // Phase 2+ 클리어: 기존 적 수/속도 증가
            IncreaseEnemyQuantityAndSpeed();
#if !PLAYABLE_AD
            Debug.Log($"Phase {currentPhaseIndex} Cleared: Enemy quantity and speed increased!");
#endif

        }

        // 공통: 활성화된 레인 수 증가
        IncreaseActiveRoots();
#if !PLAYABLE_AD
        Debug.Log($"Active Roots: {activeRootCount}, Boss Mode: {bossModeEnabled}");
        Debug.Log($"Spawn Count: {m_PerSpawnEnemyCnt}, Spawn Interval: {enemySpawnInterval:F1}s");
#endif

    }

    /// <summary>
    /// Phase 1 클리어 시: Boss 몬스터 베리에이션 추가
    /// </summary>
    void AddBossVariation()
    {
        bossModeEnabled = true;
#if !PLAYABLE_AD
        Debug.Log("Boss enemies are now included in spawning!");
#endif

    }

    /// <summary>
    /// Phase 2+ 클리어 시: 적 수와 속도 증가
    /// </summary>
    void IncreaseEnemyQuantityAndSpeed()
    {
        // 적 스폰 수 증가
        m_PerSpawnEnemyCnt += enemyCountIncrease;

        // 스폰 간격 감소 (더 빠르게 스폰)
        enemySpawnInterval = Mathf.Max(0.5f, enemySpawnInterval - spawnIntervalDecrease);

        // 현재 적들의 스탯 강화 (이미 존재하는 적들)
        EnhanceExistingEnemies();
    }

    /// <summary>
    /// 활성화된 레인 수 증가
    /// </summary>
    void IncreaseActiveRoots()
    {
        if (activeRootCount < m_Roots.Count)
        {
            activeRootCount++;
#if !PLAYABLE_AD
            Debug.Log($"Active enemy routes increased to: {activeRootCount}/{m_Roots.Count}");
#endif

        }
    }

    /// <summary>
    /// 현재 존재하는 적들의 스탯 강화
    /// </summary>
    void EnhanceExistingEnemies()
    {
        EnemyController[] allEnemies = FindObjectsOfType<EnemyController>();

        foreach (EnemyController enemy in allEnemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                // 체력 증가 (현재 Phase에 맞춰)
                int newMaxHealth = Mathf.RoundToInt(baseEnemyHealth * Mathf.Pow(enemyHealthMultiplier, currentPhaseIndex));
                enemy.IncreaseHealth(newMaxHealth);

                // 이동 속도 증가
                float newSpeed = baseEnemySpeed * Mathf.Pow(enemySpeedMultiplier, currentPhaseIndex);
                enemy.IncreaseSpeed(newSpeed);

#if !PLAYABLE_AD
                Debug.Log($"Enhanced {enemy.name}: Health={newMaxHealth}, Speed={newSpeed:F1}");
#endif

            }
        }
    }

    /// <summary>
    /// 새로 스폰되는 적의 스탯 계산
    /// </summary>
    public int GetEnemyHealthForCurrentPhase()
    {
        return Mathf.RoundToInt(baseEnemyHealth * Mathf.Pow(enemyHealthMultiplier, currentPhaseIndex));
    }

    /// <summary>
    /// 새로 스폰되는 적의 속도 계산
    /// </summary>
    public float GetEnemySpeedForCurrentPhase()
    {
        return baseEnemySpeed * Mathf.Pow(enemySpeedMultiplier, currentPhaseIndex);
    }

    /// <summary>
    /// 적 기본 스탯 설정 (초기화 시 호출)
    /// </summary>
    public void SetBaseEnemyStats(int health, float speed, float spawnInterval)
    {
        baseEnemyHealth = health;
        baseEnemySpeed = speed;
        baseSpawnInterval = spawnInterval;
        enemySpawnInterval = baseSpawnInterval;
    }

    //=====================================================
    // Building Collision System (WebGL Optimized)
    //=====================================================

    /// <summary>
    /// 위치가 건물과 충돌하는지 확인 (WebGL 최적화 - 물리체 없이 거리 기반)
    /// </summary>
    /// <param name="position">확인할 위치</param>
    /// <param name="excludeTransform">제외할 Transform (자기 자신)</param>
    /// <returns>충돌하면 true</returns>
    public bool IsPositionCollidingWithBuilding(Vector3 position, Transform excludeTransform = null, bool _isPlayer = false, bool _isNPC = false)
    {
        if (!enableBuildingCollision) return false;

        // 건설된 벽들 충돌 체크
        foreach (var wall in m_walls)
        {
            if (wall != null && wall.transform != excludeTransform &&
                wall.gameObject.activeInHierarchy && wall.IsBuilt())
            {
                if (wall.ClampToBoundary(position, wall.IsHasGate(), _isPlayer))
                {
                    return true;
                }
            }
        }

        // MainCenter 충돌 체크
        if (m_MainCenter != null && m_MainCenter.transform != excludeTransform &&
            m_MainCenter.gameObject.activeInHierarchy)
        {
            float distance = Vector3.Distance(position, m_MainCenter.transform.position);
            if (distance <= buildingCollisionRadius)
            {
                return true;
            }
        }

        // 건설된 터렛들 충돌 체크
        foreach (var turret in m_Turrets)
        {
            if (turret != null && turret.transform != excludeTransform &&
                turret.gameObject.activeInHierarchy && turret.IsBuilt() && !_isNPC)
            {
                float distance = Vector3.Distance(position, turret.transform.position);
                if (distance <= buildingCollisionRadius)
                {
                    return true;
                }
            }
        }

        // 건설된 광산들 충돌 체크
        foreach (var mine in m_Mines)
        {
            if (mine != null && mine.transform != excludeTransform &&
                mine.gameObject.activeInHierarchy && mine.IsBuilt())
            {
                float distance = Vector3.Distance(position, mine.transform.position);
                if (distance <= buildingCollisionRadius)
                {
                    return true;
                }
            }
        }

        foreach (var enhance in m_Enhances)
        {
            if (enhance != null && enhance.transform != excludeTransform &&
                enhance.gameObject.activeInHierarchy && enhance.IsBuilt()&&!_isNPC)
            {
                float distance = Vector3.Distance(position, enhance.transform.position);
                if (distance <= buildingCollisionRadius)
                {
                    return true;
                }
            }
        }
        //TODO 플레이어 추가

        return false;
    }

    /// <summary>
    /// 이동 가능한 위치로 조정 (건물 충돌 회피)
    /// </summary>
    /// <param name="currentPos">현재 위치</param>
    /// <param name="targetPos">목표 위치</param>
    /// <param name="excludeTransform">제외할 Transform</param>
    /// <returns>조정된 위치</returns>
    public Vector3 GetValidMovePosition(Vector3 currentPos, Vector3 targetPos, Transform excludeTransform = null, bool _isPlayer = false)
    {
        // 목표 위치가 충돌하지 않으면 그대로 반환
        if (!IsPositionCollidingWithBuilding(targetPos, excludeTransform))
        {
            return targetPos;
        }

        if (_isPlayer)
        {
            // 충돌하는 경우 우회 경로 계산
            Vector3 direction = (targetPos - currentPos).normalized;

            // 좌우로 회피 시도
            Vector3[] avoidanceDirections = {
            Quaternion.Euler(0, 45, 0) * direction,   // 45도 우회전
            Quaternion.Euler(0, -45, 0) * direction,  // 45도 좌회전
            Quaternion.Euler(0, 90, 0) * direction,   // 90도 우회전
            Quaternion.Euler(0, -90, 0) * direction,  // 90도 좌회전
            -direction  // 후진
            };

            float moveDistance = Vector3.Distance(currentPos, targetPos);
            moveDistance = Mathf.Min(moveDistance, 0.5f); // 최대 2유닛까지만 이동

            foreach (Vector3 avoidDir in avoidanceDirections)
            {
                Vector3 avoidPos = currentPos + avoidDir * moveDistance;
                if (!IsPositionCollidingWithBuilding(avoidPos, excludeTransform))
                {
                    return avoidPos;
                }
            }

            // 모든 회피가 실패하면 현재 위치 유지
            currentPos += new Vector3(0.1f, 0, 0.1f);
        }
        return currentPos;
    }

    public void OnClickBtnOpenURL()
    {
        Application.OpenURL(downloadUrl);
    }
}