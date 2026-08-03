using System.Collections.Generic;

/// <summary>
/// 특정 논리 시점의 플레이어 현재 시야와 누적 탐색 상태를 보존하는 읽기 전용 스냅샷이다.
/// 연출 큐가 나중에 재생돼도 각 이동 칸 당시의 시야를 그대로 적용할 수 있게 한다.
/// </summary>
public sealed class PlayerVisionSnapshot
{
    private readonly HashSet<GridPosition> visiblePositions;
    private readonly HashSet<GridPosition> exploredPositions;
    private readonly HashSet<GridActor> visibleEnemyActors;

    public IReadOnlyCollection<GridPosition> VisiblePositions => visiblePositions;
    public IReadOnlyCollection<GridPosition> ExploredPositions => exploredPositions;

    /// <summary>
    /// 지정한 현재 시야와 탐색 이력을 복사해 독립된 스냅샷을 만든다.
    /// </summary>
    public PlayerVisionSnapshot(
        IEnumerable<GridPosition> visiblePositions,
        IEnumerable<GridPosition> exploredPositions,
        IEnumerable<GridActor> visibleEnemyActors)
    {
        this.visiblePositions = new HashSet<GridPosition>(visiblePositions);
        this.exploredPositions = new HashSet<GridPosition>(exploredPositions);
        this.visibleEnemyActors = new HashSet<GridActor>(visibleEnemyActors);
    }

    /// <summary>
    /// 지정한 칸이 현재 플레이어 시야 안인지 확인한다.
    /// </summary>
    public bool IsVisible(GridPosition position)
    {
        return visiblePositions.Contains(position);
    }

    /// <summary>
    /// 스냅샷 생성 시점에 지정한 적 Actor가 현재 시야 안에 있었는지 확인한다.
    /// </summary>
    public bool IsEnemyActorVisible(GridActor actor)
    {
        return actor != null && visibleEnemyActors.Contains(actor);
    }

    /// <summary>
    /// 지정한 칸의 미탐색·탐색·현재 시야 상태를 반환한다.
    /// </summary>
    public GridVisibilityState GetState(GridPosition position)
    {
        if (visiblePositions.Contains(position))
        {
            return GridVisibilityState.Visible;
        }

        return exploredPositions.Contains(position)
            ? GridVisibilityState.Explored
            : GridVisibilityState.Unexplored;
    }
}
