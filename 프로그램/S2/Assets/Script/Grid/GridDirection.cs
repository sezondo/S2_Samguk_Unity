/// <summary>
/// 그리드 기준 4방향을 나타낸다.
/// 대각선 방향은 바라보는 방향으로 사용하지 않는다.
/// </summary>
public enum GridDirection
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// GridDirection을 GridPosition 방향 값으로 변환하는 유틸리티다.
/// </summary>
public static class GridDirectionUtility
{
    /// <summary>
    /// 지정한 방향의 전방 한 칸 오프셋을 반환한다.
    /// </summary>
    public static GridPosition ToForwardOffset(GridDirection direction)
    {
        return direction switch
        {
            GridDirection.Up => GridPosition.Up,
            GridDirection.Down => GridPosition.Down,
            GridDirection.Left => GridPosition.Left,
            GridDirection.Right => GridPosition.Right,
            _ => GridPosition.Up,
        };
    }

    /// <summary>
    /// 지정한 방향 기준 오른쪽 한 칸 오프셋을 반환한다.
    /// </summary>
    public static GridPosition ToRightOffset(GridDirection direction)
    {
        return direction switch
        {
            GridDirection.Up => GridPosition.Right,
            GridDirection.Down => GridPosition.Left,
            GridDirection.Left => GridPosition.Up,
            GridDirection.Right => GridPosition.Down,
            _ => GridPosition.Right,
        };
    }
}
