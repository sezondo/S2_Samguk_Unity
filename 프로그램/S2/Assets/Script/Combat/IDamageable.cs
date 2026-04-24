/// <summary>
/// 데미지를 받을 수 있는 대상의 공통 규약.
/// 플레이어, 적, 파괴 가능한 오브젝트가 같은 공격 처리 흐름을 사용할 때 구현한다.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 데미지 적용에 성공하면 true, 무적/사망 등으로 무시되면 false를 반환한다.
    /// </summary>
    bool TakeDamage(int damage);
}
