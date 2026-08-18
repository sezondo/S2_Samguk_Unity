/// <summary>
/// 공격 방향에서 대상에게 적용되는 엄폐 종류다.
/// </summary>
public enum CoverType
{
    None,
    Low,
}

/// <summary>
/// 대상 인접 엄폐물의 방향·각도와 최종 명중률 보정 결과를 담는 불변 값이다.
/// </summary>
public readonly struct CoverResult
{
    public CoverType Type { get; }
    public GridPosition CoverPosition { get; }
    public GridDirection CoverDirection { get; }
    public float Angle { get; }
    public float Effectiveness { get; }
    public int HitChanceModifier { get; }
    public bool HasCover => Type != CoverType.None && HitChanceModifier < 0;

    /// <summary>
    /// 지정한 엄폐 후보의 방향·각도와 명중률 보정 결과를 만든다.
    /// </summary>
    public CoverResult(
        CoverType type,
        GridPosition coverPosition,
        GridDirection coverDirection,
        float angle,
        float effectiveness,
        int hitChanceModifier)
    {
        Type = type;
        CoverPosition = coverPosition;
        CoverDirection = coverDirection;
        Angle = angle;
        Effectiveness = effectiveness;
        HitChanceModifier = hitChanceModifier;
    }

    /// <summary>
    /// 공격 방향에서 유효한 엄폐물이 없을 때 사용할 결과를 반환한다.
    /// </summary>
    public static CoverResult None()
    {
        return new CoverResult(CoverType.None, default, GridDirection.Up, 180f, 0f, 0);
    }
}
