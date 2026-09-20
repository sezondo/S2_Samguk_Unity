using UnityEngine;

/// <summary>인접 엄폐물 하나를 안정적으로 선택하고 자세·방향·표시 위치를 요청한다.</summary>
public class LowCoverIdlePresenter : MonoBehaviour
{
    [Header("Reference")]
    // 논리 위치와 생존 상태를 제공하는 명시적 참조다.
    [SerializeField] private TacticalUnitContext unitContext;
    // 실제 그림의 위치·방향·자세를 제어하는 명시적 참조다.
    [SerializeField] private ActorVisualController visualController;
    // 벽과 낮은 엄폐물의 논리 칸을 조회한다.
    [SerializeField] private GridManager gridManager;
    // 미래 논리 위치가 진행 중인 연출에 먼저 반영되지 않게 한다.
    [SerializeField] private ActionPresentationQueue presentationQueue;

    [Header("Cover Contact")]
    // 표시 루트 중심에서 접촉면까지의 로컬 거리다. X는 좌우, Y는 위아래에 사용한다.
    [Tooltip("낮은 엄폐의 접촉 기준 거리. X: 좌우, Y: 위아래. 캐릭터 배율이 적용됩니다.")]
    [SerializeField] private Vector2 lowCoverContactOffset = new Vector2(0.9f, 0.35f);
    // 높은 엄폐 자세에서 접촉면까지의 로컬 거리다.
    [Tooltip("높은 엄폐의 접촉 기준 거리. X: 좌우, Y: 위아래. 캐릭터 배율이 적용됩니다.")]
    [SerializeField] private Vector2 wallCoverContactOffset = new Vector2(0.55f, 0.6f);
    // 엄폐 칸의 경계와 접촉 기준점 사이에 남길 월드 거리다.
    [SerializeField, Min(0f)] private float surfaceGap = 0.02f;
    // 높은 엄폐와 일반 복귀에 사용하는 표시 위치 보간 시간이다.
    [SerializeField, Min(0f)] private float transitionDuration = 0.15f;
    // 낮은 엄폐의 자세가 즉시 바뀌어도 밀착 이동을 읽을 수 있도록 별도로 조절한다.
    [SerializeField, Min(0f)] private float lowCoverTransitionDuration = 0.24f;

    // 같은 거리일 때 방향이 흔들리지 않도록 적용하는 고정 탐색 순서다.
    private static readonly GridPosition[] SearchDirections =
        { GridPosition.Left, GridPosition.Right, GridPosition.Up, GridPosition.Down };
    // 현재 선택한 엄폐물의 논리 칸이다.
    private GridPosition selectedCoverPosition;
    // 유효한 엄폐물 선택을 보유하는지 나타낸다.
    private bool hasSelectedCover;
    // 현재 선택한 대상의 종류이며, 동시에 인접한 다른 엄폐물의 종류는 포함하지 않는다.
    private bool isNearLowCover;
    private bool isNearWallCover;
    // 필수 참조와 설정 검증이 완료됐는지 나타낸다.
    private bool initialized;

    public bool IsNearLowCover => isNearLowCover;
    public bool IsNearWallCover => isNearWallCover;
    public bool HasSelectedCover => hasSelectedCover;
    public GridPosition SelectedCoverPosition => selectedCoverPosition;

    /// <summary>엄폐 애니메이션·참조·접촉 설정을 검증한다.</summary>
    private void OnEnable()
    {
        hasSelectedCover = false;
        initialized = HasValidReference() && HasValidData() && visualController.HasValidCoverIdleAnimation();
        if (!initialized) enabled = false;
        else visualController.IdleRequested += PrepareIdleCover;
    }

    /// <summary>연출 큐가 비었을 때만 현재 논리 칸의 엄폐 대상을 갱신한다.</summary>
    private void LateUpdate()
    {
        if (!initialized || presentationQueue.IsPlaying || visualController.IsMovingPresentation) return;
        RefreshCover();
    }

    /// <summary>현재 엄폐물을 유지하거나 가장 적은 표시 이동으로 붙는 대상을 선택한다.</summary>
    public void RefreshCover()
    {
        if (!initialized || presentationQueue.IsPlaying || visualController.IsMovingPresentation) return;
        bool canUseCover = unitContext.isActiveAndEnabled && unitContext.IsAlive &&
                           unitContext.GridActor.IsRegisteredOnGrid && !visualController.IsDeathPresentation;
        if (!canUseCover)
        {
            ClearSelection();
            return;
        }
        SelectCover(unitContext.GridActor.GridPosition);
    }

    /// <summary>대기 요청 전에 실제 표시 도착 칸의 엄폐를 확정하여 일반 Idle이 한 프레임 끼지 않게 한다.</summary>
    private void PrepareIdleCover()
    {
        if (!initialized || visualController.IsMovingPresentation || visualController.IsDeathPresentation) return;
        // 논리 위치는 뒤에 대기 중인 이동까지 앞서갈 수 있으므로 현재 연출 위치를 사용한다.
        GridPosition position = gridManager.WorldToGrid(visualController.transform.position - visualController.CoverWorldOffset);
        SelectCover(position);
    }

