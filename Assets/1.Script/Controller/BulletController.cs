using UnityEngine;
using System.Collections;

public enum BulletOwner
{
    Player,
    Turret
}

public class BulletController : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 20f;
    //[SerializeField] private Material lineMaterial;
    [SerializeField] private AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private Color bulletColor = Color.yellow;
    [SerializeField] private float fadeDuration = 0.2f;

    private Transform m_Target;
    private BulletOwner m_Owner = BulletOwner.Player;
    private TurretController m_OwnerTurret;
    //private LineRenderer lineRenderer;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isInitialized = false;

    void SetupLineRenderer()
    {
        //if (lineRenderer == null)
        //    lineRenderer = GetComponent<LineRenderer>();
        //if (lineRenderer == null)
        //    lineRenderer = GetComponentInChildren<LineRenderer>();

        //lineRenderer.positionCount = 2;
        //lineRenderer.startWidth = lineWidth;
        //lineRenderer.endWidth = lineWidth * 0.1f;
        //lineRenderer.widthCurve = widthCurve;
        //lineRenderer.useWorldSpace = true;
        //lineRenderer.sortingOrder = 1;

        //if (lineMaterial != null)
        //{
        //    lineRenderer.material = lineMaterial;
        //}
        //else
        //{
        //    lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        //}

        //lineRenderer.startColor = bulletColor;
        //lineRenderer.endColor = bulletColor;
        //lineRenderer.enabled = false;
    }

    void OnEnable()
    {
        SetupLineRenderer();

        m_Target = null;
        m_Owner = BulletOwner.Player;
        m_OwnerTurret = null;
        isInitialized = false;

        //if (lineRenderer != null)
        //{
        //    lineRenderer.enabled = false;
        //}
    }

    public void SetTarget(Transform _target, BulletOwner _owner, TurretController _ownerTurret = null)
    {
        m_Target = _target;
        m_Owner = _owner;
        m_OwnerTurret = _ownerTurret;

        if (m_Target != null)
        {
            startPosition = transform.position;
            targetPosition = m_Target.position;

            // Rigidbody 예측 사격 제거 - 단순히 현재 위치로 조준
            // 예측 사격이 필요하다면 MovementComponent의 velocity 사용
            MovementComponent targetMovement = m_Target.GetComponent<MovementComponent>();
            if (targetMovement != null && targetMovement.IsMoving())
            {
                Vector3 targetVelocity = targetMovement.GetCurrentVelocity();
                float timeToHit = Vector3.Distance(startPosition, targetPosition) / speed;
                targetPosition += targetVelocity * timeToHit * 0.3f; // 약간의 예측
            }

            isInitialized = true;
            //lineRenderer.enabled = true;

            StartCoroutine(AnimateBullet());
        }
    }

    IEnumerator AnimateBullet()
    {
        if (!isInitialized || m_Target == null)
        {
            ReturnToPool();
            yield break;
        }

        float journeyLength = Vector3.Distance(startPosition, targetPosition);
        float elapsedTime = 0f;
        float totalTime = journeyLength / speed;

        while (elapsedTime < totalTime)
        {
            if (m_Target == null || !m_Target.gameObject.activeInHierarchy)
            {
                StartCoroutine(FadeOutAndReturn());
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / totalTime;

            Vector3 currentBulletPos = Vector3.Lerp(startPosition, targetPosition, progress);
            transform.position = currentBulletPos;

            Vector3 dir = (targetPosition - startPosition).normalized;
            if (dir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }

            //if (lineRenderer != null)
            //{
            //    Vector3 tailStart = currentBulletPos - dir * trailLength;
            //    Vector3 tailEnd = currentBulletPos;

            //    lineRenderer.SetPosition(0, tailStart);
            //    lineRenderer.SetPosition(1, tailEnd);

            //    Color currentColor = bulletColor;
            //    currentColor.a = Mathf.Lerp(1f, 0.3f, progress);
            //    lineRenderer.startColor = currentColor;
            //    lineRenderer.endColor = currentColor;
            //}

            yield return null;
        }

        HitTarget();
    }

    void HitTarget()
    {
        if (m_Target != null)
        {
            EnemyController enemy = m_Target.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(1, m_Owner, m_OwnerTurret);
            }
        }

        StartCoroutine(FadeOutAndReturn());
    }

    IEnumerator FadeOutAndReturn()
    {
        float elapsedTime = 0f;
        //Color originalColor = lineRenderer.startColor;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            //float alpha = Mathf.Lerp(originalColor.a, 0f, elapsedTime / fadeDuration);

            //Color fadeColor = originalColor;
            //fadeColor.a = alpha;
            //lineRenderer.startColor = fadeColor;
            //lineRenderer.endColor = fadeColor;

            yield return null;
        }

        ReturnToPool();
    }

    void ReturnToPool()
    {
        //lineRenderer.enabled = false;
        //lineRenderer.startColor = bulletColor;
        //lineRenderer.endColor = bulletColor;
        ObjectPool.Instance.ReturnToPool(gameObject);
    }

#if !PLAYABLE_AD
    public void SetBulletColor(Color color)
    {
        bulletColor = color;
        //if (lineRenderer != null)
        //{
        //    lineRenderer.startColor = bulletColor;
        //    lineRenderer.endColor = bulletColor;
        //}
    }

    public void SetBulletWidth(float width)
    {
        //lineWidth = width;
        //if (lineRenderer != null)
        //{
        //    lineRenderer.startWidth = lineWidth;
        //    lineRenderer.endWidth = lineWidth * 0.1f;
        //}
    }

    public void SetBulletSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
#endif
}