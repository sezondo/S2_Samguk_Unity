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
    [SerializeField] private bool logBlockedMove = true;
    [SerializeField] private bool logFailedMoveRequest = true;

    private GridActor actor;

    private void Awake()
    {
        actor = GetComponent<GridActor>();
    }

    private void Update()
    {
        if (!TryReadMoveInput(out GridPosition direction))
        {
            return;
        }

        TryMove(direction);
    }

    private void TryMove(GridPosition direction)
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null || actor == null)
        {
            return;
        }

        GridPosition targetPosition = actor.GridPosition + direction;
        if (!gridManager.CanEnter(targetPosition))
        {
            if (logBlockedMove)
            {
                Debug.Log($"{nameof(GridPlayerDebugMover)} blocked move to {targetPosition}.", this);
            }

            return;
        }

        if (!actor.TryMoveTo(targetPosition) && logFailedMoveRequest)
        {
            Debug.LogWarning($"{nameof(GridPlayerDebugMover)} failed to move from {actor.GridPosition} to {targetPosition}.", this);
        }
    }

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
