using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoldStorage : MonoBehaviour
{
    [Header("Storage Settings")]
    public Transform storageCenter; // 골드가 쌓일 중심점
    public int maxGoldPerLayer = 4; // 한 층당 최대 골드 개수
    public float layerHeight = 0.3f; // 층 간격
    public float goldSpacing = 0.4f; // 같은 층 내 골드 간격

    [Header("Player Interaction")]
    public float playerDetectionRange = 1.5f; // 플레이어 감지 범위
    public float checkInterval = 0.1f; // 체크 간격
    public float intervalTime = 0.2f;
    private List<List<GoldPickup>> goldLayers = new List<List<GoldPickup>>();
    private int totalGoldCount = 0;
    private PlayerController playerController;

    public void OnEnable()
    {
        StartCoroutine(WaitForActivationAndStart());
    }
    public void Init()
    {
        if (storageCenter == null)
            storageCenter = transform;

        // 플레이어 참조 가져오기
        if (GameManager.Instance != null && GameManager.Instance.m_Player != null)
            playerController = GameManager.Instance.m_Player;

        // GameObject가 활성화되어 있을 때만 코루틴 시작
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(CheckPlayerNear());
        }
    }

    public void AddGold(int amount = 1)
    {
        for (int i = 0; i < amount; i++)
        {
            // 새 골드 생성
            GameObject goldObj = ObjectPool.Instance.SpawnFromPool("Gold", GetNextGoldPosition(), Quaternion.Euler(90, 0, 0), storageCenter);

            if (goldObj != null)
            {
                GoldPickup goldPickup = goldObj.GetComponent<GoldPickup>();
                goldPickup.Initialize(1, AnimationType.TurretStack);

                // 현재 층 찾기 또는 새 층 생성
                int currentLayer = totalGoldCount / maxGoldPerLayer;

                // 필요하면 새 층 추가
                while (goldLayers.Count <= currentLayer)
                {
                    goldLayers.Add(new List<GoldPickup>());
                }

                goldLayers[currentLayer].Add(goldPickup);
                totalGoldCount++;
            }
        }
    }

    public bool RemoveGold(int amount = 1)
    {
        if (totalGoldCount < amount)
            return false;

        for (int i = 0; i < amount; i++)
        {
            if (goldLayers.Count == 0)
                break;

            // 맨 위층부터 제거
            int topLayer = goldLayers.Count - 1;
            if (goldLayers[topLayer].Count > 0)
            {
                GoldPickup goldToRemove = goldLayers[topLayer][goldLayers[topLayer].Count - 1];
                goldLayers[topLayer].RemoveAt(goldLayers[topLayer].Count - 1);

                ObjectPool.Instance.ReturnToPool(goldToRemove.gameObject);
                totalGoldCount--;

                // 빈 층 제거
                if (goldLayers[topLayer].Count == 0)
                {
                    goldLayers.RemoveAt(topLayer);
                }
            }
        }

        return true;
    }

    public int GetStoredGoldCount()
    {
        return totalGoldCount;
    }

    public void TransferGoldTo(Transform target, int amount = 1)
    {
        StartCoroutine(TransferGoldWithInterval(amount, target));  
    }
    public IEnumerator TransferGoldWithInterval(int amount, Transform target)
    {
        int transferAmount = Mathf.Min(amount, totalGoldCount);

        for (int i = 0; i < transferAmount; i++)
        {
            if (goldLayers.Count == 0)
                break;

            // 맨 위층에서 골드 가져오기
            int topLayer = goldLayers.Count - 1;
            if (goldLayers[topLayer].Count > 0)
            {
                GoldPickup goldToTransfer = goldLayers[topLayer][goldLayers[topLayer].Count - 1];
                goldLayers[topLayer].RemoveAt(goldLayers[topLayer].Count - 1);
                totalGoldCount--;

                // 타겟으로 날아가는 애니메이션
                goldToTransfer.Initialize(1, AnimationType.Fly, target);

                // 빈 층 제거
                if (goldLayers[topLayer].Count == 0)
                {
                    goldLayers.RemoveAt(topLayer);
                }

                yield return new WaitForSeconds(intervalTime);
            }
        }
    }
    //TODO 2025-12-02
    //마이너스로 되는 Y 값의 좌표 위치 수정
    Vector3 GetNextGoldPosition()
    {
        int currentLayer = totalGoldCount / maxGoldPerLayer;
        int positionInLayer = totalGoldCount % maxGoldPerLayer;

        // 층별 높이 계산
        float yPosition = storageCenter.position.y + (currentLayer * layerHeight);

        // 같은 층 내에서 위치 계산 (사각형 배치)
        Vector3 basePosition = storageCenter.position;
        basePosition.y = yPosition;
#if !PLAYABLE_AD
        Debug.Log($"=====GoldStrorage() positionInLayer : {positionInLayer} currentLayer : {currentLayer}  y : {yPosition}");
#endif

        switch (positionInLayer)
        {
            case 0: return basePosition + new Vector3(-goldSpacing / 2, 0.1f, -goldSpacing / 2);
            case 1: return basePosition + new Vector3(goldSpacing / 2, 0.1f - goldSpacing / 2);
            case 2: return basePosition + new Vector3(goldSpacing / 2, 0.1f, goldSpacing / 2);
            case 3: return basePosition + new Vector3(-goldSpacing / 2, 0.1f, goldSpacing / 2);
            default: return basePosition;
        }
    }

    // GameObject가 비활성화된 상태에서 Init()이 호출된 경우 활성화를 기다리는 코루틴
    IEnumerator WaitForActivationAndStart()
    {
        // GameObject가 활성화될 때까지 대기
        while (!gameObject.activeInHierarchy)
        {
            yield return new WaitForSeconds(0.1f);
        }

        // 활성화되면 체크 코루틴 시작
        StartCoroutine(CheckPlayerNear());
    }

    // 플레이어가 가까이 오면 골드를 자동으로 전송
    IEnumerator CheckPlayerNear()
    {
        while (true)
        {
            // 골드가 있고 플레이어가 존재할 때만 체크
            if (totalGoldCount > 0 && playerController != null)
            {
                float distance = Vector3.Distance(playerController.transform.position, transform.position);

                if (distance <= playerDetectionRange)
                {
                    // 모든 골드를 플레이어에게 전송
                    int goldToTransfer = totalGoldCount;
                    TransferGoldTo(playerController.transform, goldToTransfer);

                    // 골드 전송 완료 후 잠깐 대기 (연속 전송 방지)
                    yield return new WaitForSeconds(0.5f);
                }
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }
#if !PLAYABLE_AD
    // 디버그용 메소드들
    public void SetPlayerDetectionRange(float range)
    {
        playerDetectionRange = Mathf.Max(0f, range);
    }

    void OnDrawGizmosSelected()
    {
        // 플레이어 감지 범위 표시
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, playerDetectionRange);

        // 골드 저장소 중심점 표시
        if (storageCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(storageCenter.position, Vector3.one * 0.5f);
        }
    }
#endif

}