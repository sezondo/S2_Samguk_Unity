using System;

/// <summary>
/// 조작 가능한 전술 유닛이 데이터상 반드시 가져야 하는 행동 능력 조합이다.
/// </summary>
[Flags]
public enum UnitAbilityType
{
    None = 0,
    Move = 1 << 0,
    Gun = 1 << 1,
    Hack = 1 << 2,
    Sword = 1 << 3,
    Melee = 1 << 4,
    HeavyGun = 1 << 5,
}
