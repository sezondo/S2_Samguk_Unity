using System;
using UnityEngine;

/// <summary>
/// S2-T의 보드 칸 좌표다.
/// Unity 월드 좌표와 분리해서 턴제 규칙은 이 좌표를 기준으로 계산한다.
/// </summary>
[Serializable]
public struct GridPosition : IEquatable<GridPosition>
{
    // 보드의 가로축 칸 좌표다.
    public int x;
    // 보드의 세로축 칸 좌표다.
    public int y;

    /// <summary>
    /// 지정한 x, y 칸 좌표로 GridPosition을 만든다.
    /// </summary>
    public GridPosition(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static GridPosition Zero => new(0, 0);
    public static GridPosition Up => new(0, 1);
    public static GridPosition Down => new(0, -1);
    public static GridPosition Left => new(-1, 0);
    public static GridPosition Right => new(1, 0);

    /// <summary>
    /// Unity의 Vector2Int 좌표로 변환한다.
    /// </summary>
    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 다른 칸까지의 맨해튼 거리를 계산한다.
    /// </summary>
    public int ManhattanDistanceTo(GridPosition other)
    {
        return Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y);
    }

    /// <summary>
    /// 다른 GridPosition과 같은 칸인지 비교한다.
    /// </summary>
    public bool Equals(GridPosition other)
    {
        return x == other.x && y == other.y;
    }

    /// <summary>
    /// object 비교에서 같은 GridPosition 값인지 확인한다.
    /// </summary>
    public override bool Equals(object obj)
    {
        return obj is GridPosition other && Equals(other);
    }

    /// <summary>
    /// Dictionary 키로 사용할 해시 값을 계산한다.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            return (x * 397) ^ y;
        }
    }

    /// <summary>
    /// 로그에서 읽기 쉬운 좌표 문자열로 변환한다.
    /// </summary>
    public override string ToString()
    {
        return $"({x}, {y})";
    }

    /// <summary>
    /// 두 그리드 좌표를 더해 이동 후 좌표를 만든다.
    /// </summary>
    public static GridPosition operator +(GridPosition left, GridPosition right)
    {
        return new GridPosition(left.x + right.x, left.y + right.y);
    }

    /// <summary>
    /// 두 그리드 좌표를 빼서 좌표 차이를 만든다.
    /// </summary>
    public static GridPosition operator -(GridPosition left, GridPosition right)
    {
        return new GridPosition(left.x - right.x, left.y - right.y);
    }

    /// <summary>
    /// 두 그리드 좌표가 같은 칸인지 비교한다.
    /// </summary>
    public static bool operator ==(GridPosition left, GridPosition right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// 두 그리드 좌표가 다른 칸인지 비교한다.
    /// </summary>
    public static bool operator !=(GridPosition left, GridPosition right)
    {
        return !left.Equals(right);
    }
}
