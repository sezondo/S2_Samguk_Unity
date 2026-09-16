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
    // 연출 시점보다 먼저 바뀔 수 있는 시야 원점과 벽·닫힌 문을 독립적으로 보존한다.
    private readonly HashSet<GridPosition> sightBlockers;
    // 외부에서 내용을 바꿀 수 없는 원형 시야 원점 목록이다.
    public IReadOnlyList<PlayerVisionSource> Sources { get; }

    public IReadOnlyCollection<GridPosition> VisiblePositions => visiblePositions;
    public IReadOnlyCollection<GridPosition> ExploredPositions => exploredPositions;

    /// <summary>
    /// 현재 시야, 탐색 이력, 적 표시와 광원·차단물 배치를 복사해 독립된 스냅샷을 만든다.
    /// </summary>
    public PlayerVisionSnapshot(
        IEnumerable<GridPosition> visiblePositions,
        IEnumerable<GridPosition> exploredPositions,
        IEnumerable<GridActor> visibleEnemyActors,
        IEnumerable<PlayerVisionSource> sources,
        IEnumerable<GridPosition> sightBlockers)
    {
        this.visiblePositions = new HashSet<GridPosition>(visiblePositions);
        this.exploredPositions = new HashSet<GridPosition>(exploredPositions);
        this.visibleEnemyActors = new HashSet<GridActor>(visibleEnemyActors);
        Sources = new List<PlayerVisionSource>(sources).AsReadOnly();
        this.sightBlockers = new HashSet<GridPosition>(sightBlockers);
    }

    /// <summary>이 스냅샷이 만들어진 시점에 해당 칸이 시야를 차단했는지 확인한다.</summary>
    public bool IsSightBlocked(GridPosition position) => sightBlockers.Contains(position);

    /// <summary>논리 칸 결과가 같아도 광원 위치나 벽이 달라졌는지 검사한다.</summary>
    public bool HasSameGeometry(IReadOnlyList<PlayerVisionSource> sources, HashSet<GridPosition> blockers)
    {
        if (Sources.Count != sources.Count || !sightBlockers.SetEquals(blockers)) return false;
        for (int i = 0; i < Sources.Count; i++)
        {
            if (Sources[i].Origin != sources[i].Origin || Sources[i].Radius != sources[i].Radius ||
                Sources[i].GuaranteeAdjacent != sources[i].GuaranteeAdjacent) return false;
        }
        return true;
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
