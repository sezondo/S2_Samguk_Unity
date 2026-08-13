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

    /// <summary>
    /// 지정한 경로를 따라 적을 이동시키고 실제로 이동한 칸 수를 반환한다.
    /// </summary>
    public static int MoveAlongPath(
        EnemyContext enemy,
        IReadOnlyList<GridPosition> path,
        ActionResolutionContext context,
        string message)
    {
        if (enemy == null || enemy.GridActor == null || enemy.GridSight == null || path == null || context == null)
        {
            return 0;
        }

        int movedSteps = 0;
        GridPosition previousPosition = enemy.GridActor.GridPosition;
        for (int i = 0; i < path.Count; i++)
        {
            GridPosition nextPosition = path[i];
            if (!enemy.GridActor.TryMoveTo(nextPosition))
            {
                Debug.LogError($"{nameof(EnemyMovementUtility)}: {enemy.name} 적을 {nextPosition} 칸으로 이동시키지 못했습니다.", enemy);
                break;
            }

            SetFacingFromMovement(enemy, previousPosition, nextPosition);
            enemy.GridSight.RefreshSight();
            context.Publish(new MoveStepEnteredLogicEvent(enemy.GridActor, nextPosition));
            context.Publish(new EnemyPerceptionChangedLogicEvent(enemy));
            context.EnqueuePresentation(PresentationEvent.EnemyReactionMove(
                enemy,
                previousPosition,
                nextPosition,
                MovePresentationPhaseUtility.GetPhase(i, path.Count),
                message));

            previousPosition = nextPosition;
            movedSteps++;
        }

        if (movedSteps > 0)
        {
            context.Publish(new MoveCompletedLogicEvent(enemy.GridActor, enemy.GridActor.GridPosition));
        }

        return movedSteps;
    }

    /// <summary>
    /// 적을 지정한 방향으로 돌리고 플레이어 감지 재검사 이벤트를 발행한다.
    /// </summary>
    public static void FaceDirection(EnemyContext enemy, GridDirection direction, ActionResolutionContext context)
    {
        if (enemy == null || enemy.GridSight == null || context == null)
        {
            return;
        }

        enemy.GridSight.SetFacingDirection(direction);
        enemy.GridSight.RefreshSight();
        context.Publish(new EnemyPerceptionChangedLogicEvent(enemy));
    }

    /// <summary>
    /// 적이 지정한 칸을 향하도록 가장 큰 축 기준의 4방향을 선택한다.
    /// </summary>
    public static void FacePosition(EnemyContext enemy, GridPosition targetPosition, ActionResolutionContext context)
    {
        if (enemy == null || enemy.GridActor == null)
        {
            return;
        }

        GridPosition offset = targetPosition - enemy.GridActor.GridPosition;
        if (offset == GridPosition.Zero)
        {
            return;
        }

        GridDirection direction = Mathf.Abs(offset.x) >= Mathf.Abs(offset.y)
            ? (offset.x >= 0 ? GridDirection.Right : GridDirection.Left)
            : (offset.y >= 0 ? GridDirection.Up : GridDirection.Down);
        FaceDirection(enemy, direction, context);
    }

    /// <summary>
    /// 실제 한 칸 이동 방향을 적의 시야 방향으로 적용한다.
    /// </summary>
    private static void SetFacingFromMovement(EnemyContext enemy, GridPosition fromPosition, GridPosition toPosition)
    {
        GridPosition offset = toPosition - fromPosition;
        GridDirection direction = offset switch
        {
            var value when value == GridPosition.Up => GridDirection.Up,
            var value when value == GridPosition.Down => GridDirection.Down,
            var value when value == GridPosition.Left => GridDirection.Left,
            _ => GridDirection.Right,
        };
        enemy.GridSight.SetFacingDirection(direction);
    }
}
