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

public enum PlayerAnimDir3
{
    Front,      // Down
    Back,       // Up
    Side,       // Right (좌측은 flip으로 처리)
}

public enum PlayerAnimState
{
    IdleFront = 0,
    IdleSide = 1,
    IdleBack = 2,
    RunFront = 3,
    RunSide = 4,
    RunBack = 5,
    AttackFront = 6,
    AttackSide = 7,
    AttackBack = 8,
    DodgeFront = 9,
    DodgeSide = 10,
    DodgeBack = 11,
    DeadFront = 12,
    DeadSide = 13,
    DeadBack = 14,
    WeaponThrowReadyFront = 15,
    WeaponThrowReadySide = 16,
    WeaponThrowReadyBack = 17,
    WeaponThrowFront = 18,
    WeaponThrowSide = 19,
    WeaponThrowBack = 20,
}
