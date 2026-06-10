using System.Collections.Generic;

/// <summary>
/// 격자 보드에서 도달 가능한 칸과 목표 칸까지의 경로를 계산하는 공용 탐색 도구다.
/// 플레이어, 적, 장치 이동 판단이 함께 쓸 수 있도록 턴, AP, 입력 정책은 포함하지 않는다.
/// </summary>
public static class GridPathfinder
{
    // 상하좌우 4방향 이동만 허용하는 S2-T 기본 이동 방향이다.
    private static readonly GridPosition[] CardinalDirections =
    {
        GridPosition.Up,
        GridPosition.Down,
        GridPosition.Left,
        GridPosition.Right,
    };

    /// <summary>
    /// 시작 칸에서 지정한 최대 이동 거리 안에 실제로 도달 가능한 칸 목록을 계산한다.
    /// </summary>
    public static void FindReachablePositions(
        GridManager gridManager,
        GridPosition startPosition,
        int maxDistance,
        List<GridPosition> results)
    {
        results.Clear();

        Dictionary<GridPosition, int> distanceByPosition = new();
        FindReachablePositionDistances(gridManager, startPosition, maxDistance, distanceByPosition);
        foreach (GridPosition position in distanceByPosition.Keys)
        {
            results.Add(position);
        }
    }

    /// <summary>
    /// 시작 칸에서 지정한 최대 이동 거리 안에 도달 가능한 칸과 실제 최단 거리를 계산한다.
    /// </summary>
    public static void FindReachablePositionDistances(
        GridManager gridManager,
        GridPosition startPosition,
        int maxDistance,
        Dictionary<GridPosition, int> results)
    {
        results.Clear();

        if (gridManager == null || maxDistance <= 0 || !gridManager.IsInside(startPosition))
        {
            return;
        }

        Queue<GridPosition> frontier = new();
        Dictionary<GridPosition, int> distanceByPosition = new();

        frontier.Enqueue(startPosition);
        distanceByPosition[startPosition] = 0;

        while (frontier.Count > 0)
        {
            GridPosition current = frontier.Dequeue();
            int currentDistance = distanceByPosition[current];

            if (currentDistance >= maxDistance)
            {
                continue;
            }

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                GridPosition next = current + CardinalDirections[i];
                if (distanceByPosition.ContainsKey(next) || !CanEnterForPath(gridManager, next, startPosition))
                {
                    continue;
                }

                int nextDistance = currentDistance + 1;
                distanceByPosition[next] = nextDistance;
                frontier.Enqueue(next);
                results.Add(next, nextDistance);
            }
        }
    }

    /// <summary>
    /// 시작 칸에서 목표 칸까지 지정한 최대 이동 거리 안의 최단 경로를 계산한다.
    /// 반환 경로에는 시작 칸을 제외하고 실제로 진입할 칸만 담는다.
    /// </summary>
    public static bool TryFindPath(
        GridManager gridManager,
        GridPosition startPosition,
        GridPosition targetPosition,
        int maxDistance,
        List<GridPosition> path)
    {
        path.Clear();

        if (gridManager == null || maxDistance <= 0 || startPosition == targetPosition)
        {
            return false;
        }

        if (!gridManager.IsInside(startPosition) || !CanEnterForPath(gridManager, targetPosition, startPosition))
        {
            return false;
        }

        Queue<GridPosition> frontier = new();
        Dictionary<GridPosition, int> distanceByPosition = new();
        Dictionary<GridPosition, GridPosition> previousByPosition = new();

        frontier.Enqueue(startPosition);
        distanceByPosition[startPosition] = 0;

        while (frontier.Count > 0)
        {
            GridPosition current = frontier.Dequeue();
            int currentDistance = distanceByPosition[current];

            if (current == targetPosition)
            {
                BuildPath(startPosition, targetPosition, previousByPosition, path);
                return path.Count > 0;
            }

            if (currentDistance >= maxDistance)
            {
                continue;
            }

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                GridPosition next = current + CardinalDirections[i];
                if (distanceByPosition.ContainsKey(next) || !CanEnterForPath(gridManager, next, startPosition))
                {
                    continue;
                }

                distanceByPosition[next] = currentDistance + 1;
                previousByPosition[next] = current;
                frontier.Enqueue(next);
            }
        }

        return false;
    }

    /// <summary>
    /// 경로 탐색 중 지정한 칸에 진입할 수 있는지 확인한다.
    /// 시작 칸은 현재 이동 주체가 점유 중일 수 있으므로 예외로 허용한다.
    /// </summary>
    private static bool CanEnterForPath(GridManager gridManager, GridPosition position, GridPosition startPosition)
    {
        return position == startPosition || gridManager.CanEnter(position);
    }

    /// <summary>
    /// 목표 칸에서 시작 칸까지 되짚은 탐색 결과를 실제 이동 순서로 변환한다.
    /// </summary>
    private static void BuildPath(
        GridPosition startPosition,
        GridPosition targetPosition,
        Dictionary<GridPosition, GridPosition> previousByPosition,
        List<GridPosition> path)
    {
        GridPosition current = targetPosition;

        while (current != startPosition)
        {
            path.Add(current);
            current = previousByPosition[current];
        }

        path.Reverse();
    }
}
