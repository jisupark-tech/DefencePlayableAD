using System.Collections;
using UnityEngine;

public enum AnimationType
{
    None = -1,
    Stack = 0,
    Drop = 1,
    Fly = 2,
    TurretStack = 3,
}

public class GoldPickup : MonoBehaviour
{
    [Header("Gold Settings")]
    public float flySpeed = 10f;
    public float pickupRange = 1.5f;
    public float delayBeforeFly = 0.5f;
    public float rotationSpeed = 90f; // 회전 속도
    [Header("Fly to Player Speed")]
    public float GoldAccerate = 1.1f;
    [Header("Fly to Target Speed")]
    public float TargetAccerate = 15f;

    [Header("Animation and the other thing")]
    public bool SetFlytoPlayer = false;
    public bool HorizontalStand = false;
    public bool PatternizeCoinArray = false;

    [Header("Parabolic Arc Settings")]
    public bool UseParabolicArc = true; // 포물선 이동 사용 여부
    public float arcHeight = 2.0f; // 포물선의 최대 높이
    public float arcDuration = 1.0f; // 포물선 이동 시간
    public AnimationCurve arcCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 가속도 곡선

    [Header("Bounce Animation Settings")]
    public int bounceCount = 3; // 바운스 횟수
    public float maxBounceHeight = 1.0f; // 첫 번째 바운스 최대 높이
    public float bounceDecayRate = 0.6f; // 바운스 높이 감소율

    [Header("GoldBody")]
    public GameObject body;
    private int goldValue;
    private Transform player;

    public void Initialize(int value, AnimationType _type = AnimationType.None, Transform _target = null)
    {
        player = GameManager.Instance.m_Player.transform;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player").transform;

        if (_type == AnimationType.Stack)
        {

            transform.localRotation = Quaternion.Euler(0, 0, 90);

            return;
        }

        if (_type == AnimationType.TurretStack)
        {
            return;
        }

        goldValue = value;

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(RotateGold());
            if (_type == AnimationType.Fly && _target != null)
            {
                // 포물선 이동 사용 여부에 따라 메서드 선택
                if (UseParabolicArc)
                {
                    StartCoroutine(FlyToTargetWithArc(_target));
                }
                else
                {
                    StartCoroutine(FlyToTarget(_target));
                }
            }
            else
            {
                if (SetFlytoPlayer && player != null)
                {
                    if (UseParabolicArc)
                    {
                        StartCoroutine(FlyToTargetWithArc(player));
                    }
                    else
                    {
                        StartCoroutine(FlyToPlayer());
                    }
                }
                else
                    StartCoroutine(DropDownInPlace());
            }
        }
    }

    IEnumerator RotateGold()
    {
        // 골드가 활성화되어 있는 동안 계속 회전
        while (gameObject.activeInHierarchy)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
            yield return null;
        }
    }

    IEnumerator DropDownInPlace()
    {
        Vector3 startPosition = transform.position;

        if (HorizontalStand)
        {
            transform.rotation = Quaternion.Euler(90, 0, 0);
            startPosition += new Vector3(0, 0.5f, 0);
            transform.position = startPosition;
        }

        // 바운싱 애니메이션 추가
        yield return StartCoroutine(BouncingAnimation(startPosition));

        yield return new WaitForSeconds(delayBeforeFly);

        StartCoroutine(StartToSearchPlayerNear());
    }

    // 새로운 바운싱 애니메이션 메소드
    IEnumerator BouncingAnimation(Vector3 groundPosition)
    {
        for (int i = 0; i < bounceCount; i++)
        {
            // 각 바운스의 높이 계산 (점점 낮아짐)
            float currentBounceHeight = maxBounceHeight * Mathf.Pow(bounceDecayRate, i);

            // 바운스 지속시간 (점점 짧아짐)
            float currentBounceDuration = 0.4f * (1f - i * 0.1f);

            // 올라가는 애니메이션 (가속도 적용)
            yield return StartCoroutine(BounceUp(
                transform.position,
                groundPosition + Vector3.up * currentBounceHeight,
                currentBounceDuration * 0.4f
            ));

            // 내려가는 애니메이션 (중력 효과)
            yield return StartCoroutine(BounceDown(
                transform.position,
                groundPosition,
                currentBounceDuration * 0.6f
            ));
        }
    }

    // 올라가는 모션 (감속 곡선)
    IEnumerator BounceUp(Vector3 startPos, Vector3 targetPos, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease-out 곡선 (위로 갈수록 느려짐)
            t = 1f - Mathf.Pow(1f - t, 3f);

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
    }

    // 내려가는 모션 (가속 곡선)
    IEnumerator BounceDown(Vector3 startPos, Vector3 targetPos, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease-in 곡선 (아래로 갈수록 빨라짐, 중력 효과)
            t = t * t * t;

            transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        transform.position = targetPos;
    }

    IEnumerator FlyToPlayer()
    {
        // 잠시 대기 후 플레이어로 날아감
        yield return new WaitForSeconds(delayBeforeFly);

        if (player == null)
        {
            ObjectPool.Instance.ReturnToPool(gameObject);
            yield break;
        }

        // 플레이어를 향해 날아감
        //TODO 2025-12-02
        // 플레이어의 등에 있는 동전 더미의 꼭대기
        while (Vector3.Distance(transform.position, player.position) < pickupRange && gameObject.activeInHierarchy)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            transform.position += direction * flySpeed * Time.deltaTime;

            // 속도 점진적으로 증가 (자석 효과)
            flySpeed += Time.deltaTime * GoldAccerate;

            yield return null;
        }

        // 플레이어에게 골드 지급
        GameManager.Instance?.AddGold(goldValue);

        // 오브젝트 풀로 반환
        ObjectPool.Instance.ReturnToPool(gameObject);

        AudioManager.Instance.PlayGoldCollectSound();
    }

    IEnumerator StartToSearchPlayerNear()
    {
        while (gameObject.activeInHierarchy)
        {
            if (Vector3.Distance(transform.position, player.position) <= pickupRange)
            {
                // 플레이어가 범위 안에 들어오면 플레이어에게 날아가기 시작
                StartCoroutine(FlyToTargetWithArc(player));
                yield break;
            }
            yield return null; // 매 프레임마다 거리 체크
        }
    }
