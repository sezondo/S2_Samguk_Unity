/// <summary>
/// 데미지를 받을 수 있는 대상의 공통 규약.
/// 플레이어, 적, 파괴 가능한 오브젝트가 같은 공격 처리 흐름을 사용할 때 구현한다.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 데미지 적용 전후 HP 스냅샷과 적용 여부를 반환한다.
    /// </summary>
    DamageResult TakeDamage(int damage);
}
