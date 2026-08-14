using System.Collections;
using UnityEngine;

/// <summary>
/// 적 Visual과 분리된 발밑 원형 표시로 논리 결과의 시야 방향을 연출 순서에 맞춰 보여준다.
/// </summary>
public class EnemyFacingIndicatorPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Target")]
    // 방향 표시를 적용할 적 Context다.
    [SerializeField] private EnemyContext targetEnemy;
    // 적의 화면 위치와 플레이어 시야 알파를 제공하는 Visual 컴포넌트다.
    [SerializeField] private ActorVisualController visualController;

    [Header("Layout")]
    // VisualRoot의 월드 위치를 기준으로 적용할 발밑 표시 오프셋이다.
    [SerializeField] private Vector3 worldPositionOffset = new(0f, -0.32f, -0.05f);
    // 원형 표시의 월드 기준 반지름이다.
    [SerializeField] private float radius = 0.42f;
    // 회색 기본 원의 월드 기준 선 굵기다.
    [SerializeField] private float baseLineWidth = 0.055f;
    // 주황색 방향 호의 월드 기준 선 굵기다.
    [SerializeField] private float directionLineWidth = 0.085f;
    // 주황색 방향 호가 차지하는 각도다.
    [SerializeField] private float directionArcDegrees = 90f;
    // 전체 원을 구성할 선분 수다.
    [SerializeField] private int segmentCount = 48;

    [Header("Turn Presentation")]
    // 시야 방향이 달라졌을 때 제자리 회전에 사용할 시간이다.
    [SerializeField] private float turnDuration = 0.18f;
    // 시야 방향 회전 진행률을 실제 각도 보간률로 바꾸는 곡선이다.
    [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Color")]
    // 방향이 칠해지지 않은 기본 원의 색이다.
    [SerializeField] private Color baseColor = new(0.18f, 0.2f, 0.22f, 0.72f);
    // 현재 시야 방향을 나타내는 호의 색이다.
    [SerializeField] private Color directionColor = new(1f, 0.42f, 0.08f, 0.95f);

    [Header("Sorting")]
    // 적 SpriteRenderer보다 낮게 표시하기 위한 정렬 순서 차이다.
    [SerializeField] private int sortingOrderOffset = -1;

    // 두 LineRenderer를 이동시킬 런타임 표시 루트다.
    private GameObject runtimeIndicatorRoot;
    // 방향이 칠해지지 않은 전체 원을 그리는 렌더러다.
    private LineRenderer baseRingRenderer;
    // 현재 시야 방향의 호를 그리는 렌더러다.
    private LineRenderer directionArcRenderer;
    // 두 LineRenderer가 공유하는 런타임 전용 머티리얼이다.
    private Material runtimeMaterial;
    // 마지막으로 표시 색상에 적용한 플레이어 시야 알파다.
    private float appliedVisionAlpha = -1f;
    // 논리 처리와 분리해 화면에 현재 표시 중인 시야 각도다.
    private float presentedDirectionAngle;
    // 연출 시간축에서 Unaware 또는 Suspicious 상태로 표시할 수 있는지 나타낸다.
    private bool awarenessPresentationVisible;
    // 현재 방향 회전 연출을 실행 중인 코루틴이다.
    private Coroutine turnCoroutine;
    // 현재 직접 처리 중인 방향 회전 이벤트의 완료 핸들이다.
    private PresentationEventHandle activeHandle;
    // ActorPresentationRegistry에 방향 표시 연결이 등록됐는지 나타낸다.
    private bool registeredToRegistry;

    public bool IsTurning => turnCoroutine != null;

    /// <summary>
    /// 필수 참조와 표시 데이터를 검사하고 방향 표시를 생성한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData() || !CreateIndicator())
        {
            enabled = false;
            return;
        }

        presentedDirectionAngle = GetDirectionAngle(targetEnemy.GridSight.FacingDirection);
        awarenessPresentationVisible = !targetEnemy.AlertState.IsAlerted;
        RefreshDirection(presentedDirectionAngle);
        RefreshVisibility(true);
        FollowVisualRoot();
        TryRegisterPresentationSystems(false);
    }

    /// <summary>
    /// 초기화 순서로 OnEnable에서 놓친 연출 큐와 등록소 연결을 다시 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterPresentationSystems(true);
    }

    /// <summary>
    /// 연출 시스템 등록과 진행 중인 회전을 정리하고 런타임 표시를 숨긴다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (registeredToRegistry && ActorPresentationRegistry.Instance != null && targetEnemy != null)
        {
            ActorPresentationRegistry.Instance.UnregisterFacingIndicator(targetEnemy.GridActor, this);
        }

        registeredToRegistry = false;
        if (turnCoroutine != null)
        {
            StopCoroutine(turnCoroutine);
            turnCoroutine = null;
        }

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
        }

        activeHandle = null;
        SetRendererEnabled(false);
    }

    /// <summary>
    /// 런타임에 생성한 표시 오브젝트와 머티리얼을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (runtimeIndicatorRoot != null)
        {
            Destroy(runtimeIndicatorRoot);
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    /// <summary>
    /// 이동 연출 중에도 표시가 적 Visual을 따라가고 Fog 알파와 일치하도록 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        FollowVisualRoot();
        RefreshVisibility(false);
    }

    /// <summary>
    /// 현재 씬의 연출 큐와 Actor 연출 등록소에 이 방향 표시를 등록한다.
    /// </summary>
    private void TryRegisterPresentationSystems(bool logMissingSystems)
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            if (logMissingSystems)
            {
                Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);

        if (registeredToRegistry)
        {
            return;
        }

        ActorPresentationRegistry registry = ActorPresentationRegistry.Instance;
        if (registry == null)
        {
            if (logMissingSystems)
            {
                Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}에는 씬의 {nameof(ActorPresentationRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        registeredToRegistry = registry.RegisterFacingIndicator(targetEnemy.GridActor, this);
        if (!registeredToRegistry)
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 이 Presenter가 담당 적 하나의 방향 회전 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.EnemyFacingTurn &&
            presentationEvent.Enemy == targetEnemy;
    }

    /// <summary>
    /// 담당 적의 방향 표시를 목표 방향까지 회전하고 완료 신호를 보낸다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (turnCoroutine != null)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}은 이미 방향 회전 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다.", this);
            handle.Complete();
            return;
        }

        activeHandle = handle;
        if (!TryStartPresentationTurn(presentationEvent.ToFacingDirection))
        {
            activeHandle = null;
            handle.Complete();
        }
    }

    /// <summary>
    /// 편대 이동 Presenter가 호출할 제자리 방향 회전 연출을 시작한다.
    /// 이미 회전 중이면 false를 반환한다.
    /// </summary>
    public bool TryStartPresentationTurn(GridDirection targetDirection)
    {
        if (turnCoroutine != null)
        {
            return false;
        }

        float targetAngle = GetDirectionAngle(targetDirection);
        if (Mathf.Abs(Mathf.DeltaAngle(presentedDirectionAngle, targetAngle)) <= 0.01f)
        {
            presentedDirectionAngle = targetAngle;
            ApplyHorizontalVisualFacing(targetDirection);
            RefreshDirection(presentedDirectionAngle);
            CompleteActiveTurn();
            return true;
        }

        turnCoroutine = StartCoroutine(PlayDirectionTurn(targetDirection, targetAngle));
        return true;
    }

    /// <summary>
    /// 현재 표시 각도에서 목표 각도까지 최단 방향으로 회전하고 중간 지점에서 좌우 비주얼을 맞춘다.
    /// </summary>
    private IEnumerator PlayDirectionTurn(GridDirection targetDirection, float targetAngle)
    {
        float startAngle = presentedDirectionAngle;
        float angleDelta = Mathf.DeltaAngle(startAngle, targetAngle);
        float elapsed = 0f;
        bool visualFacingApplied = targetDirection is GridDirection.Up or GridDirection.Down;
        while (elapsed < turnDuration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / turnDuration);
            float curveTime = turnCurve.Evaluate(normalizedTime);
            presentedDirectionAngle = startAngle + angleDelta * curveTime;
            RefreshDirection(presentedDirectionAngle);

            if (!visualFacingApplied && curveTime >= 0.5f)
            {
                ApplyHorizontalVisualFacing(targetDirection);
                visualFacingApplied = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        presentedDirectionAngle = targetAngle;
        if (!visualFacingApplied)
        {
            ApplyHorizontalVisualFacing(targetDirection);
        }

        RefreshDirection(presentedDirectionAngle);
        CompleteActiveTurn();
    }

    /// <summary>
    /// 목표 시야가 좌우 방향일 때만 캐릭터 비주얼의 화면 방향을 함께 변경한다.
    /// 상하 방향은 마지막 좌우 방향을 유지한다.
    /// </summary>
    private void ApplyHorizontalVisualFacing(GridDirection targetDirection)
    {
        if (targetDirection == GridDirection.Left)
        {
            visualController.FaceLeft();
        }
        else if (targetDirection == GridDirection.Right)
        {
            visualController.FaceRight();
        }
    }

    /// <summary>
    /// 현재 방향 회전 상태를 정리하고 직접 처리한 큐 이벤트가 있으면 완료한다.
    /// </summary>
    private void CompleteActiveTurn()
    {
        turnCoroutine = null;
        PresentationEventHandle completedHandle = activeHandle;
        activeHandle = null;
        completedHandle?.Complete();
    }

    /// <summary>
    /// 연출 시간축의 인식 상태에 따라 방향 표시 허용 여부를 변경한다.
    /// </summary>
    public void SetAwarenessPresentationVisible(bool visible)
    {
        awarenessPresentationVisible = visible;
        RefreshVisibility(true);
    }

    /// <summary>
    /// 지정한 화면 각도를 중심으로 주황색 호의 점을 배치한다.
    /// </summary>
    private void RefreshDirection(float centerAngle)
    {
        if (directionArcRenderer == null)
        {
            return;
        }

        int arcSegmentCount = Mathf.Max(2, Mathf.CeilToInt(segmentCount * directionArcDegrees / 360f));
        directionArcRenderer.positionCount = arcSegmentCount + 1;

        float startAngle = centerAngle - directionArcDegrees * 0.5f;
        for (int i = 0; i <= arcSegmentCount; i++)
        {
            float progress = (float)i / arcSegmentCount;
            float angle = (startAngle + directionArcDegrees * progress) * Mathf.Deg2Rad;
            directionArcRenderer.SetPosition(
                i,
                new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    /// <summary>
    /// 런타임 표시 루트를 현재 적 Visual의 화면 위치에 맞춘다.
    /// </summary>
    private void FollowVisualRoot()
    {
        if (runtimeIndicatorRoot != null && visualController != null)
        {
            runtimeIndicatorRoot.transform.position = visualController.transform.position + worldPositionOffset;
        }
    }

    /// <summary>
    /// 적 생존 여부와 플레이어 시야 알파를 두 LineRenderer에 적용한다.
    /// </summary>
    private void RefreshVisibility(bool forceColorRefresh)
    {
        if (baseRingRenderer == null || directionArcRenderer == null || visualController == null || targetEnemy == null)
        {
            return;
        }

        bool visible = awarenessPresentationVisible && targetEnemy.IsAlive && visualController.VisionAlpha > 0.001f;
        SetRendererEnabled(visible);
        if (!visible)
        {
            return;
        }

        float visionAlpha = visualController.VisionAlpha;
        if (!forceColorRefresh && Mathf.Approximately(appliedVisionAlpha, visionAlpha))
        {
            return;
        }

        appliedVisionAlpha = visionAlpha;
        ApplyRendererColor(baseRingRenderer, baseColor, visionAlpha);
        ApplyRendererColor(directionArcRenderer, directionColor, visionAlpha);
    }

    /// <summary>
    /// 전체 원과 방향 호의 활성 상태를 함께 변경한다.
    /// </summary>
    private void SetRendererEnabled(bool value)
    {
        if (baseRingRenderer != null)
        {
            baseRingRenderer.enabled = value;
        }

        if (directionArcRenderer != null)
        {
            directionArcRenderer.enabled = value;
        }
    }

    /// <summary>
    /// 지정한 원본 색에 플레이어 시야 알파를 곱해 LineRenderer에 적용한다.
    /// </summary>
    private static void ApplyRendererColor(LineRenderer renderer, Color sourceColor, float visionAlpha)
    {
        Color color = sourceColor;
        color.a *= visionAlpha;
        renderer.startColor = color;
        renderer.endColor = color;
    }

    /// <summary>
    /// 회색 전체 원과 주황색 방향 호를 런타임에 생성한다.
    /// </summary>
    private bool CreateIndicator()
    {
        if (runtimeIndicatorRoot != null)
        {
            return true;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}은 방향 표시에 사용할 Sprites/Default 셰이더를 찾지 못했습니다.", this);
            return false;
        }

        runtimeIndicatorRoot = new GameObject($"{nameof(EnemyFacingIndicatorPresenter)}_RuntimeIndicator");
        runtimeIndicatorRoot.transform.SetParent(transform, true);
        runtimeIndicatorRoot.transform.localScale = Vector3.one;

        runtimeMaterial = new Material(shader)
        {
            name = $"{nameof(EnemyFacingIndicatorPresenter)}_RuntimeMaterial",
        };

        baseRingRenderer = CreateLineRenderer("BaseRing", baseLineWidth, true);
        directionArcRenderer = CreateLineRenderer("DirectionArc", directionLineWidth, false);
        BuildBaseRing();
        return true;
    }

    /// <summary>
    /// 지정한 이름과 선 굵기로 방향 표시용 LineRenderer를 생성한다.
    /// </summary>
    private LineRenderer CreateLineRenderer(string objectName, float lineWidth, bool loop)
    {
        GameObject lineObject = new(objectName);
        lineObject.transform.SetParent(runtimeIndicatorRoot.transform, false);

        LineRenderer renderer = lineObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = false;
        renderer.loop = loop;
        renderer.alignment = LineAlignment.TransformZ;
        renderer.startWidth = lineWidth;
        renderer.endWidth = lineWidth;
        renderer.numCornerVertices = 2;
        renderer.numCapVertices = loop ? 0 : 3;
        renderer.sharedMaterial = runtimeMaterial;
        renderer.sortingLayerID = visualController.TargetRenderer.sortingLayerID;
        renderer.sortingOrder = visualController.TargetRenderer.sortingOrder + sortingOrderOffset;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>
    /// 지정한 반지름과 선분 수로 회색 전체 원의 점을 배치한다.
    /// </summary>
    private void BuildBaseRing()
    {
        baseRingRenderer.positionCount = segmentCount;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / segmentCount;
            baseRingRenderer.SetPosition(
                i,
                new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    /// <summary>
    /// 그리드 방향을 오른쪽 0도 기준의 원형 각도로 변환한다.
    /// </summary>
    private static float GetDirectionAngle(GridDirection direction)
    {
        return direction switch
        {
            GridDirection.Up => 90f,
            GridDirection.Down => 270f,
            GridDirection.Left => 180f,
            GridDirection.Right => 0f,
            _ => 90f,
        };
    }

    /// <summary>
    /// 방향 표시 대상과 Visual 참조가 올바르게 연결돼 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetEnemy == null || targetEnemy.GridActor == null || targetEnemy.GridSight == null || targetEnemy.AlertState == null)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}에는 유효한 적 Context, GridActor, EnemyGridSight와 EnemyAlertState가 필요합니다.", this);
            return false;
        }

        if (visualController == null || !visualController.HasValidReference())
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}에는 유효한 {nameof(ActorVisualController)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 원 크기, 선 굵기, 방향 호와 선분 수가 표시 가능한 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (radius <= 0f || baseLineWidth <= 0f || directionLineWidth <= 0f || turnDuration <= 0f)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}의 반지름, 선 굵기와 방향 회전 시간은 0보다 커야 합니다.", this);
            return false;
        }

        if (turnCurve == null || turnCurve.length == 0)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}의 방향 회전 곡선이 비어 있습니다.", this);
            return false;
        }

        if (directionArcDegrees <= 0f || directionArcDegrees > 360f)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}의 방향 호 각도는 0보다 크고 360 이하여야 합니다.", this);
            return false;
        }

        if (segmentCount < 8)
        {
            Debug.LogError($"{nameof(EnemyFacingIndicatorPresenter)} on {name}의 원 선분 수는 8 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
