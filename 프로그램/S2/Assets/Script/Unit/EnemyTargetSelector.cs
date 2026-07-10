using System.Collections.Generic;

/// <summary>
/// 적 위치를 기준으로 가장 가까운 살아 있는 플레이어 진영 유닛을 선택한다.
/// </summary>
public sealed class EnemyTargetSelector
{
    private readonly List<ITacticalUnit> candidateBuffer = new();

    /// <summary>
    /// 맨해튼 거리가 가장 가까운 플레이어 진영 유닛을 반환한다.
    /// 거리가 같으면 등록 순서가 빠른 유닛을 선택한다.
    /// </summary>
    public bool TrySelectNearestPlayerUnit(GridPosition enemyPosition, out TacticalUnitContext target)
    {
        target = null;
        TacticalUnitRegistry registry = TacticalUnitRegistry.Instance;
        if (registry == null)
        {
            return false;
        }

        registry.GetAliveUnits(UnitFaction.Player, candidateBuffer);
        int bestDistance = int.MaxValue;
        for (int i = 0; i < candidateBuffer.Count; i++)
        {
            if (candidateBuffer[i] is not TacticalUnitContext candidate)
            {
                continue;
            }

            int distance = enemyPosition.ManhattanDistanceTo(candidate.GridActor.GridPosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            target = candidate;
        }

        return target != null;
    }
}
