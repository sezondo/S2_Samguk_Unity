using UnityEngine;

/// <summary>
/// 대상의 동서남북 인접 낮은 장애물과 공격 방향의 각도로 엄폐 효과를 계산한다.
/// </summary>
public sealed class CoverCalculator
{
    private static readonly GridDirection[] CardinalDirections =
    {
        GridDirection.Up,
        GridDirection.Down,
        GridDirection.Left,
        GridDirection.Right,
    };

    // 낮은 장애물 존재 여부를 조회할 전투 그리드다.
    private readonly GridManager gridManager;

    /// <summary>
    /// 지정한 전투 그리드를 사용하는 엄폐 계산기를 만든다.
    /// </summary>
    public CoverCalculator(GridManager gridManager)
    {
        this.gridManager = gridManager;
    }

    /// <summary>
    /// 공격자와 대상 위치를 기준으로 가장 강하게 적용되는 인접 낮은 엄폐 결과를 반환한다.
    /// </summary>
    public CoverResult Calculate(
        GridPosition attackerPosition,
        GridPosition targetPosition,
        int lowCoverHitPenalty,
        float fullEffectMaximumAngle,
        float flankMinimumAngle)
    {
        if (gridManager == null || attackerPosition == targetPosition)
        {
            return CoverResult.None();
        }

        Vector2 attackDirection = new(
            attackerPosition.x - targetPosition.x,
            attackerPosition.y - targetPosition.y);
        if (attackDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return CoverResult.None();
        }

        CoverResult strongestResult = CoverResult.None();
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            GridDirection coverDirection = CardinalDirections[i];
            GridPosition coverOffset = GridDirectionUtility.ToForwardOffset(coverDirection);
            GridPosition coverPosition = targetPosition + coverOffset;
            if (!gridManager.IsLowObstacle(coverPosition))
            {
                continue;
            }

            Vector2 coverVector = new(coverOffset.x, coverOffset.y);
            float angle = Vector2.Angle(attackDirection, coverVector);
            if (angle >= flankMinimumAngle)
            {
                continue;
            }

            float effectiveness = angle <= fullEffectMaximumAngle
                ? 1f
                : Mathf.InverseLerp(flankMinimumAngle, fullEffectMaximumAngle, angle);
            int hitChanceModifier = -Mathf.RoundToInt(lowCoverHitPenalty * effectiveness);
            if (hitChanceModifier >= strongestResult.HitChanceModifier)
            {
                continue;
            }

            strongestResult = new CoverResult(
                CoverType.Low,
                coverPosition,
                coverDirection,
                angle,
                effectiveness,
                hitChanceModifier);
        }

        return strongestResult;
    }
}
