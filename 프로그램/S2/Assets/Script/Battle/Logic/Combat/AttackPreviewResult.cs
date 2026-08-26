/// <summary>
/// 난수를 굴리지 않고 계산한 원거리 공격의 명중률과 엄폐 결과다.
/// </summary>
public readonly struct AttackPreviewResult
{
    public int BaseHitChance { get; }
    public int FinalHitChance { get; }
    public CoverResult CoverResult { get; }

    /// <summary>
    /// 지정한 기본·최종 명중률과 엄폐 결과로 공격 미리보기 값을 만든다.
    /// </summary>
    public AttackPreviewResult(int baseHitChance, int finalHitChance, CoverResult coverResult)
    {
        BaseHitChance = baseHitChance;
        FinalHitChance = finalHitChance;
        CoverResult = coverResult;
    }
}
