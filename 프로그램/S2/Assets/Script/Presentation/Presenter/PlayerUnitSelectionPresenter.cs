using UnityEngine;

/// <summary>
/// 담당 플레이어 전술 유닛이 현재 조작 유닛일 때 발밑에 임시 선택 링을 표시한다.
/// 선택 판정에는 관여하지 않고 PlayerUnitControlManager의 선택 변경 결과만 시각화한다.
/// </summary>
public class PlayerUnitSelectionPresenter : MonoBehaviour
{
    [Header("Target")]
    // 이 Presenter가 선택 상태를 표시할 플레이어 전술 유닛이다.
    [SerializeField] private TacticalUnitContext targetUnit;

    [Header("Temporary Ring")]
    // 유닛 루트 기준 임시 선택 링의 위치다.
    [SerializeField] private Vector3 localOffset = new(0f, -0.4f, -0.05f);
    // 임시 선택 링의 반지름이다.
    [SerializeField] private float radius = 0.35f;
    // 임시 선택 링 선의 굵기다.
    [SerializeField] private float lineWidth = 0.04f;
    // 임시 선택 링의 색상이다.
    [SerializeField] private Color ringColor = new(1f, 0.85f, 0.15f, 0.9f);
    // 임시 선택 링의 원형을 구성할 선분 수다.
    [SerializeField] private int segmentCount = 48;
    // 다른 스프라이트와 겹칠 때 사용할 임시 선택 링의 정렬 순서다.
    [SerializeField] private int sortingOrder = 5;

    // 런타임에 생성한 임시 선택 링 오브젝트다.
    private GameObject runtimeRingObject;
    // 임시 선택 링을 그리는 LineRenderer다.
    private LineRenderer ringRenderer;
    // 임시 선택 링에 사용하는 런타임 전용 머티리얼이다.
    private Material runtimeRingMaterial;
    // 현재 플레이어 유닛 제어 매니저의 선택 변경 이벤트를 구독 중인지 나타낸다.
    private bool subscribedControlManager;

    /// <summary>
    /// 필수 참조와 표시 데이터를 검사하고 선택 변경 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData() || !CreateTemporaryRing())
        {
            enabled = false;
            return;
        }

        SetSelected(false);
        TrySubscribeControlManager(false);
        RefreshSelectionState();
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 제어 매니저 구독을 시작 시점에 다시 시도한다.
    /// </summary>
    private void Start()
    {
        if (!TrySubscribeControlManager(true))
        {
            enabled = false;
            return;
        }

        RefreshSelectionState();
    }

