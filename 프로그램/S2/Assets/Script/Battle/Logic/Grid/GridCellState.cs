/// <summary>
/// 격자 보드의 한 칸이 가진 현재 상태를 보관한다.
/// 게임 규칙 판단은 하지 않고, GridManager가 승인한 상태만 기록한다.
/// </summary>
public sealed class GridCellState
{
    /// <summary>
    /// 이 상태가 나타내는 보드 칸 좌표다.
    /// </summary>
    public GridPosition Position { get; }

    /// <summary>
    /// 벽, 장애물, 낭떠러지처럼 고정 이동불가 칸인지 나타낸다.
    /// </summary>
    public bool IsBlocked { get; private set; }

    /// <summary>
    /// 현재 이 칸을 점유 중인 보드 말이다.
    /// </summary>
    public GridActor OccupiedActor { get; private set; }

    /// <summary>
    /// 현재 다른 말이 이 칸을 점유 중인지 나타낸다.
    /// </summary>
    public bool IsOccupied => OccupiedActor != null;

    /// <summary>
    /// 기본 이동 규칙 기준으로 이 칸에 새 말이 들어갈 수 있는지 나타낸다.
    /// </summary>
    public bool CanEnter => !IsBlocked && !IsOccupied;

    /// <summary>
    /// 지정한 좌표를 가진 칸 상태를 만든다.
    /// </summary>
    public GridCellState(GridPosition position)
    {
        Position = position;
    }

    /// <summary>
    /// GridManager가 승인한 고정 이동불가 상태를 기록한다.
    /// </summary>
    internal void SetBlocked(bool isBlocked)
    {
        IsBlocked = isBlocked;
    }

    /// <summary>
    /// GridManager가 승인한 점유자를 기록한다.
    /// </summary>
    internal void SetOccupiedActor(GridActor actor)
    {
        OccupiedActor = actor;
    }

    /// <summary>
    /// 지정한 말이 현재 점유자일 때만 점유 정보를 비운다.
    /// </summary>
    internal bool TryClearOccupiedActor(GridActor actor)
    {
        if (OccupiedActor != actor)
        {
            return false;
        }

        OccupiedActor = null;
        return true;
    }
}
