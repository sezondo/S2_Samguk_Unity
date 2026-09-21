using UnityEngine;

/// <summary>플레이어와 적이 공유하는 공격 거리와 장애물 시야 규칙이다.</summary>
public static class CombatTargetRules
{
    /// <summary>기존 플레이어와 같은 대각선 포함 8방향 근접 범위다.</summary>
    public static bool IsMeleeAdjacent(GridPosition a, GridPosition b)
    {
        int x = Mathf.Abs(a.x - b.x), y = Mathf.Abs(a.y - b.y);
        return x <= 1 && y <= 1 && x + y > 0;
    }

    /// <summary>합산 시야로 식별한 대상이 무기 맨해튼 사거리 안인지 검사한다.</summary>
    public static bool CanRangedAttack(GridPosition from, GridPosition to, int range, bool identified) => identified && from != to && from.ManhattanDistanceTo(to) <= range;
    /// <summary>아군 합산 시야의 관측자 하나를 검사한다. 기존 인접 8칸 보장 규칙을 공유한다.</summary>
    public static bool CanObserve(GridManager grid, GridPosition from, GridPosition to, int range, bool guaranteeAdjacent)
    {
        if (grid == null || !grid.IsInside(from) || !grid.IsInside(to))
            return false;
        int x = to.x - from.x, y = to.y - from.y;
        if (guaranteeAdjacent && Mathf.Abs(x) <= 1 && Mathf.Abs(y) <= 1)
            return true;
        return x * x + y * y <= range * range && GridLineOfSight.HasLineOfSight(grid, from, to);
    }
}
