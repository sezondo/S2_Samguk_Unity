using System;
using UnityEngine;

/// <summary>
/// S2-T의 보드 칸 좌표다.
/// Unity 월드 좌표와 분리해서 턴제 규칙은 이 좌표를 기준으로 계산한다.
/// </summary>
[Serializable]
public struct GridPosition : IEquatable<GridPosition>
{
    public int x;
    public int y;

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

    public Vector2Int ToVector2Int()
    {
        return new Vector2Int(x, y);
    }

    public int ManhattanDistanceTo(GridPosition other)
    {
        return Mathf.Abs(x - other.x) + Mathf.Abs(y - other.y);
    }

    public bool Equals(GridPosition other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is GridPosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (x * 397) ^ y;
        }
    }

    public override string ToString()
    {
        return $"({x}, {y})";
    }

    public static GridPosition operator +(GridPosition left, GridPosition right)
    {
        return new GridPosition(left.x + right.x, left.y + right.y);
    }

    public static GridPosition operator -(GridPosition left, GridPosition right)
    {
        return new GridPosition(left.x - right.x, left.y - right.y);
    }

    public static bool operator ==(GridPosition left, GridPosition right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridPosition left, GridPosition right)
    {
        return !left.Equals(right);
    }
}
