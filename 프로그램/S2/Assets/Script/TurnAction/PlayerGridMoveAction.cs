using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 정식 그리드 이동 행동을 담당한다.
/// UI가 붙기 전까지는 디버그 키로 이동 행동 선택과 클릭 실행만 검증한다.
/// </summary>
[RequireComponent(typeof(GridActor))]
public class PlayerGridMoveAction : MonoBehaviour
{
    [Header("Move Action")]
    // 플레이어 공통 참조와 턴 데이터를 제공하는 필수 Context다.
    [SerializeField] private PlayerContext playerContext;
    // true면 플레이어 턴일 때만 이동 행동을 선택하고 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Input")]
    // 마우스 화면 좌표를 월드 좌표로 바꿀 카메라다. 비어 있으면 Camera.main을 사용한다.
    [SerializeField] private Camera worldCamera;
    // UI 버튼이 붙기 전까지 키보드로 이동 행동 선택을 검증할지 정한다.
    [SerializeField] private bool allowDebugKeyboardSelect = true;
    // 디버그 이동 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectMoveKey = Key.M;
    // true면 우클릭으로 현재 이동 행동 선택을 취소한다.
    [SerializeField] private bool cancelByRightClick = true;
    // true면 Escape 키로 현재 이동 행동 선택을 취소한다.
    [SerializeField] private bool cancelByEscape = true;

    [Header("Log")]
    // 이동 행동 선택, 취소, 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 이동 불가 목표를 클릭했을 때 차단 사유 로그를 출력할지 정한다.
    [SerializeField] private bool logBlockedTarget = true;

    // 현재 이동 행동 선택 상태에서 실제로 이동 가능한 칸 목록이다.
    private readonly List<GridPosition> movablePositions = new();
    // 목표 칸까지 한 칸씩 이동할 경로를 임시로 담는 재사용 버퍼다.
    private readonly List<GridPosition> movePathBuffer = new();

    // 같은 오브젝트의 그리드 말 컴포넌트다.
    private GridActor actor;
    // 플레이어가 현재 이동 행동을 선택한 상태인지 나타낸다.
    private bool isMoveSelected;

    // 외부 UI나 표시 컴포넌트가 이동 선택 상태를 읽을 때 사용한다.
    public bool IsMoveSelected => isMoveSelected;
    // 음수 입력을 막은 실제 이동 거리 값이다.
    public int MoveRange => playerContext.TurnData.MoveRange;

