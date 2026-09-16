/// <summary>한 논리 시점의 시야 원점과 반경을 보존한다. 거리는 셀 단위이며 월드 반경은 셀 크기를 곱한다.</summary>
public readonly struct PlayerVisionSource
{
    // 시야를 내보내는 유닛 또는 배치된 검의 칸이다.
    public readonly GridPosition Origin;
    // 원형 시야의 반경이다.
    public readonly int Radius;
    // 기존 플레이어 주변 8칸의 근접 시야 예외 적용 여부다.
    public readonly bool GuaranteeAdjacent;

    /// <summary>현재 원점, 반경과 근접 시야 정책을 값으로 복사한다.</summary>
    public PlayerVisionSource(GridPosition origin, int radius, bool guaranteeAdjacent)
    {
        Origin = origin;
        Radius = radius;
        GuaranteeAdjacent = guaranteeAdjacent;
    }
}