    /// <summary>
    /// 선택 변경 이벤트 구독을 해제하고 임시 선택 링을 숨긴다.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeControlManager();
        SetSelected(false);
    }

    /// <summary>
    /// 런타임에 만든 임시 선택 링 오브젝트와 머티리얼을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (runtimeRingObject != null)
        {
            Destroy(runtimeRingObject);
        }

        if (runtimeRingMaterial != null)
        {
            Destroy(runtimeRingMaterial);
        }
    }

    /// <summary>
    /// 현재 씬의 플레이어 유닛 제어 매니저 선택 변경 이벤트를 구독한다.
    /// </summary>
    private bool TrySubscribeControlManager(bool logMissingManager)
    {
        if (subscribedControlManager)
        {
            return true;
        }

        PlayerUnitControlManager controlManager = PlayerUnitControlManager.Instance;
        if (controlManager == null)
        {
            if (logMissingManager)
            {
                Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}에는 씬의 {nameof(PlayerUnitControlManager)}가 필요합니다.", this);
            }

            return false;
        }

        controlManager.ActiveUnitChanged -= HandleActiveUnitChanged;
        controlManager.ActiveUnitChanged += HandleActiveUnitChanged;
        subscribedControlManager = true;
        return true;
    }

    /// <summary>
    /// 현재 플레이어 유닛 제어 매니저의 선택 변경 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribeControlManager()
    {
        PlayerUnitControlManager controlManager = PlayerUnitControlManager.Instance;
        if (controlManager != null)
        {
            controlManager.ActiveUnitChanged -= HandleActiveUnitChanged;
        }

        subscribedControlManager = false;
    }

    /// <summary>
    /// 새 조작 유닛이 담당 유닛인지에 따라 임시 선택 링 표시 상태를 갱신한다.
    /// </summary>
    private void HandleActiveUnitChanged(TacticalUnitContext previousUnit, TacticalUnitContext activeUnit)
    {
        SetSelected(activeUnit == targetUnit);
    }

    /// <summary>
    /// 현재 조작 유닛을 조회해 임시 선택 링 표시 상태를 즉시 맞춘다.
    /// </summary>
    private void RefreshSelectionState()
    {
        PlayerUnitControlManager controlManager = PlayerUnitControlManager.Instance;
        SetSelected(controlManager != null && controlManager.ActiveUnit == targetUnit);
    }

    /// <summary>
    /// 담당 유닛의 선택 여부를 임시 선택 링 활성 상태에 적용한다.
    /// </summary>
    private void SetSelected(bool selected)
    {
        if (ringRenderer != null)
        {
            ringRenderer.enabled = selected;
        }
    }

    /// <summary>
    /// 현재 테스트에 사용할 LineRenderer 기반 임시 선택 링을 생성하고 모양을 설정한다.
    /// </summary>
    private bool CreateTemporaryRing()
    {
        if (ringRenderer != null)
        {
            return true;
        }

        Shader ringShader = Shader.Find("Sprites/Default");
        if (ringShader == null)
        {
            Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}은 임시 선택 링에 사용할 Sprites/Default 셰이더를 찾지 못했습니다.", this);
            return false;
        }

        runtimeRingObject = new GameObject($"{nameof(PlayerUnitSelectionPresenter)}_TemporaryRing");
        runtimeRingObject.transform.SetParent(transform, false);
        runtimeRingObject.transform.localPosition = localOffset;

        runtimeRingMaterial = new Material(ringShader)
        {
            name = $"{nameof(PlayerUnitSelectionPresenter)}_RuntimeMaterial",
        };

        ringRenderer = runtimeRingObject.AddComponent<LineRenderer>();
        ringRenderer.useWorldSpace = false;
        ringRenderer.loop = true;
        ringRenderer.alignment = LineAlignment.TransformZ;
        ringRenderer.positionCount = segmentCount;
        ringRenderer.startWidth = lineWidth;
        ringRenderer.endWidth = lineWidth;
        ringRenderer.startColor = ringColor;
        ringRenderer.endColor = ringColor;
        ringRenderer.numCornerVertices = 2;
        ringRenderer.sharedMaterial = runtimeRingMaterial;
        ringRenderer.sortingOrder = sortingOrder;
        ringRenderer.receiveShadows = false;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / segmentCount;
            ringRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        return true;
    }

    /// <summary>
    /// 선택 표시 대상 플레이어 전술 유닛 참조가 올바르게 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetUnit == null)
        {
            Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}에는 선택 상태를 표시할 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (targetUnit.Faction != UnitFaction.Player || targetUnit.ControlType != UnitControlType.Player)
        {
            Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}의 대상은 플레이어가 조작하는 전술 유닛이어야 합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 임시 선택 링을 그리는 데 필요한 표시 값이 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (radius <= 0f)
        {
            Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}의 선택 링 반지름은 0보다 커야 합니다.", this);
            return false;
        }

        if (lineWidth <= 0f)
        {
            Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}의 선택 링 굵기는 0보다 커야 합니다.", this);
            return false;
        }

        if (segmentCount < 8)
        {
            Debug.LogError($"{nameof(PlayerUnitSelectionPresenter)} on {name}의 선택 링 선분 수는 8 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