#if !PLAYABLE_AD
    IEnumerator FlyToPlayerFromNear()
    {
        if (player == null)
        {
            ObjectPool.Instance.ReturnToPool(gameObject);
            yield break;
        }

        // 플레이어를 향해 날아감
        while (Vector3.Distance(transform.position, player.position) > 0.1f && gameObject.activeInHierarchy)
        {
            Vector3 direction = (player.position - transform.position).normalized;
            transform.position += direction * flySpeed * Time.deltaTime;

            // 속도 점진적으로 증가 (자석 효과)
            flySpeed += Time.deltaTime * 5f;

            yield return null;
        }

        // 플레이어에게 골드 지급
        GameManager.Instance?.AddGold(goldValue);

        // 오브젝트 풀로 반환
        ObjectPool.Instance.ReturnToPool(gameObject);

        AudioManager.Instance.PlayGoldCollectSound();
    }
#endif


    // Y축에만 Arc를 적용한 포물선 이동 메서드
    IEnumerator FlyToTargetWithArc(Transform target)
    {
        if (target == null)
        {
            ObjectPool.Instance.ReturnToPool(gameObject);
            yield break;
        }

        Vector3 startPosition = transform.position;
        //TODO 2025-12-09
        //플레이어의 경우 골드 스텍 쌓여있는 상단에
        Vector3 targetPosition = Vector3.zero;
        if (IsTargetPlayer(target))
        {
            PlayerController _player = target.gameObject.GetComponent<PlayerController>();
            if (_player != null)
            {
                targetPosition = _player.GetGoldStackTopPos().localPosition;
            }
        }
        else
            targetPosition = target.position;

        float elapsed = 0f;

        // X, Z축은 직선 이동을 위한 시작 위치 저장
        Vector3 startXZ = new Vector3(startPosition.x, 0, startPosition.z);
        float startY = startPosition.y;

        while (elapsed < arcDuration && gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / arcDuration;

            // 타겟이 움직일 수 있으므로 실시간으로 목표 위치 업데이트
            targetPosition = IsTargetPlayer(target) ? target.GetComponent<PlayerController>().GetGoldStackTopPos().position :target.position;
            Vector3 targetXZ = new Vector3(targetPosition.x, 0, targetPosition.z);

            // X, Z축은 선형 보간 (직선 이동)
            Vector3 currentXZ = Vector3.Lerp(startXZ, targetXZ, t);

            // Y축에만 arcCurve 적용
            float arcValue = arcCurve.Evaluate(t);
            float currentY = Mathf.Lerp(startY, targetPosition.y, t) + (arcValue * arcHeight);

            // 최종 위치 설정
            Vector3 currentPos = new Vector3(currentXZ.x, currentY, currentXZ.z);
            transform.position = currentPos;

            yield return null;
        }

        // 최종 위치로 이동
        transform.position = targetPosition;

        // 핵심 수정: 타겟이 플레이어인지 확인하고 골드 처리
        if (IsTargetPlayer(target))
        {
            // 플레이어에게 골드 지급 (GameManager 골드 증가)
            GameManager.Instance?.AddGold(goldValue);
        }
        else
        {
            ICollectable _collect = target.GetComponent<ICollectable>();
            if (_collect != null)
            {
                _collect.CollectGold();
            }
        }

        // 오브젝트 풀로 반환
        ObjectPool.Instance.ReturnToPool(gameObject);
        AudioManager.Instance.PlayGoldCollectSound();
    }

    // 기존 직선 이동 메서드 (그대로 유지)
    IEnumerator FlyToTarget(Transform target)
    {
        // 즉시 타겟으로 날아감 (터렛 건설용 등)
        while (Vector3.Distance(transform.position, target.position) > 0.5f && gameObject.activeInHierarchy)
        {
            //각도를 상단에 지역번수로 선언(각도는 45도 정도)
            //Sin값을 이용하여 높이를 추가
            //중간지점(지속적으로 계산해야듯,캐릭터가 움직일수있기때문에)
            //1구간(증가)
            //Ease.InAndOut? 사용해서 곡선 생성(가속치)
            //1구간 끝에 도달하면 속도를 0으로
            //그리고 2구간에 갔을때, 목표지점으로 떨어지게
            //SIn값이 -여야하므로 중간지점을 계산해서 중간지점을 넘으면, 해당 코인 Sin Y값을 마이너스로 전환해서
            //Target의 Y포즈까지

            //float _radians = angle * Mathf.Deg2Rad;
            //float _initialVelocityY = flySpeed * Mathf.Sin(_radians);

            Vector3 direction = (target.position - transform.position).normalized;
            transform.position += direction * flySpeed * Time.deltaTime;

            // 속도 점진적으로 증가
            flySpeed += Time.deltaTime * TargetAccerate;

            yield return null;
        }

        // 핵심 수정: 타겟이 플레이어인지 확인하고 골드 처리
        if (IsTargetPlayer(target))
        {
            // 플레이어에게 골드 지급 (GameManager 골드 증가)
            GameManager.Instance?.AddGold(goldValue);
        }
        else
        {
            ICollectable _collect = target.GetComponent<ICollectable>();
            if (_collect != null)
            {
                _collect.CollectGold();
            }
        }
        // 오브젝트 풀로 반환
        ObjectPool.Instance.ReturnToPool(gameObject);
        AudioManager.Instance.PlayGoldCollectSound();
    }

    // 타겟이 플레이어인지 확인하는 메소드
    bool IsTargetPlayer(Transform target)
    {
        if (target == null) return false;

        // PlayerController 컴포넌트가 있는지 확인
        PlayerController playerController = target.GetComponent<PlayerController>();
        if (playerController != null) return true;

        // 플레이어 태그 확인
        if (target.CompareTag("Player")) return true;

        // GameManager의 플레이어와 비교
        if (GameManager.Instance != null && GameManager.Instance.m_Player != null)
        {
            return target == GameManager.Instance.m_Player.transform;
        }

        return false;
    }
}