using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class VirtualPad : MonoBehaviour
{
    [Header("Pad Settings")]
    public RectTransform padTransform;     // Pad 이미지 (조작용)
    [Range(0.05f, 0.3f)]
    public float moveRangeRatio = 0.15f;   // 화면 크기 대비 비율
    public float returnSpeed = 5f;         // 패드 복귀 속도

    [Header("Dynamic Pad Settings")]
    public bool enableDynamicPosition = true;  // 동적 위치 생성 활성화
    public float padFadeSpeed = 10f;           // 패드 나타나기/사라지기 속도

    [Header("Universal Scaling (Optimized)")]
    public bool useUniversalScaling = true;    // 통합 스케일링 사용
    public float targetSizeInInches = 0.8f;    // 목표 물리적 크기 (인치)
    public bool debugMode = false;             // 디버그 정보 출력
    [Header("OverLay Setting")]
    public RectTransform OverLayTr;
    [Header("Auto Setup")]
    private RectTransform bgTransform;         // BG 이미지 (흰 원 배경)
    private Vector2 initialPadPosition;        // 패드의 초기 위치
    private Vector2 initialBgPosition;         // BG의 초기 위치
    private Vector2 dynamicPadCenter;          // 동적 패드의 중심 위치
    private Vector2 inputDirection = Vector2.zero;
    private Canvas parentCanvas;
    private bool isDragging = false;
    private bool isActive = false;             // 패드 활성화 상태

    // 최적화된 스케일링 시스템
    private float finalMoveRange;              // 최종 계산된 moveRange (캐시됨)
    private float universalScaleFactor = 1f;  // 통합 스케일 팩터 (캐시됨)
    private bool scaleCalculated = false;      // 스케일 계산 완료 플래그

    // 플랫폼별 최적화 상수들 (컴파일 타임에 결정)
#if UNITY_ANDROID
        private const float PLATFORM_DPI = 320f;
        private const float PLATFORM_SCALE_MODIFIER = 1.0f;
#elif UNITY_IOS
        private const float PLATFORM_DPI = 326f;
        private const float PLATFORM_SCALE_MODIFIER = 1.0f;
#elif PLAYABLE_AD
    private const float PLATFORM_DPI = 96f;
    private const float PLATFORM_SCALE_MODIFIER = 1.0f;
#else
        private const float PLATFORM_DPI = 96f;
        private const float PLATFORM_SCALE_MODIFIER = 1.1f; // PC는 약간 크게
#endif

    private const float REFERENCE_DPI = 160f;  // 기준 DPI
    private const float MIN_MOVE_RANGE = 20f;  // 최소 이동 범위
    private const float MAX_MOVE_RANGE = 500f; // 최대 이동 범위

    // 터치 감지용 투명 오버레이
    private GameObject touchOverlay;
    private RectTransform touchOverlayRect;
    private PadEventHandler padEventHandler;

    // UI 컴포넌트들 (VirtualPad 전체 제어용)
    private CanvasGroup virtualPadCanvasGroup;

    // 최적화: 코루틴 캐싱
    private Coroutine fadeCoroutine;

    // 최적화: 자주 사용되는 계산 캐싱
    private Vector2 cachedPadOffset;
    private bool offsetCached = false;

    void Awake()
    {
        // 자동으로 자식 오브젝트들 찾기
        if (padTransform == null)
        {
            Transform padChild = transform.Find("Pad");
            if (padChild != null)
                padTransform = padChild.GetComponent<RectTransform>();
        }

        Transform bgChild = transform.Find("BG");
        if (bgChild != null)
            bgTransform = bgChild.GetComponent<RectTransform>();

        // VirtualPad 전체에 CanvasGroup 설정 (BG와 Pad 함께 제어)
        SetupCanvasGroup();
    }

    void SetupCanvasGroup()
    {
        // VirtualPad 전체에 CanvasGroup 추가
        virtualPadCanvasGroup = GetComponent<CanvasGroup>();
        if (virtualPadCanvasGroup == null)
        {
            virtualPadCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();

        // 통합 스케일링 계산 (한 번만 실행)
        CalculateUniversalScale();

        // 초기 위치들 저장 (BG와 Pad 모두)
        if (padTransform != null)
            initialPadPosition = padTransform.anchoredPosition;

        if (bgTransform != null)
            initialBgPosition = bgTransform.anchoredPosition;

        // 패드 오프셋 캐싱 (최적화)
        CachePadOffset();

        // 처음에는 패드 전체를 숨김
        SetPadVisibility(false, true);

        // 전체 화면 터치 감지용 오버레이 생성
        CreateTouchOverlay();

        // 디버그 정보 출력
        if (debugMode)
        {
#if !PLAYABLE_AD
            LogOptimizedDeviceInfo();
#endif

        }
    }

    /// <summary>
    /// 통합 스케일링 계산 (최적화됨 - 한 번만 실행)
    /// </summary>
    void CalculateUniversalScale()
    {
        if (scaleCalculated) return; // 이미 계산됨

        if (useUniversalScaling)
        {
            // 물리적 크기 기반 통합 스케일링
            float deviceDPI = Screen.dpi > 0 ? Screen.dpi : PLATFORM_DPI;
            float canvasScale = parentCanvas ? parentCanvas.scaleFactor : 1f;

            // 물리적 크기를 픽셀로 변환
            float targetPixels = targetSizeInInches * deviceDPI;

            // Canvas Scaler 보정 적용
            CanvasScaler scaler = parentCanvas?.GetComponent<CanvasScaler>();
            if (scaler && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                // Reference Resolution 기반 보정
                Vector2 refRes = scaler.referenceResolution;
                float refSize = Mathf.Min(refRes.x, refRes.y);
                targetPixels = refSize * moveRangeRatio * canvasScale;
            }

            // 플랫폼별 미세 조정
            targetPixels *= PLATFORM_SCALE_MODIFIER;

            // 최종 범위 계산
            finalMoveRange = Mathf.Clamp(targetPixels, MIN_MOVE_RANGE, MAX_MOVE_RANGE);
            universalScaleFactor = finalMoveRange / (targetSizeInInches * REFERENCE_DPI);
        }
        else
        {
            // 기존 방식 (Canvas 기준)
            CanvasScaler canvasScaler = parentCanvas?.GetComponent<CanvasScaler>();
            RectTransform canvasRect = parentCanvas?.GetComponent<RectTransform>();

            if (canvasScaler && canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                Vector2 referenceResolution = canvasScaler.referenceResolution;
                float referenceSize = Mathf.Min(referenceResolution.x, referenceResolution.y);
                finalMoveRange = referenceSize * moveRangeRatio;
            }
            else if (canvasRect)
            {
                float currentSize = Mathf.Min(canvasRect.rect.width, canvasRect.rect.height);
                finalMoveRange = currentSize * moveRangeRatio;
            }
            else
            {
                finalMoveRange = Screen.height * moveRangeRatio;
            }

            finalMoveRange = Mathf.Clamp(finalMoveRange, MIN_MOVE_RANGE, MAX_MOVE_RANGE);
            universalScaleFactor = 1f;
        }

        scaleCalculated = true;
    }

    /// <summary>
    /// 패드 오프셋 캐싱 (최적화)
    /// </summary>
    void CachePadOffset()
    {
        if (padTransform && bgTransform && !offsetCached)
        {
            cachedPadOffset = initialPadPosition - initialBgPosition;
            offsetCached = true;
        }
    }

    /// <summary>
    /// 최적화된 디바이스 정보 로그
    /// </summary>
    void LogOptimizedDeviceInfo()
    {
#if !PLAYABLE_AD
        Debug.Log("=== VirtualPad Optimized Info ===");
        Debug.Log($"Platform: {Application.platform}");
        Debug.Log($"Screen: {Screen.width}x{Screen.height}");
        Debug.Log($"DPI: {Screen.dpi} (Est: {PLATFORM_DPI})");
        Debug.Log($"Canvas Scale: {(parentCanvas ? parentCanvas.scaleFactor : 1f)}");
        Debug.Log($"Final Move Range: {finalMoveRange:F1}px");
        Debug.Log($"Universal Scale: {universalScaleFactor:F3}");
        Debug.Log($"Target Physical Size: {targetSizeInInches}\"");
        Debug.Log("===============================");
#endif

    }

    /// <summary>
    /// 전체 화면 터치 감지용 투명 오버레이 생성
    /// </summary>
    void CreateTouchOverlay()
    {
        // 터치 감지용 투명 오버레이 생성
        touchOverlay = new GameObject("TouchOverlay");
        touchOverlay.transform.SetParent(OverLayTr!=null? OverLayTr :parentCanvas.transform, false);

        // RectTransform 설정 (전체 화면)
        touchOverlayRect = touchOverlay.AddComponent<RectTransform>();
        touchOverlayRect.anchorMin = Vector2.zero;
        touchOverlayRect.anchorMax = Vector2.one;
        touchOverlayRect.offsetMin = Vector2.zero;
        touchOverlayRect.offsetMax = Vector2.zero;

        // 완전 투명한 이미지 추가 (터치 감지용)
        Image overlayImage = touchOverlay.AddComponent<Image>();
        overlayImage.color = new Color(0, 0, 0, 0); // 완전 투명
        overlayImage.raycastTarget = true; // 터치 감지 활성화

        // 이벤트 핸들러 추가
        padEventHandler = touchOverlay.AddComponent<PadEventHandler>();
        padEventHandler.Initialize(this);
    }

    void Update()
    {
        // 최적화: 필요한 경우에만 계산
        if (!isDragging && isActive && padTransform != null)
        {
            // 패드를 중앙으로 복귀 (시각적 효과만)
            Vector2 targetPadPosition = enableDynamicPosition ?
                (dynamicPadCenter + cachedPadOffset) :
                initialPadPosition;

            // 최적화: 거리 체크 후 Lerp 적용
            float distance = Vector2.Distance(padTransform.anchoredPosition, targetPadPosition);
            if (distance > 1f)
            {
                padTransform.anchoredPosition = Vector2.Lerp(
                    padTransform.anchoredPosition,
                    targetPadPosition,
                    returnSpeed * Time.deltaTime
                );
            }
            else
            {
                padTransform.anchoredPosition = targetPadPosition;
            }
        }

        // 최적화: alpha 값 체크를 줄임
        if (!isActive && virtualPadCanvasGroup != null && virtualPadCanvasGroup.alpha <= 0.01f)
        {
            SetPadVisibility(false, true);
        }
    }

    /// <summary>
    /// VirtualPad 전체의 가시성을 부드럽게 또는 즉시 설정 (최적화됨)
    /// </summary>
    void SetPadVisibility(bool visible, bool immediate = false)
    {
        if (immediate)
        {
            // 기존 페이드 코루틴 중단
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            if (virtualPadCanvasGroup != null)
                virtualPadCanvasGroup.alpha = visible ? 1f : 0f;
        }
        else
        {
            // 기존 페이드 코루틴이 있다면 중단
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadePad(visible));
        }

        // 상호작용 가능 여부 설정 (BG와 Pad 함께)
        if (virtualPadCanvasGroup != null)
        {
            virtualPadCanvasGroup.interactable = visible;
            virtualPadCanvasGroup.blocksRaycasts = visible;
        }
    }

    /// <summary>
    /// VirtualPad 페이드 인/아웃 애니메이션 (최적화됨)
    /// </summary>
    IEnumerator FadePad(bool fadeIn)
    {
        if (virtualPadCanvasGroup == null)
        {
            fadeCoroutine = null;
            yield break;
        }

        float targetAlpha = fadeIn ? 1f : 0f;
        float startAlpha = virtualPadCanvasGroup.alpha;
        float fadeDuration = 1f / padFadeSpeed;
        float elapsedTime = 0f;

        // 최적화: 변화량이 작으면 즉시 완료
        if (Mathf.Abs(targetAlpha - startAlpha) < 0.01f)
        {
            virtualPadCanvasGroup.alpha = targetAlpha;
            fadeCoroutine = null;
            yield break;
        }

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeDuration;

            virtualPadCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        virtualPadCanvasGroup.alpha = targetAlpha;
        fadeCoroutine = null; // 코루틴 완료 표시
    }

    // Touch Overlay 이벤트 핸들러에서 호출되는 함수들
    public void OnPadPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        isActive = true;

        if (enableDynamicPosition)
        {
            // 터치한 위치를 새로운 VirtualPad 중심으로 설정
            SetDynamicPadCenter(eventData);
        }

        // VirtualPad 전체 표시
        SetPadVisibility(true);

        // 패드 위치 업데이트
        UpdatePadPosition(eventData);
    }

    public void OnPadPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        isActive = false;

        // 핵심: 즉시 정지 (관성 제거)
        inputDirection = Vector2.zero;

        // VirtualPad 전체 숨김
        SetPadVisibility(false);
    }

    public void OnPadDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            UpdatePadPosition(eventData);
        }
    }

    /// <summary>
    /// 동적 패드의 중심 위치를 터치 위치로 설정 (최적화됨)
    /// </summary>
    void SetDynamicPadCenter(PointerEventData eventData)
    {
        if (touchOverlayRect == null || bgTransform == null) return;

        Vector2 virtualPadLocalPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform,
            eventData.position,
            parentCanvas.renderMode == RenderMode.ScreenSpaceCamera ? parentCanvas.worldCamera : null,
            out virtualPadLocalPoint))
        {
            // 동적 패드 중심을 터치 위치로 설정
            dynamicPadCenter = virtualPadLocalPoint;

            // BG를 해당 위치로 이동
            bgTransform.anchoredPosition = dynamicPadCenter;

            // Pad를 BG 중심에 배치 (캐시된 오프셋 사용)
            if (padTransform != null)
                padTransform.anchoredPosition = dynamicPadCenter + cachedPadOffset;
        }
    }

    /// <summary>
    /// 패드 위치 업데이트 (핵심 최적화 적용)
    /// </summary>
    void UpdatePadPosition(PointerEventData eventData)
    {
        if (touchOverlayRect == null || padTransform == null || !isDragging) return;

        Vector2 localPoint;

        // 전체 화면 기준으로 터치 위치 계산
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform,
            eventData.position,
            parentCanvas.renderMode == RenderMode.ScreenSpaceCamera ? parentCanvas.worldCamera : null,
            out localPoint))
        {
            // 패드 중심으로부터의 오프셋 계산
            Vector2 padCenter = enableDynamicPosition ? dynamicPadCenter : initialBgPosition;
            Vector2 offset = localPoint - padCenter;

            // 🔥 핵심: 통합 스케일링이 적용된 finalMoveRange 사용
            Vector2 clampedOffset = Vector2.ClampMagnitude(offset, finalMoveRange);

            // 패드 위치 업데이트 (캐시된 오프셋 사용)
            padTransform.anchoredPosition = padCenter + clampedOffset + cachedPadOffset;

            // 🔥 핵심: finalMoveRange로 정규화하여 모든 디바이스에서 동일한 입력값 보장
            inputDirection = clampedOffset / finalMoveRange;
        }
    }

    // 외부에서 호출할 함수들 (최적화됨)
    public Vector2 GetInputDirection() => inputDirection;
    public float GetInputMagnitude() => inputDirection.magnitude;
    public bool IsPressed() => isDragging;
    public bool IsActive() => isActive;
    public GameObject OnGettouchOverlay() => touchOverlay;