    /// <summary>표시할 칸에서 기존 엄폐를 유지하거나 가장 가까운 새 대상을 고른다.</summary>
    private void SelectCover(GridPosition position)
    {
        // 같은 대상이 여전히 인접한 실제 엄폐물이면 새 후보보다 먼저 유지한다.
        if (hasSelectedCover && position.ManhattanDistanceTo(selectedCoverPosition) == 1 &&
            TryGetCoverKind(selectedCoverPosition, out bool retainedLow))
        {
            ApplySelection(position, selectedCoverPosition, retainedLow);
            return;
        }
        bool found = false;
        bool bestLow = false;
        GridPosition bestPosition = default;
        float bestDistance = float.PositiveInfinity;
        foreach (GridPosition direction in SearchDirections)
        {
            GridPosition candidate = position + direction;
            if (!TryGetCoverKind(candidate, out bool low)) continue;
            Vector3 offset = CalculateContactOffset(direction, low);
            float distance = (offset - visualController.CoverWorldOffset).sqrMagnitude;
            // 거의 같은 거리에서는 앞서 검사한 방향을 유지한다.
            if (found && distance >= bestDistance - 0.000001f) continue;
            found = true;
            bestLow = low;
            bestPosition = candidate;
            bestDistance = distance;
        }
        if (found) ApplySelection(position, bestPosition, bestLow);
        else ClearSelection();
    }

    /// <summary>경계·동적 차단물을 제외하고 실제 벽/낮은 타일만 분류한다.</summary>
    private bool TryGetCoverKind(GridPosition position, out bool low)
    {
        low = gridManager.IsLowObstacle(position);
        return low || gridManager.IsWall(position);
    }

    /// <summary>접촉 기준점을 인접 엄폐 칸의 가까운 면에 맞출 표시 오프셋을 계산한다.</summary>
    private Vector3 CalculateContactOffset(GridPosition direction, bool low)
    {
        Vector2 contact = low ? lowCoverContactOffset : wallCoverContactOffset;
        Vector3 scale = visualController.transform.lossyScale;
        float distance = direction.x != 0 ? contact.x * Mathf.Abs(scale.x) : contact.y * Mathf.Abs(scale.y);
        // 기준점이 이미 경계에 닿으면 엄폐 반대쪽으로 밀어내지 않는다.
        float amount = Mathf.Max(0f, gridManager.CellSize * 0.5f - distance - surfaceGap);
        return new Vector3(direction.x * amount, direction.y * amount, 0f);
    }

    /// <summary>선택한 엄폐물의 종류·좌우 방향·목표 위치를 함께 전달한다.</summary>
    private void ApplySelection(GridPosition position, GridPosition coverPosition, bool low)
    {
        selectedCoverPosition = coverPosition;
        hasSelectedCover = true;
        isNearLowCover = low;
        isNearWallCover = !low;
        GridPosition direction = coverPosition - position;
        visualController.SetCoverPresentation(low, !low, CalculateContactOffset(direction, low), direction.x, low ? lowCoverTransitionDuration : transitionDuration);
    }

    /// <summary>엄폐 선택을 해제하고 생존한 그림만 부드럽게 원위치로 돌린다.</summary>
    private void ClearSelection()
    {
        float releaseDuration = isNearLowCover ? lowCoverTransitionDuration : transitionDuration;
        hasSelectedCover = false;
        isNearLowCover = false;
        isNearWallCover = false;
        visualController.SetCoverPresentation(false, false, Vector3.zero, 0, releaseDuration);
    }

    /// <summary>기능이 꺼지면 이전 엄폐 요청을 해제한다.</summary>
    private void OnDisable()
    {
        if (initialized && visualController != null)
        {
            visualController.IdleRequested -= PrepareIdleCover;
            ClearSelection();
        }
        initialized = false;
        hasSelectedCover = false;
    }

    /// <summary>필수 Context 및 연출 참조가 명시적으로 연결됐는지 검사한다.</summary>
    public bool HasValidReference()
    {
        if (unitContext == null || unitContext.GridActor == null || unitContext.Health == null ||
            visualController == null || gridManager == null || presentationQueue == null)
        {
            Debug.LogError($"{nameof(LowCoverIdlePresenter)} on {name}에는 GridActor·Health가 연결된 TacticalUnitContext, ActorVisualController, GridManager, ActionPresentationQueue가 필요합니다.", this);
            return false;
        }
        return true;
    }

    /// <summary>접촉 거리와 전환 시간이 유한한 음수 아닌 값인지 검사한다.</summary>
    public bool HasValidData()
    {
        if (!IsValidDistance(lowCoverContactOffset.x) || !IsValidDistance(lowCoverContactOffset.y) ||
            !IsValidDistance(wallCoverContactOffset.x) || !IsValidDistance(wallCoverContactOffset.y) ||
            !IsValidDistance(surfaceGap) || !IsValidDistance(transitionDuration) ||
            !IsValidDistance(lowCoverTransitionDuration))
        {
            Debug.LogError($"{nameof(LowCoverIdlePresenter)} on {name}의 접촉 거리·여백·전환 시간은 유한한 0 이상의 값이어야 합니다.", this);
            return false;
        }
        return true;
    }

    /// <summary>거리 또는 시간 값이 사용할 수 있는 범위인지 확인한다.</summary>
    private static bool IsValidDistance(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
