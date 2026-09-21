using System;
using System.Collections.Generic;

/// <summary>애드 후 위치를 공유하는 표적 목록이다. 은신 예외는 이 관문을 확장한다.</summary>
[Serializable]
public class EnemyTargetProvider
{
    /// <summary>공격·추적·위험 평가 모두 같은 대상 목록을 받는다.</summary>
    public List<TacticalUnitContext> Collect()
    {
        var result = new List<TacticalUnitContext>();
        var units = new List<ITacticalUnit>();
        if (TacticalUnitRegistry.Instance == null)
            return result;
        TacticalUnitRegistry.Instance.GetAliveUnits(UnitFaction.Player, units);
        foreach (var unit in units)
            if (unit is TacticalUnitContext candidate && IsEligible(candidate))
                result.Add(candidate);
        return result;
    }

    /// <summary>향후 은신 상태에서 전투 타겟 제외가 필요할 때 재정의한다.</summary>
    public virtual bool IsEligible(TacticalUnitContext unit) => unit != null && unit.isActiveAndEnabled && unit.IsAlive;
}
