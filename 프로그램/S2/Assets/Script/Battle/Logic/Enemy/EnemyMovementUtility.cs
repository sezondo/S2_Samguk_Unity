using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적의 논리 이동, 방향·시야 갱신과 공통 이동 연출 생성을 한 순서로 처리한다.
/// </summary>
public static class EnemyMovementUtility
{
    /// <summary>
    /// 편대 한 칸 이동이 모두 확정된 뒤 구성원 하나의 방향과 시야를 새 위치에 맞춰 갱신한다.
    /// 감지 이벤트는 편대 전체 갱신 뒤 그룹이 등록 순서대로 판정한다.
    /// </summary>
    public static bool RefreshAfterFormationStep(
        EnemyContext enemy,
        GridPosition fromPosition,
        GridPosition toPosition)
    {
        if (enemy == null || enemy.GridActor == null || enemy.GridSight == null)
        {
            return false;
        }

        SetFacingFromMovement(enemy, fromPosition, toPosition);
        enemy.GridSight.RefreshSight();
        return true;
    }

    /// <summary>확정한 이동 칸 수를 반환한다. 이미 발각된 적의 전투·엄폐 이동도 같은 경로를 사용한다.</summary>
    public static int MoveAlongPath(EnemyContext enemy, IReadOnlyList<GridPosition> path,
        ActionResolutionContext context, string message)
        => MoveAlongPath(enemy, path, context, message, out _);

    /// <summary>한 칸마다 감지를 확정하고 발견한 칸에서 원래 이동을 종료한다.</summary>
    public static int MoveAlongPath(EnemyContext enemy, IReadOnlyList<GridPosition> path,
        ActionResolutionContext context, string message, out bool detectedPlayer)
    {
        detectedPlayer = false;
        if (enemy == null || enemy.GridActor == null || enemy.GridSight == null || path == null ||
            context == null || context.HasFailed) return 0;

        // 확정된 칸만 보관해 중간 감지·막힘에서도 마지막 연출을 Single 또는 End로 만든다.
        var steps = new List<(GridPosition from, GridPosition to, GridDirection previous,
            GridDirection direction, EnemyPerceptionChangedLogicEvent perception)>();
        GridPosition previousPosition = enemy.GridActor.GridPosition;
        for (int i = 0; i < path.Count; i++)
        {
            GridPosition nextPosition = path[i];
            if (!enemy.GridActor.TryMoveTo(nextPosition))
            {
                Debug.LogError($"{nameof(EnemyMovementUtility)}: {enemy.name} 적을 {nextPosition} 칸으로 이동시키지 못했습니다.", enemy);
                break;
            }
            GridDirection previousDirection = enemy.GridSight.FacingDirection;
            GridDirection direction = GetDirectionFromMovement(previousPosition, nextPosition);
            enemy.GridSight.SetFacingDirection(direction);
            enemy.GridSight.RefreshSight();
            var perception = EnemyPerceptionCoordinator.CapturePlayerDetection(enemy);
            steps.Add((previousPosition, nextPosition, previousDirection, direction, perception));
            previousPosition = nextPosition;
            detectedPlayer = perception.HasDetectedPlayer;
            if (detectedPlayer) break;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.previous != step.direction && CanPresentFacing(enemy))
                context.EnqueuePresentation(PresentationEvent.EnemyFacingTurn(enemy, step.previous, step.direction,
                    "적 이동 전 시야 방향 전환 연출"));
            context.Publish(new MoveStepEnteredLogicEvent(enemy.GridActor, step.to));
            context.EnqueuePresentation(PresentationEvent.EnemyReactionMove(enemy, step.from, step.to,
                MovePresentationPhaseUtility.GetPhase(i, steps.Count), message));
            context.Publish(step.perception);
        }
        if (steps.Count > 0)
            context.Publish(new MoveCompletedLogicEvent(enemy.GridActor, previousPosition));
        return steps.Count;
    }

    /// <summary>
    /// 적을 지정한 방향으로 돌린 직후 감지를 확정하고 발견 여부를 반환한다.
    /// </summary>
    public static bool FaceDirection(EnemyContext enemy, GridDirection direction, ActionResolutionContext context)
    {
        if (enemy == null || enemy.GridSight == null || context == null)
        {
            return false;
        }

        GridDirection previousDirection = enemy.GridSight.FacingDirection;
        if (!RefreshFacingDirection(enemy, direction))
        {
            return false;
        }

        if (previousDirection != direction && CanPresentFacing(enemy))
        {
            context.EnqueuePresentation(PresentationEvent.EnemyFacingTurn(
                enemy,
                previousDirection,
                direction,
                "적 제자리 시야 방향 전환 연출"));
        }

        var perception = EnemyPerceptionCoordinator.CapturePlayerDetection(enemy);
        context.Publish(perception);
        return perception.HasDetectedPlayer;
    }

    /// <summary>
    /// 적의 논리 시야 방향을 지정하고 현재 위치에서 시야 칸을 다시 계산한다.
    /// 감지 논리 이벤트와 방향 전환 연출은 호출자가 필요한 순서로 별도 생성한다.
    /// </summary>
    public static bool RefreshFacingDirection(EnemyContext enemy, GridDirection direction)
    {
        if (enemy == null || enemy.GridSight == null)
        {
            return false;
        }

        enemy.GridSight.SetFacingDirection(direction);
        enemy.GridSight.RefreshSight();
        return true;
    }

    /// <summary>
    /// 지정한 칸을 바라본 직후 감지를 확정하고 발견 여부를 반환한다.
    /// </summary>
    public static bool FacePosition(EnemyContext enemy, GridPosition targetPosition, ActionResolutionContext context)
    {
        if (enemy == null || enemy.GridActor == null)
        {
            return false;
        }

        GridPosition offset = targetPosition - enemy.GridActor.GridPosition;
        if (offset == GridPosition.Zero)
        {
            return false;
        }

        GridDirection direction = Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)
            ? (offset.x >= 0 ? GridDirection.Right : GridDirection.Left)
            : (offset.y >= 0 ? GridDirection.Up : GridDirection.Down);
        return FaceDirection(enemy, direction, context);
    }

    /// <summary>
    /// 실제 한 칸 이동 방향을 적의 시야 방향으로 적용한다.
    /// </summary>
    private static void SetFacingFromMovement(EnemyContext enemy, GridPosition fromPosition, GridPosition toPosition)
    {
        enemy.GridSight.SetFacingDirection(GetDirectionFromMovement(fromPosition, toPosition));
    }

    /// <summary>
    /// 인접한 두 칸의 차이를 이동 시야에 사용할 상하좌우 방향으로 변환한다.
    /// </summary>
    public static GridDirection GetDirectionFromMovement(GridPosition fromPosition, GridPosition toPosition)
    {
        GridPosition offset = toPosition - fromPosition;
        return offset switch
        {
            var value when value == GridPosition.Up => GridDirection.Up,
            var value when value == GridPosition.Down => GridDirection.Down,
            var value when value == GridPosition.Left => GridDirection.Left,
            _ => GridDirection.Right,
        };
    }

    /// <summary>
    /// 전방 시야 표시를 사용하는 평상 또는 의심 상태인지 확인한다.
    /// </summary>
    private static bool CanPresentFacing(EnemyContext enemy)
    {
        return enemy != null && enemy.AlertState != null && !enemy.AlertState.IsAlerted;
    }
}
