using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 정식 그리드 이동 행동을 담당한다.
/// UI가 붙기 전까지는 디버그 키로 이동 행동 선택과 클릭 실행만 검증한다.
/// </summary>
[RequireComponent(typeof(GridActor))]
public class PlayerGridMoveAction : MonoBehaviour
{
    [Header("Move Action")]
    // 플레이어 공통 참조와 턴 데이터를 제공하는 필수 Context다.
    [SerializeField] private TacticalUnitContext playerContext;
    // true면 플레이어 턴일 때만 이동 행동을 선택하고 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Log")]
    // 이동 행동 선택, 취소, 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 이동 불가 목표를 클릭했을 때 차단 사유 로그를 출력할지 정한다.
    [SerializeField] private bool logBlockedTarget = true;

    // 현재 이동 행동 선택 상태에서 실제로 이동 가능한 칸 목록이다.
    private readonly List<GridPosition> movablePositions = new();
    // AP 1개 구간으로 이동 가능한 칸 목록이다.
    private readonly List<GridPosition> blueMovePositions = new();
    // AP 2개 구간으로 이동 가능한 칸 목록이다.
    private readonly List<GridPosition> yellowMovePositions = new();
    // AP 3개 이상 구간으로 이동 가능한 칸 목록이다.
    private readonly List<GridPosition> redMovePositions = new();
    // 이동 가능 칸까지의 실제 최단 거리 정보를 임시로 보관한다.
    private readonly Dictionary<GridPosition, int> distanceByMovablePosition = new();
    // 목표 칸까지 한 칸씩 이동할 경로를 임시로 담는 재사용 버퍼다.
    private readonly List<GridPosition> movePathBuffer = new();
    // 이동 행동 선택 중 마우스가 가리키는 칸까지의 경로 미리보기다.
    private readonly List<GridPosition> pathPreviewPositions = new();

    // 같은 오브젝트의 그리드 말 컴포넌트다.
    private GridActor actor;
    // 플레이어가 현재 이동 행동을 선택한 상태인지 나타낸다.
    private bool isMoveSelected;
    // 마지막으로 경로 미리보기를 계산한 마우스 칸이다.
    private GridPosition lastPreviewTargetPosition;
    // 현재 경로 미리보기 대상 칸이 유효한지 나타낸다.
    private bool hasPreviewTargetPosition;

    // 외부 UI나 표시 컴포넌트가 이동 선택 상태를 읽을 때 사용한다.
    public bool IsMoveSelected => isMoveSelected;
    // 표시 계층이 핵심 참조와 현재 경로를 읽는 전용 접근점이다.
    public TacticalUnitContext UnitContext => playerContext;
    public IReadOnlyList<GridPosition> PreviewPath => pathPreviewPositions;
    // 표시 중인 AP 한도 변경을 감지해 오래된 범위를 지운다.
    private int displayedMoveRange;
    // 현재 AP로 한 번에 이동할 수 있는 최대 칸 수다.
    public int MoveRange => CalculateAffordableMoveSegmentCount() * MoveDistancePerActionPoint;
    // AP 1개 구간당 이동 가능한 칸 수다.
    public int MoveDistancePerActionPoint => playerContext.UnitData.MoveDistancePerActionPoint;

