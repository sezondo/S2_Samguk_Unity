using UnityEngine;

public static class PlayerFacingUtil
{
    /// <summary>
    /// 방향 벡터(Vector2)를 8방향(PlayerSide8)으로 양자화해준다.
    /// 
    /// 중요:
    /// - dir이 거의 0이면(입력이 없으면) 방향을 정할 수가 없다.
    /// - 이 경우 false를 반환하고, 호출자는 "이전 lastSide 유지"를 해야 한다.
    /// </summary>
    public static bool TryQuantize8(Vector2 dir, out PlayerSide8 side)
    {
        if (dir.sqrMagnitude < 0.0001f) // 백터의 길이를 제곱한 값
        {
            side = default;
            return false;
        }

        dir.Normalize();

        // atan2는 -180~180도 범위, 0~360도로 바꿔서 처리
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // 각도 구해서 상수로 만들기 degree
        if (angle < 0) angle += 360f;

        // 45도 단위로 8구역
        // 0:Right, 1:UpRight, 2:Up, 3:UpLeft, 4:Left, 5:DownLeft, 6:Down, 7:DownRight
        int sector = Mathf.RoundToInt(angle / 45f) % 8;

        side = sector switch
        {
            0 => PlayerSide8.Right,
            1 => PlayerSide8.UpRight,
            2 => PlayerSide8.Up,
            3 => PlayerSide8.UpLeft,
            4 => PlayerSide8.Left,
            5 => PlayerSide8.DownLeft,
            6 => PlayerSide8.Down,
            _ => PlayerSide8.DownRight,
        };

        return true;
    }

    /// <summary>
    /// TryQuantize8의 편의 버전.
    /// - dir이 0이면 defaultSide를 그대로 반환한다.
    /// (즉, lastSide 같은 걸 기본값으로 넣으면 안전)
    /// </summary>
    public static PlayerSide8 Quantize8OrDefault(Vector2 dir, PlayerSide8 defaultSide)
    {
        return TryQuantize8(dir, out var side) ? side : defaultSide;
    }

    public static PlayerSide8 ChooseSide(bool useAim, PlayerSide8 lastMoveSide, PlayerSide8 aimSide)
        => useAim ? aimSide : lastMoveSide;

    public static Vector2 Side8ToDir(PlayerSide8 s)
    {
        return s switch
        {
            PlayerSide8.Up => Vector2.up,
            PlayerSide8.UpRight => new Vector2(1, 1).normalized,
            PlayerSide8.Right => Vector2.right,
            PlayerSide8.DownRight => new Vector2(1, -1).normalized,
            PlayerSide8.Down => Vector2.down,
            PlayerSide8.DownLeft => new Vector2(-1, -1).normalized,
            PlayerSide8.Left => Vector2.left,
            _ => new Vector2(-1, 1).normalized, // UpLeft
        };
    }
}
    
