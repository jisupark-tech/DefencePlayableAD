using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Pool
{
    public string tag;
    public GameObject prefab;
    public int size;
    public bool canExpand = true; // 풀 확장 가능 여부
    public int maxSize = 1000; // 최대 크기 제한
}

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance;

    [Header("Pool Settings")]
    public List<Pool> pools;
    [Header("Debug")]
    public bool showDebugLogs = false;

    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Pool> poolSettings; // 풀 설정 참조
    private Dictionary<string, List<GameObject>> allPoolObjects; // 모든 풀 오브젝트 추적

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void Init()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolSettings = new Dictionary<string, Pool>();
        allPoolObjects = new Dictionary<string, List<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();
            List<GameObject> allObjects = new List<GameObject>();

            // 초기 풀 생성
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = CreatePoolObject(pool.prefab, pool.tag);
                objectPool.Enqueue(obj);
                allObjects.Add(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
            poolSettings.Add(pool.tag, pool);
            allPoolObjects.Add(pool.tag, allObjects);

            if (showDebugLogs)
#if !PLAYABLE_AD
                Debug.Log($"Initialized pool '{pool.tag}' with {pool.size} objects");
#endif

        }
    }

    GameObject CreatePoolObject(GameObject prefab, string poolTag)
    {
        GameObject obj = Instantiate(prefab);
        obj.SetActive(false);
        obj.name = $"{prefab.name}_{poolTag}"; // 디버깅을 위한 이름 설정
        return obj;
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation, Transform _parent = null)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"Pool with tag '{tag}' doesn't exist!");
#endif

            return null;
        }

        GameObject objectToSpawn = null;

        // 사용 가능한 오브젝트가 있는지 확인
        if (poolDictionary[tag].Count > 0)
        {
            objectToSpawn = poolDictionary[tag].Dequeue();
        }
        else
        {
            // 풀이 비어있는 경우
            Pool poolSetting = poolSettings[tag];

            if (poolSetting.canExpand && allPoolObjects[tag].Count < poolSetting.maxSize)
            {
                // 풀 확장
                objectToSpawn = CreatePoolObject(poolSetting.prefab, tag);
                allPoolObjects[tag].Add(objectToSpawn);

                if (showDebugLogs)
#if !PLAYABLE_AD
                    Debug.Log($"Expanded pool '{tag}': {allPoolObjects[tag].Count}/{poolSetting.maxSize}");
#endif

            }
            else
            {
                // 풀을 확장할 수 없거나 최대 크기에 도달한 경우, 가장 오래된 활성 오브젝트를 재사용
                objectToSpawn = FindOldestActiveObject(tag);

                if (objectToSpawn != null)
                {
                    if (showDebugLogs)
#if !PLAYABLE_AD
                        Debug.Log($"Reusing oldest active object from pool '{tag}'");
#endif

                }
                else
                {
#if !PLAYABLE_AD
                    Debug.LogError($"Cannot spawn from pool '{tag}': pool exhausted and cannot expand!");
#endif

                    return null;
                }
            }
        }

        // 오브젝트 활성화 및 설정
        objectToSpawn.SetActive(true);
        objectToSpawn.transform.SetParent(_parent == null ? this.transform : _parent);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;

        // 풀에 다시 추가 (순환 구조)
        poolDictionary[tag].Enqueue(objectToSpawn);

        if (showDebugLogs)
#if !PLAYABLE_AD
            Debug.Log($"Spawned '{objectToSpawn.name}' from pool '{tag}'. Available: {GetAvailableCount(tag)}");
#endif


        return objectToSpawn;
    }

    GameObject FindOldestActiveObject(string tag)
    {
        if (!allPoolObjects.ContainsKey(tag)) return null;

        // 활성화된 오브젝트 중 가장 오래된 것을 찾음
        List<GameObject> objects = allPoolObjects[tag];

        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i].activeInHierarchy)
            {
                // 오브젝트를 비활성화하여 재사용 준비
                objects[i].SetActive(false);
                return objects[i];
            }
        }

        return null;
    }
    public List<GameObject> FindActiveObjects(string _tag)
    {
        if (!allPoolObjects.ContainsKey(tag)) return null;

        List<GameObject> _objects = new List<GameObject>();

        for(int i= 0; i < allPoolObjects[_tag].Count; i++)
        {
            if(allPoolObjects[_tag][i].activeInHierarchy)
            {
                _objects.Add(allPoolObjects[_tag][i]);
            }
        }

        return _objects;
    }
    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);

        if (showDebugLogs)
#if !PLAYABLE_AD
            Debug.Log($"Returned '{obj.name}' to pool");
#endif

    }

    // 풀 상태 확인 메서드들
    public int GetAvailableCount(string tag)
    {
        if (!poolDictionary.ContainsKey(tag)) return 0;

        int availableCount = 0;
        foreach (GameObject obj in allPoolObjects[tag])
        {
            if (!obj.activeInHierarchy)
                availableCount++;
        }
        return availableCount;
    }

    public int GetActiveCount(string tag)
    {
        if (!allPoolObjects.ContainsKey(tag)) return 0;

        int activeCount = 0;
        foreach (GameObject obj in allPoolObjects[tag])
        {
            if (obj.activeInHierarchy)
                activeCount++;
        }
        return activeCount;
    }

    public int GetTotalCount(string tag)
    {
        if (!allPoolObjects.ContainsKey(tag)) return 0;
        return allPoolObjects[tag].Count;
    }

}