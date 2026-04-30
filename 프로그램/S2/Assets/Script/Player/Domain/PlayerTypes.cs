public enum PlayerState
{
    Idle,
    MeleeAttack,
    WeaponAiming,
    WeaponThrowing,
    WeaponReceiving,
    Dodge,
    Dead,
}

public enum PlayerHitJudgment
{
    Vulnerable,
    Invincible,
}

public enum PlayerUnderState
{
    Idle,
    Move,
}

public enum PlayerAttackCount
{
    None,
    One,
    Two,
    Three,
}

/// <summary>
/// 화면 기준 8방향
/// - Up    : 화면 위
/// - Down  : 화면 아래
/// - Left  : 화면 왼쪽
/// - Right : 화면 오른쪽
/// 
/// ★ 아트 규칙
/// - Down = 정면(캐릭터가 카메라를 향함)
/// - Up   = 후면(캐릭터 등 보임)
/// </summary>
public enum PlayerSide8
{
    Up,
    UpRight,
    Right,
    DownRight,
    Down,
    DownLeft,
    Left,
    UpLeft,
}

public enum PlayerAnimDir5
{
    Front,      // Down
    Back,       // Up
    Side,       // Right (좌측은 flip으로 처리)
    SideUp,     // UpRight (좌상은 flip)
    SideDown,   // DownRight (좌하는 flip)
}
