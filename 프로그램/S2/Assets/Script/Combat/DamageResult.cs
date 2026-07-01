/// <summary>
/// 피해 적용 전후의 HP 스냅샷과 적용 결과를 담는 값이다.
/// 논리 HP는 즉시 바뀌지만 연출은 이 값의 전후 HP를 기준으로 재생한다.
/// </summary>
public readonly struct DamageResult
{
    public bool Applied { get; }
    public int Damage { get; }
    public int HitPointBefore { get; }
    public int HitPointAfter { get; }
    public bool WasDeadBefore { get; }
    public bool IsDeadAfter { get; }
    public bool KilledByThisDamage => !WasDeadBefore && IsDeadAfter;

    /// <summary>
    /// 지정한 피해 적용 결과 스냅샷을 만든다.
    /// </summary>
    public DamageResult(
        bool applied,
        int damage,
        int hitPointBefore,
        int hitPointAfter,
        bool wasDeadBefore,
        bool isDeadAfter)
    {
        Applied = applied;
        Damage = damage;
        HitPointBefore = hitPointBefore;
        HitPointAfter = hitPointAfter;
        WasDeadBefore = wasDeadBefore;
        IsDeadAfter = isDeadAfter;
    }
}