    // 이동 가능 칸 목록을 화면에 표시해야 할 때 발생한다.
    public event Action<IReadOnlyList<GridPosition>> MoveRangeShown;
    // 이동 가능 칸 표시를 지워야 할 때 발생한다.
    public event Action MoveRangeHidden;
    // 플레이어가 경로상의 한 칸에 진입할 때마다 발생한다. 적 시야 검사 연결 지점이다.
    public event Action<GridPosition> MoveStepEntered;
    // 이동 행동이 최종 도착 칸까지 끝났을 때 발생한다.
    public event Action<GridPosition> MoveCompleted;

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
    /// 디버그 행동 선택, 행동 취소, 목표 칸 클릭 입력을 매 프레임 확인한다.
    /// </summary>
    private void Update()
    {
        HandleDebugSelectInput();

        if (!isMoveSelected)
        {
            return;
        }

        HandleCancelInput();
        HandleTargetClickInput();
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
        if (!isMoveSelected)
        {
            return;
        }

        isMoveSelected = false;
        movablePositions.Clear();
        MoveRangeHidden?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGridMoveAction)}: 이동 행동 선택을 취소했습니다.", this);
        }
    }

    /// <summary>
    /// 선택된 목표 칸으로 이동 행동을 실행한다.
    /// </summary>
    public bool TryExecuteMoveTo(GridPosition targetPosition)
    {
        if (!isMoveSelected)
        {
            return false;
        }

        if (!IsValidMoveTarget(targetPosition))
        {
            LogBlockedTarget(targetPosition, "목표 칸이 현재 이동 범위 밖이거나 진입할 수 없는 칸입니다");
            return false;
        }

        if (!TryBuildSimplePath(actor.GridPosition, targetPosition, movePathBuffer))
        {
            LogBlockedTarget(targetPosition, "단순 이동 경로가 막혀 있습니다");
            return false;
        }

        // 이동 행동은 시작 시점에 AP를 소비한다.
        int cost = playerContext.TurnData.MoveActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.TrySpend(cost))
        {
            LogBlockedTarget(targetPosition, "AP가 부족합니다");
            return false;
        }

        foreach (GridPosition step in movePathBuffer)
        {
            if (!actor.TryMoveTo(step))
            {
                LogBlockedTarget(step, "GridActor 이동 요청이 실패했습니다");
                CancelMoveAction();
                return false;
            }

            MoveStepEntered?.Invoke(step);
        }

        isMoveSelected = false;
        movablePositions.Clear();
        MoveRangeHidden?.Invoke();
        MoveCompleted?.Invoke(actor.GridPosition);

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGridMoveAction)}: {actor.GridPosition} 칸으로 이동했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// UI 버튼이 없을 때 사용할 디버그 이동 행동 선택 키 입력을 처리한다.
    /// </summary>
    private void HandleDebugSelectInput()
    {
        if (!allowDebugKeyboardSelect || debugSelectMoveKey == Key.None || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[debugSelectMoveKey].wasPressedThisFrame)
        {
            SelectMoveAction();
        }
    }

    /// <summary>
    /// 이동 행동 선택 상태에서 취소 입력을 처리한다.
    /// </summary>
    private void HandleCancelInput()
    {
        if (cancelByEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelMoveAction();
            return;
        }

        if (cancelByRightClick && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelMoveAction();
        }
    }

    /// <summary>
    /// 이동 행동 선택 상태에서 마우스 좌클릭 목표 칸 입력을 처리한다.
    /// </summary>
    private void HandleTargetClickInput()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (!TryGetMouseGridPosition(out GridPosition targetPosition))
        {
            return;
        }

        TryExecuteMoveTo(targetPosition);
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

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGridMoveAction)}: 현재 턴이 {turnManager.CurrentSide}라서 이동 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        int cost = playerContext.TurnData.MoveActionPointCost;
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
        movablePositions.Clear();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || actor == null)
        {
            return;
        }

        GridPosition origin = actor.GridPosition;
        int range = MoveRange;

        for (int x = origin.x - range; x <= origin.x + range; x++)
        {
            for (int y = origin.y - range; y <= origin.y + range; y++)
            {
                GridPosition candidate = new(x, y);
                if (candidate == origin || origin.ManhattanDistanceTo(candidate) > range)
                {
                    continue;
                }

                if (gridManager.CanEnter(candidate))
                {
                    movablePositions.Add(candidate);
                }
            }
        }
    }

    /// <summary>
    /// 목표 칸이 현재 이동 행동에서 선택 가능한 칸인지 확인한다.
    /// </summary>
    private bool IsValidMoveTarget(GridPosition targetPosition)
    {
        if (!CanSelectMoveAction())
        {
            return false;
        }

        for (int i = 0; i < movablePositions.Count; i++)
        {
            if (movablePositions[i] == targetPosition)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 시작 칸에서 목표 칸까지 X축 우선 단순 맨해튼 경로를 만든다.
    /// </summary>
    private bool TryBuildSimplePath(GridPosition from, GridPosition to, List<GridPosition> path)
    {
        path.Clear();

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            return false;
        }

        GridPosition current = from;
        while (current.x != to.x)
        {
            int nextX = current.x + Math.Sign(to.x - current.x);
            current = new GridPosition(nextX, current.y);
            if (!CanStepThrough(gridManager, current, to))
            {
                return false;
            }

            path.Add(current);
        }

        while (current.y != to.y)
        {
            int nextY = current.y + Math.Sign(to.y - current.y);
            current = new GridPosition(current.x, nextY);
            if (!CanStepThrough(gridManager, current, to))
            {
                return false;
            }

            path.Add(current);
        }

        return path.Count > 0;
    }

    /// <summary>
    /// 단순 경로의 한 칸을 지나가거나 도착할 수 있는지 확인한다.
    /// </summary>
    private bool CanStepThrough(GridManager gridManager, GridPosition position, GridPosition targetPosition)
    {
        if (position == targetPosition)
        {
            return gridManager.CanEnter(position);
        }

        // 정식 경로 탐색 전까지는 X축 우선 맨해튼 경로의 중간 칸도 비어 있어야 한다.
        return gridManager.CanEnter(position);
    }

    /// <summary>
    /// 현재 마우스 화면 좌표를 보드 칸 좌표로 변환한다.
    /// </summary>
    private bool TryGetMouseGridPosition(out GridPosition gridPosition)
    {
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            gridPosition = GridPosition.Zero;
            return false;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));
        gridPosition = GridManager.Instance.WorldToGrid(worldPosition);
        return true;
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
    /// 이동 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
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
        PlayerTurnData turnData = playerContext.TurnData;
        if (turnData == null)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}에는 {nameof(PlayerTurnData)} 참조가 필요합니다.", this);
            return false;
        }

        if (turnData.MoveRange <= 0)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}의 {nameof(PlayerTurnData)} 이동 범위는 0보다 커야 합니다.", this);
            return false;
        }

        if (turnData.MoveActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerGridMoveAction)} on {name}의 {nameof(PlayerTurnData)} 이동 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
