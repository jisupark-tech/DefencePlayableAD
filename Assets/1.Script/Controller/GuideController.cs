using UnityEngine;

public class GuideController : MonoBehaviour
{
    [Header("Line Settings")]
    [SerializeField] private LineRenderer _line;
    [SerializeField] private float _lineWidth = 0.15f;
    [SerializeField] private float _heightOffset = 0.2f;         // 살짝 띄워서 그리기

    [Header("Texture Settings")]
    [SerializeField] private float _tilingPerUnit = 1f;          // 길이당 타일링 배율
    [SerializeField] private float _scrollSpeed = 1f;            // 텍스처 흐르는 속도 (나중에 머티리얼 만들 때 사용)

    private Transform _from;
    private Transform _to;
    private bool _isActive;

    // 머티리얼 인스턴스 (한 줄당 하나, 광고용이라 부담 없다고 가정)
    private Material _materialInstance;
    private float _scrollOffset;

    private void Awake()
    {
        if (_line == null)
            _line = GetComponent<LineRenderer>();

        _line.positionCount = 2;
        _line.enabled = false;
    }

    private void OnEnable()
    {
        _isActive = false;
        _scrollOffset = 0f;
    }

    /// <summary>
    /// 라인 시작/끝 대상 설정
    /// </summary>
    public void Init(Transform from, Transform to)
    {
        _from = from;
        _to = to;

        if (_line == null)
            _line = GetComponent<LineRenderer>();

        if (_line == null || _from == null || _to == null)
        {
            ReleaseToPool();
            return;
        }

        _isActive = true;
        _line.enabled = true;
        _line.positionCount = 2;
        _line.startWidth = _lineWidth;
        _line.endWidth = _lineWidth;

        // 머티리얼 인스턴스 확보 (타일링/오프셋 변경용)
        if (_materialInstance == null && _line.material != null)
        {
            // 광고용이라 여기서는 material 인스턴스 사용
            _materialInstance = _line.material;
        }

        UpdateLine(force: true);
    }

    private void Update()
    {
        if (!_isActive)
            return;

        // 대상이 사라졌거나 비활성화되면 가이드 반환
        if (_from == null || _to == null
            || !_from.gameObject.activeInHierarchy
            || !_to.gameObject.activeInHierarchy)
        {
            ReleaseToPool();
            return;
        }

        UpdateLine();
        UpdateTextureAnimation();
    }

    private void UpdateLine(bool force = false)
    {
        if (_line == null)
            return;

        Vector3 fromPos = _from.position + Vector3.up * _heightOffset;
        Vector3 toPos = _to.position + Vector3.up * _heightOffset;

        _line.SetPosition(0, fromPos);
        _line.SetPosition(1, toPos);

        // 길이에 따라 타일링 조정 (나중에 삼각형 텍스처 반복용)
        if (_materialInstance != null)
        {
            float dist = Vector3.Distance(fromPos, toPos);
            float tiling = dist * _tilingPerUnit;

            Vector2 scale = _materialInstance.mainTextureScale;
            scale.x = tiling;
            _materialInstance.mainTextureScale = scale;
        }
    }

    private void UpdateTextureAnimation()
    {
        if (_materialInstance == null || Mathf.Approximately(_scrollSpeed, 0f))
            return;

        _scrollOffset += Time.deltaTime * _scrollSpeed;

        Vector2 offset = _materialInstance.mainTextureOffset;
        offset.x = _scrollOffset;
        _materialInstance.mainTextureOffset = offset;
    }

    private void ReleaseToPool()
    {
        _isActive = false;

        if (_line != null)
        {
            _line.enabled = false;
        }

        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.ReturnToPool(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 외부에서 강제로 가이드 종료할 때 호출
    /// </summary>
    public void StopGuide()
    {
        ReleaseToPool();
    }
}
