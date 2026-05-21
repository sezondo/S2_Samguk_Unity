using UnityEngine;

/// <summary>
/// 공격 방향과 공격 중 여부를 외부 표현/판정 컴포넌트에 제공하는 공용 규약.
/// 플레이어, 적, NPC 공격 컴포넌트가 같은 방식으로 공격 정보를 노출할 때 구현한다.
/// </summary>
public interface IAttackInfoProvider
{
    Vector2 AttackDirection { get; }
    bool IsAttacking { get; }
}