#if !PLAYABLE_AD
   /// <summary>
    /// 현재 적용된 실제 moveRange 값 반환
    /// </summary>
    public float GetActualMoveRange() => finalMoveRange;

    /// <summary>
    /// 현재 통합 스케일 팩터 반환
    /// </summary>
    public float GetUniversalScale() => universalScaleFactor;

    /// <summary>
    /// 패드 모드 전환 (동적/고정)
    /// </summary>
    public void SetDynamicMode(bool dynamic)
    {
        enableDynamicPosition = dynamic;

        if (!dynamic && !isDragging)
        {
            // 고정 모드로 전환 시 초기 위치로 복귀
            if (bgTransform != null)
                bgTransform.anchoredPosition = initialBgPosition;

            if (padTransform != null)
                padTransform.anchoredPosition = initialPadPosition;
        }
    }

    /// <summary>
    /// 강제 초기화 (외부에서 호출 가능)
    /// </summary>
    public void ResetPad()
    {
        isDragging = false;
        isActive = false;
        inputDirection = Vector2.zero;

        // 페이드 코루틴 중단
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        SetPadVisibility(false, true);

        // 원래 위치로 복귀
        if (bgTransform != null)
            bgTransform.anchoredPosition = initialBgPosition;

        if (padTransform != null)
            padTransform.anchoredPosition = initialPadPosition;
    }

    /// <summary>
    /// 패드 설정 조정 메서드들 (최적화됨)
    /// </summary>
    public void SetMoveRangeRatio(float ratio)
    {
        moveRangeRatio = Mathf.Clamp(ratio, 0.05f, 0.3f);
        scaleCalculated = false; // 재계산 플래그
        CalculateUniversalScale(); // 재계산
    }

    public void SetFadeSpeed(float speed)
    {
        padFadeSpeed = Mathf.Max(1f, speed);
    }

    public void SetPhysicalSize(float sizeInInches)
    {
        targetSizeInInches = Mathf.Clamp(sizeInInches, 0.3f, 2f);
        if (useUniversalScaling)
        {
            scaleCalculated = false; // 재계산 플래그
            CalculateUniversalScale(); // 재계산
        }
    }

    /// <summary>
    /// 런타임 중 스케일 모드 변경
    /// </summary>
    public void SetScaleMode(bool useUniversal)
    {
        useUniversalScaling = useUniversal;
        scaleCalculated = false; // 재계산 플래그
        CalculateUniversalScale(); // 재계산

        if (debugMode)
        {
#if !PLAYABLE_AD
            Debug.Log($"[VirtualPad] Scale mode: {(useUniversal ? "Universal" : "Traditional")}");
            Debug.Log($"[VirtualPad] New move range: {finalMoveRange:F1}px");
#endif

        }
    }

    /// <summary>
    /// 강제 스케일 재계산 (해상도 변경 시 등)
    /// </summary>
    public void ForceRecalculateScale()
    {
        scaleCalculated = false;
        CalculateUniversalScale();
        offsetCached = false;
        CachePadOffset();

        if (debugMode)
        {
            LogOptimizedDeviceInfo();
        }
    }
#endif
 
}