using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 씬의 전술 유닛을 진영과 무관하게 등록하고 조회하는 씬 단위 등록소다.
/// </summary>
public class TacticalUnitRegistry : MonoBehaviour
{
    public static TacticalUnitRegistry Instance { get; private set; }

    // 현재 활성화되어 등록된 모든 전술 유닛 목록이다.
    private readonly List<ITacticalUnit> units = new();
    // 플레이어가 직접 조작할 수 있는 전술 유닛 목록이다.
    private readonly List<TacticalUnitContext> playerControllableUnits = new();

    public IReadOnlyList<ITacticalUnit> Units => units;
    public IReadOnlyList<TacticalUnitContext> PlayerControllableUnits => playerControllableUnits;

    // 조작 가능한 플레이어 진영 유닛이 등록됐을 때 발생한다.
    public event Action<TacticalUnitContext> PlayerUnitRegistered;
    // 조작 가능한 플레이어 진영 유닛이 등록 해제됐을 때 발생한다.
    public event Action<TacticalUnitContext> PlayerUnitUnregistered;

    /// <summary>
    /// 씬의 단일 전술 유닛 등록소 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(TacticalUnitRegistry)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 전역 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 활성 전술 유닛을 등록한다.
    /// </summary>
    public void Register(ITacticalUnit unit)
    {
        if (unit == null || units.Contains(unit))
        {
            return;
        }

        units.Add(unit);
        if (unit is TacticalUnitContext context &&
            context.Faction == UnitFaction.Player &&
            context.ControlType == UnitControlType.Player)
        {
            playerControllableUnits.Add(context);
            PlayerUnitRegistered?.Invoke(context);
        }
    }

    /// <summary>
    /// 비활성화된 전술 유닛의 등록을 해제한다.
    /// </summary>
    public void Unregister(ITacticalUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        units.Remove(unit);
        if (unit is TacticalUnitContext context && playerControllableUnits.Remove(context))
        {
            PlayerUnitUnregistered?.Invoke(context);
        }
    }

    /// <summary>
    /// 지정한 GridActor에 해당하는 조작 가능한 플레이어 유닛을 찾는다.
    /// </summary>
    public bool TryGetPlayerControllableUnit(GridActor actor, out TacticalUnitContext context)
    {
        for (int i = 0; i < playerControllableUnits.Count; i++)
        {
            TacticalUnitContext candidate = playerControllableUnits[i];
            if (candidate != null && candidate.GridActor == actor)
            {
                context = candidate;
                return true;
            }
        }

        context = null;
        return false;
    }

    /// <summary>
    /// 지정한 진영의 살아 있는 전술 유닛을 결과 목록에 추가한다.
    /// </summary>
    public void GetAliveUnits(UnitFaction faction, List<ITacticalUnit> results)
    {
        results.Clear();
        for (int i = 0; i < units.Count; i++)
        {
            ITacticalUnit unit = units[i];
            if (unit != null && unit.Faction == faction && unit.IsAlive)
            {
                results.Add(unit);
            }
        }
    }
}
