public enum EnemyState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    Hit,
    Dead,
    Hacked,
}

public enum EnemyDetectionState
{
    PlayerUndetected,
    PlayerDetected,
}

public enum EnemyDirection4
{
    Down,
    Up,
    Right,
    Left,
}

public enum EnemyAnimState
{
    IdleDown = 0,
    IdleUp = 1,
    IdleSide = 2,
    MoveDown = 3,
    MoveUp = 4,
    MoveSide = 5,
    AttackDown = 6,
    AttackUp = 7,
    AttackSide = 8,
    DeadDown = 9,
    DeadUp = 10,
    DeadSide = 11,
}
