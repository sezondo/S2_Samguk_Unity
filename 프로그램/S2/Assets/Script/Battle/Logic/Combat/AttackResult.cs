/// <summary>
/// 엄폐 계산과 난수 굴림으로 확정된 공격 명중 결과를 담는 불변 스냅샷이다.
/// </summary>
public readonly struct AttackResult
{
    public bool IsHit { get; }
    public int BaseHitChance { get; }
    public int FinalHitChance { get; }
    public int Roll { get; }
    public CoverResult CoverResult { get; }

    /// <summary>
    /// 지정한 명중률·난수·엄폐 결과로 공격 판정 스냅샷을 만든다.
    /// </summary>
    public AttackResult(
        bool isHit,
        int baseHitChance,
        int finalHitChance,
        int roll,
        CoverResult coverResult)
    {
        IsHit = isHit;
        BaseHitChance = baseHitChance;
        FinalHitChance = finalHitChance;
        Roll = roll;
        CoverResult = coverResult;
    }

    /// <summary>
    /// 엄폐와 난수 판정을 거치지 않는 확정 명중 결과를 반환한다.
    /// </summary>
    public static AttackResult GuaranteedHit()
    {
        return new AttackResult(true, 100, 100, -1, CoverResult.None());
    }
}
