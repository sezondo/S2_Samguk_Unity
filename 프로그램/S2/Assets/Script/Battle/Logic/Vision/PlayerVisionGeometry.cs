using System;
using UnityEngine;

/// <summary>아군 본체와 검이 칸 내부에서 모서리를 살피는 시야 계산을 공유한다. 공격·적 감지는 변경하지 않는다.</summary>
public static class PlayerVisionGeometry
{
    // 벽 경계를 넘지 않는 중앙·상하좌우 관측 표본의 칸 단위 간격이다.
    private const float SampleOffset = 0.3f;
    private static readonly Vector2[] Offsets =
    {
        Vector2.zero, new(SampleOffset, 0f), new(-SampleOffset, 0f),
        new(0f, SampleOffset), new(0f, -SampleOffset)
    };

    /// <summary>현재 그리드의 아군 관측 판정을 계산한다. 근접 범위도 벽 차단을 따른다.</summary>
    public static bool CanObserve(GridManager grid, GridPosition origin, GridPosition target, int radius, bool adjacent)
    {
        return grid != null && grid.IsInside(origin) && grid.IsInside(target) &&
            CanSeeCell(new PlayerVisionSource(origin, radius, adjacent), target, grid.IsSightBlocked);
    }

    /// <summary>중앙이 보이거나 주변 표본 두 곳 이상이 보여야 칸 전체의 적을 식별한다.</summary>
    public static bool CanSeeCell(PlayerVisionSource source, GridPosition target, Func<GridPosition, bool> blocked)
    {
        if (!IsInRange(source, target.x, target.y)) return false;
        if (CanSeePoint(source, target.x, target.y, blocked)) return true;
        int visibleSamples = 0;
        for (int i = 1; i < Offsets.Length; i++)
        {
            Vector2 offset = Offsets[i];
            if (CanSeePoint(source, target.x + offset.x, target.y + offset.y, blocked) && ++visibleSamples >= 2)
                return true;
        }
        return false;
    }

    /// <summary>같은 칸 내부의 다섯 출발점 중 하나에서 열린 공간으로 이어지는 세부 지점을 관측한다.</summary>
    public static bool CanSeePoint(PlayerVisionSource source, float targetX, float targetY, Func<GridPosition, bool> blocked)
    {
        if (!IsInRange(source, targetX, targetY) || blocked(source.Origin)) return false;
        for (int i = 0; i < Offsets.Length; i++)
        {
            Vector2 offset = Offsets[i];
            if (Trace(source.Origin.x + offset.x, source.Origin.y + offset.y, targetX, targetY, blocked))
                return true;
        }
        return false;
    }

    /// <summary>시야 거리는 이동한 관측 표본이 아닌 원래 칸 중심 기준으로 유지한다.</summary>
    private static bool IsInRange(PlayerVisionSource source, float targetX, float targetY)
    {
        float dx = targetX - source.Origin.x, dy = targetY - source.Origin.y;
        int cellX = Mathf.FloorToInt(targetX + 0.5f), cellY = Mathf.FloorToInt(targetY + 0.5f);
        return source.GuaranteeAdjacent && Mathf.Abs(cellX - source.Origin.x) <= 1 && Mathf.Abs(cellY - source.Origin.y) <= 1 ||
            dx * dx + dy * dy <= source.Radius * source.Radius;
    }

    /// <summary>연속 좌표 사이의 셀 경계를 검사한다. 벽 표면은 보이지만 닫힌 대각선 틈은 통과하지 않는다.</summary>
    private static bool Trace(float fromX, float fromY, float toX, float toY, Func<GridPosition, bool> blocked)
    {
        int x = Mathf.FloorToInt(fromX + 0.5f), y = Mathf.FloorToInt(fromY + 0.5f);
        int endX = Mathf.FloorToInt(toX + 0.5f), endY = Mathf.FloorToInt(toY + 0.5f);
        float dx = toX - fromX, dy = toY - fromY;
        int stepX = Math.Sign(dx), stepY = Math.Sign(dy);
        float deltaX = stepX == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(dx);
        float deltaY = stepY == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(dy);
        float nextX = stepX == 0 ? float.PositiveInfinity : (x + stepX * 0.5f - fromX) / dx;
        float nextY = stepY == 0 ? float.PositiveInfinity : (y + stepY * 0.5f - fromY) / dy;
        int maxSteps = Mathf.Abs(endX - x) + Mathf.Abs(endY - y) + 1;
        for (int i = 0; i < maxSteps && (x != endX || y != endY); i++)
        {
            if (Mathf.Abs(nextX - nextY) < 0.000001f)
            {
                if (blocked(new GridPosition(x + stepX, y)) && blocked(new GridPosition(x, y + stepY))) return false;
                x += stepX; y += stepY; nextX += deltaX; nextY += deltaY;
            }
            else if (nextX < nextY) { x += stepX; nextX += deltaX; }
            else { y += stepY; nextY += deltaY; }
            if ((x != endX || y != endY) && blocked(new GridPosition(x, y))) return false;
        }
        return x == endX && y == endY;
    }
}
