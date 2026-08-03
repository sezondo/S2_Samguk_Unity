using System;
using UnityEngine;

/// <summary>
/// 두 그리드 칸 사이가 고정·동적 시야 차단물에 가리지 않는지 계산한다.
/// 적 시야와 플레이어 시야가 같은 모서리 차단 규칙을 공유하도록 순수 계산만 담당한다.
/// </summary>
public static class GridLineOfSight
{
    /// <summary>
    /// 시작 칸에서 목표 칸까지 시선이 이어지는지 확인한다.
    /// 목표 칸 자체가 차단 칸이어도 그 칸의 벽 표면은 볼 수 있도록 목표 칸은 가림 검사에서 제외한다.
    /// </summary>
    public static bool HasLineOfSight(GridManager gridManager, GridPosition origin, GridPosition target)
    {
        if (gridManager == null || !gridManager.IsInside(origin) || !gridManager.IsInside(target))
        {
            return false;
        }

        if (origin == target)
        {
            return true;
        }

        int deltaX = target.x - origin.x;
        int deltaY = target.y - origin.y;
        int absoluteDeltaX = Mathf.Abs(deltaX);
        int absoluteDeltaY = Mathf.Abs(deltaY);
        int stepX = Math.Sign(deltaX);
        int stepY = Math.Sign(deltaY);

        int currentX = origin.x;
        int currentY = origin.y;
        int horizontalSteps = 0;
        int verticalSteps = 0;

        while (horizontalSteps < absoluteDeltaX || verticalSteps < absoluteDeltaY)
        {
            int horizontalDecision = (1 + 2 * horizontalSteps) * absoluteDeltaY;
            int verticalDecision = (1 + 2 * verticalSteps) * absoluteDeltaX;

            if (horizontalDecision == verticalDecision)
            {
                GridPosition horizontalSide = new(currentX + stepX, currentY);
                GridPosition verticalSide = new(currentX, currentY + stepY);
                if (gridManager.IsSightBlocked(horizontalSide) &&
                    gridManager.IsSightBlocked(verticalSide))
                {
                    return false;
                }

                currentX += stepX;
                currentY += stepY;
                horizontalSteps++;
                verticalSteps++;
            }
            else if (horizontalDecision < verticalDecision)
            {
                currentX += stepX;
                horizontalSteps++;
            }
            else
            {
                currentY += stepY;
                verticalSteps++;
            }

            GridPosition currentPosition = new(currentX, currentY);
            if (currentPosition != target && gridManager.IsSightBlocked(currentPosition))
            {
                return false;
            }
        }

        return true;
    }
}
