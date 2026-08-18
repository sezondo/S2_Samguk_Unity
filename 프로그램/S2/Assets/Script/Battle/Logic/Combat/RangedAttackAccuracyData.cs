using System;
using UnityEngine;

/// <summary>
/// 유닛별 원거리 공격의 기본 명중률과 엄폐 대응 수치를 묶어 보관한다.
/// </summary>
[Serializable]
public sealed class RangedAttackAccuracyData
{
    // 거리·무기 추가 보정이 없는 원거리 공격의 기본 명중률이다.
    [SerializeField, Range(0, 100)] private int baseHitChance = 80;
    // 일반 명중 판정에서 허용할 최저 명중률이다.
    [SerializeField, Range(0, 100)] private int minimumHitChance = 5;
    // 일반 명중 판정에서 허용할 최고 명중률이다.
    [SerializeField, Range(0, 100)] private int maximumHitChance = 95;
    // 낮은 엄폐가 정면에서 제공하는 최대 명중률 감소량이다.
    [SerializeField, Range(0, 100)] private int lowCoverHitPenalty = 20;

    public int BaseHitChance => baseHitChance;
    public int MinimumHitChance => minimumHitChance;
    public int MaximumHitChance => maximumHitChance;
    public int LowCoverHitPenalty => lowCoverHitPenalty;
}