    // 이동 가능 칸 목록을 화면에 표시해야 할 때 발생한다.
    public event Action<IReadOnlyList<GridPosition>> MoveRangeShown;
    // AP 소비 구간별 이동 가능 칸 목록을 화면에 표시해야 할 때 발생한다.
    public event Action<IReadOnlyList<GridPosition>, IReadOnlyList<GridPosition>, IReadOnlyList<GridPosition>> MoveRangeSegmentsShown;
    // 이동 가능 칸 표시를 지워야 할 때 발생한다.
    public event Action MoveRangeHidden;
    // 이동 경로 미리보기가 갱신될 때 현재 경로 칸 목록을 전달한다.
    public event Action<IReadOnlyList<GridPosition>> MovePathPreviewShown;
    // 이동 경로 미리보기를 지워야 할 때 발생한다.
    public event Action MovePathPreviewHidden;
    /// <summary>
    /// 이동 행동에 필요한 같은 오브젝트의 컴포넌트 참조를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        actor = playerContext.GridActor;
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 이동 경로 미리보기를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        isMoveSelected = false;
        ClearMoveSelectionPresentation();
    }

    /// <summary>외부 AP 변화가 발생하면 선택 중인 범위와 경로를 다시 검증한다.</summary>
    private void Update()
    {
        if (!isMoveSelected || displayedMoveRange == MoveRange) return;
        if (MoveRange <= 0) { CancelMoveAction(); return; }
        bool hadTarget = hasPreviewTargetPosition;
        GridPosition target = lastPreviewTargetPosition;
        ClearPathPreview();
        RefreshMovablePositions();
        if (hadTarget) RefreshPathPreview(target);
    }

    /// <summary>
    /// 이동 행동을 선택하고 현재 위치 기준 이동 가능 칸 목록을 갱신한다.
    /// </summary>
    public void SelectMoveAction()
    {
        if (!CanSelectMoveAction())
        {
            return;
        }

        isMoveSelected = true;
        RefreshMovablePositions();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGridMoveAction)}: 이동 행동을 선택했습니다. 이동 가능 칸 수: {movablePositions.Count}", this);
        }

        MoveRangeShown?.Invoke(movablePositions);
    }

    /// <summary>
    /// 현재 이동 행동 선택을 취소하고 이동 가능 칸 표시를 지운다.
    /// </summary>
    public void CancelMoveAction()
    {
        bool wasSelected = isMoveSelected;
        isMoveSelected = false;
        ClearMoveSelectionPresentation();

        if (wasSelected && logActionState)
        {
            Debug.Log($"{nameof(PlayerGridMoveAction)}: 이동 행동 선택을 취소했습니다.", this);
        }
    }

    /// <summary>
    /// 선택된 목표 칸으로 이동 행동을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteMoveTo(GridPosition targetPosition, ActionResolutionContext resolutionContext)
    {
        if (!isMoveSelected)
        {
            return false;
        }

        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}에는 이동 행동을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        // 턴, AP 같은 행동 가능 상태와 실제 경로 유효성은 서로 다른 책임으로 분리한다.
        if (!CanSelectMoveAction())
        {
            return false;
        }

        if (!GridPathfinder.TryFindPath(GridManager.Instance, actor.GridPosition, targetPosition, MoveRange, movePathBuffer))
        {
            LogBlockedTarget(targetPosition, "이동 가능한 경로를 찾지 못했습니다");
            return false;
        }

        // 이동 행동은 시작 시점에 AP를 소비한다.
        int cost = CalculateMoveActionPointCost(movePathBuffer.Count);
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.TrySpend(cost))
        {
            LogBlockedTarget(targetPosition, "AP가 부족합니다");
            return false;
        }

        GridPosition previousPosition = actor.GridPosition;
        for (int i = 0; i < movePathBuffer.Count; i++)
        {
            GridPosition step = movePathBuffer[i];
            if (!actor.TryMoveTo(step))
            {
                LogBlockedTarget(step, "GridActor 이동 요청이 실패했습니다");
                CancelMoveAction();
                return false;
            }

            MovePresentationPhase movePhase = MovePresentationPhaseUtility.GetPhase(i, movePathBuffer.Count);
            resolutionContext.EnqueuePresentation(PresentationEvent.MoveActor(actor, previousPosition, step, movePhase, "플레이어 이동 연출"));
            resolutionContext.Publish(new MoveStepEnteredLogicEvent(actor, step));
            // 이동 연출 사이에 발각 같은 후속 연출 이벤트가 끼어들 수 있도록 칸 단위로 논리 이벤트를 즉시 처리한다.
            resolutionContext.Resolve();
            previousPosition = step;
        }

        isMoveSelected = false;
        ClearMoveSelectionPresentation();
        resolutionContext.Publish(new MoveCompletedLogicEvent(actor, actor.GridPosition));
        // 이동 완료 후 목표 달성 같은 후속 논리 이벤트를 연출 큐 재생 전에 확정한다.
        resolutionContext.Resolve();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGridMoveAction)}: {actor.GridPosition} 칸으로 이동했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 턴과 AP 상태 기준으로 이동 행동을 선택할 수 있는지 확인한다.
    /// </summary>
    private bool CanSelectMoveAction()
    {
        if (actor == null || GridManager.Instance == null)
        {
            return false;
        }

        ActionPresentationQueue presentationQueue = ActionPresentationQueue.Instance;
        if (presentationQueue == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}에는 이동 연출을 실행할 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            }

            return false;
        }

        if (presentationQueue.IsBusy)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGridMoveAction)}: 연출 큐가 실행 중이라 이동 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGridMoveAction)}: 현재 턴이 {turnManager.CurrentSide}라서 이동 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        int cost = playerContext.UnitData.MoveActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.CanSpend(cost))
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGridMoveAction)}: AP가 부족해서 이동 행동을 선택할 수 없습니다. 필요 AP: {cost}, 현재 AP: {actionPoint.Current}", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 플레이어 위치 기준으로 이동 가능한 칸 목록을 다시 계산한다.
    /// </summary>
    private void RefreshMovablePositions()
    {
        displayedMoveRange = MoveRange;
        movablePositions.Clear();
        ClearMoveRangeSegments();
        distanceByMovablePosition.Clear();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || actor == null)
        {
            return;
        }

        GridPathfinder.FindReachablePositionDistances(gridManager, actor.GridPosition, MoveRange, distanceByMovablePosition);
        foreach (KeyValuePair<GridPosition, int> pair in distanceByMovablePosition)
        {
            GridPosition position = pair.Key;
            int distance = pair.Value;
            movablePositions.Add(position);
            AddMovePositionToSegment(position, distance);
        }

        MoveRangeSegmentsShown?.Invoke(blueMovePositions, yellowMovePositions, redMovePositions);
    }

    /// <summary>
    /// 이동 선택 상태에서 현재 마우스 칸까지의 경로 미리보기를 갱신한다.
    /// </summary>
    public void RefreshPathPreview(GridPosition targetPosition)
    {
        if (!isMoveSelected) { ClearPathPreview(); return; }
        if (hasPreviewTargetPosition && lastPreviewTargetPosition == targetPosition)
        {
            return;
        }

        hasPreviewTargetPosition = true;
        lastPreviewTargetPosition = targetPosition;
        pathPreviewPositions.Clear();

        if (GridPathfinder.TryFindPath(GridManager.Instance, actor.GridPosition, targetPosition, MoveRange, pathPreviewPositions))
        {
            MovePathPreviewShown?.Invoke(pathPreviewPositions);
            return;
        }

        ClearPathPreview();
    }

    /// <summary>
    /// 현재 이동 경로 미리보기 정보를 비운다.
    /// </summary>
    private void ClearPathPreview()
    {
        hasPreviewTargetPosition = false;
        pathPreviewPositions.Clear();

        MovePathPreviewHidden?.Invoke();
    }

    /// <summary>
    /// 외부 입력 컨트롤러가 현재 이동 경로 미리보기를 지우도록 요청한다.
    /// </summary>
    public void ClearMovePathPreview()
    {
        ClearPathPreview();
    }

    /// <summary>
    /// 이동 선택 여부와 관계없이 이동 범위와 경로 미리보기 표시를 모두 정리한다.
    /// </summary>
    private void ClearMoveSelectionPresentation()
    {
        movablePositions.Clear();
        distanceByMovablePosition.Clear();
        ClearMoveRangeSegments();
        ClearPathPreview();
        MoveRangeHidden?.Invoke();
    }

    /// <summary>
    /// 이동이 막힌 목표 칸과 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedTarget(GridPosition targetPosition, string reason)
    {
        if (logBlockedTarget)
        {
            Debug.Log($"{nameof(PlayerGridMoveAction)}: {targetPosition} 칸으로 이동할 수 없습니다. 사유: {reason}", this);
        }
    }

    /// <summary>
    /// 이동 경로 길이 기준으로 실제 소비할 AP를 계산한다.
    /// </summary>
    public int CalculateMoveActionPointCost(int pathLength)
    {
        if (pathLength <= 0)
        {
            return 0;
        }

        int segmentCount = Mathf.CeilToInt(pathLength / (float)MoveDistancePerActionPoint);
        return segmentCount * playerContext.UnitData.MoveActionPointCost;
    }

    /// <summary>
    /// 현재 AP로 감당할 수 있는 이동 거리 구간 수를 계산한다.
    /// </summary>
    private int CalculateAffordableMoveSegmentCount()
    {
        int segmentCost = playerContext.UnitData.MoveActionPointCost;
        if (segmentCost <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, playerContext.ActionPoint.Current / segmentCost);
    }

    /// <summary>
    /// 이동 가능 칸을 실제 거리 기준 AP 소비 구간 목록에 추가한다.
    /// </summary>
    private void AddMovePositionToSegment(GridPosition position, int distance)
    {
        int segmentIndex = Mathf.CeilToInt(distance / (float)MoveDistancePerActionPoint);
        if (segmentIndex <= 1)
        {
            blueMovePositions.Add(position);
            return;
        }

        if (segmentIndex == 2)
        {
            yellowMovePositions.Add(position);
            return;
        }

        redMovePositions.Add(position);
    }

    /// <summary>
    /// 이동 가능 칸의 AP 소비 구간 캐시를 비운다.
    /// </summary>
    private void ClearMoveRangeSegments()
    {
        blueMovePositions.Clear();
        yellowMovePositions.Clear();
        redMovePositions.Clear();
    }

    /// <summary>
    /// 이동 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 이동 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        ControllableUnitData unitData = playerContext.UnitData;
        if (unitData == null)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}에는 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        if (unitData.MoveDistancePerActionPoint <= 0)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}의 {nameof(ControllableUnitData)} AP당 이동 거리는 0보다 커야 합니다.", this);
            return false;
        }

        if (unitData.MoveActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}의 {nameof(ControllableUnitData)} 이동 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
