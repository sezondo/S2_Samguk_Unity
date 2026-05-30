using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 그리드 이동 확인용 임시 디버그 스크립트다.
/// 정식 PlayerInput을 붙이기 전까지 WASD로 GridActor 이동이 정상 동작하는지 확인한다.
/// </summary>
[RequireComponent(typeof(GridActor))]
public class GridPlayerDebugMover : MonoBehaviour
{
    [Header("Debug")]
    // WASD 디버그 이동 1칸마다 소비할 AP 비용이다.
    [SerializeField] private int moveActionPointCost = 1;
    // 디버그 이동 비용을 소비할 AP 컴포넌트다. 비어 있으면 같은 오브젝트에서 찾는다.
    [SerializeField] private ActionPoint actionPoint;
    // true면 플레이어 턴일 때만 WASD 디버그 이동을 허용한다.
    [SerializeField] private bool requirePlayerTurn = true;
    // 목표 칸이 막혔을 때 로그를 출력할지 정한다.
    [SerializeField] private bool logBlockedMove = true;
    // GridActor 이동 요청 자체가 실패했을 때 경고 로그를 출력할지 정한다.
    [SerializeField] private bool logFailedMoveRequest = true;
    // 턴 조건 또는 AP 부족으로 이동이 막혔을 때 로그를 출력할지 정한다.
    [SerializeField] private bool logTurnOrApBlockedMove = true;

    // 실제 격자 위치와 점유 이동을 처리하는 말 컴포넌트다.
    private GridActor actor;

    /// <summary>
    /// 디버그 이동에 필요한 같은 오브젝트의 컴포넌트 참조를 준비한다.
    /// </summary>
    private void Awake()
    {
        actor = GetComponent<GridActor>();
        if (actionPoint == null)
        {
            actionPoint = GetComponent<ActionPoint>();
        }
    }

    /// <summary>
    /// 매 프레임 WASD 입력을 확인해 디버그 그리드 이동을 요청한다.
    /// </summary>
    private void Update()
    {
        if (!TryReadMoveInput(out GridPosition direction))
        {
            return;
        }

        TryMove(direction);
    }

    /// <summary>
    /// 입력 방향으로 한 칸 이동을 시도하고 필요하면 AP를 소비한다.
    /// </summary>
    private void TryMove(GridPosition direction)
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || actor == null)
        {
            return;
        }

        if (!CanRequestMove())
        {
            return;
        }

        GridPosition targetPosition = actor.GridPosition + direction;
        if (!gridManager.CanEnter(targetPosition))
        {
            if (logBlockedMove)
            {
                Debug.Log($"{nameof(GridPlayerDebugMover)}: {targetPosition} 칸으로 이동할 수 없습니다.", this);
            }

            return;
        }

        if (!actor.TryMoveTo(targetPosition))
        {
            if (logFailedMoveRequest)
            {
                Debug.LogWarning($"{nameof(GridPlayerDebugMover)}: {actor.GridPosition} 칸에서 {targetPosition} 칸으로 이동 요청이 실패했습니다.", this);
            }

            return;
        }

        if (actionPoint != null && moveActionPointCost > 0 && !actionPoint.TrySpend(moveActionPointCost))
        {
            Debug.LogWarning($"{nameof(GridPlayerDebugMover)}: 이동은 되었지만 AP 소비에 실패했습니다. AP 흐름을 확인해야 합니다.", this);
        }
    }

    /// <summary>
    /// 현재 턴과 AP 상태 기준으로 디버그 이동을 요청할 수 있는지 확인한다.
    /// </summary>
    private bool CanRequestMove()
    {
        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logTurnOrApBlockedMove)
            {
                Debug.Log($"{nameof(GridPlayerDebugMover)}: 현재 턴이 {turnManager.CurrentSide}라서 이동할 수 없습니다.", this);
            }

            return false;
        }

        if (actionPoint != null && moveActionPointCost > 0 && !actionPoint.CanSpend(moveActionPointCost))
        {
            if (logTurnOrApBlockedMove)
            {
                Debug.Log($"{nameof(GridPlayerDebugMover)}: AP가 부족해서 이동할 수 없습니다. 필요 AP: {moveActionPointCost}, 현재 AP: {actionPoint.Current}", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// WASD 키 입력을 그리드 방향 값으로 변환한다.
    /// </summary>
    private static bool TryReadMoveInput(out GridPosition direction)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            direction = GridPosition.Zero;
            return false;
        }

        if (keyboard.wKey.wasPressedThisFrame)
        {
            direction = GridPosition.Up;
            return true;
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            direction = GridPosition.Down;
            return true;
        }

        if (keyboard.aKey.wasPressedThisFrame)
        {
            direction = GridPosition.Left;
            return true;
        }

        if (keyboard.dKey.wasPressedThisFrame)
        {
            direction = GridPosition.Right;
            return true;
        }

        direction = GridPosition.Zero;
        return false;
    }
}
