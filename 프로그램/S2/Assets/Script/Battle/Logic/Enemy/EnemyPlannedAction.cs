using System.Collections.Generic;

/// <summary>공통 계획기가 생성하는 기본 행동 종류다.</summary>
public enum EnemyPlannedActionKind
{
    Move,
    Melee,
    Ranged
}

/// <summary>실행 전 재검증할 행동과 비용, 이동 경로의 불변 스냅샷이다.</summary>
public sealed class EnemyPlannedAction
{
    public EnemyPlannedActionKind Kind { get; }
    public GridPosition Destination { get; }
    public int TargetIndex { get; }
    public int Cost { get; }
    public IReadOnlyList<GridPosition> Path { get; }

    /// <summary>계획 행동을 구성한다.</summary>
    public EnemyPlannedAction(EnemyPlannedActionKind kind, GridPosition destination, int targetIndex, int cost, IReadOnlyList<GridPosition> path = null)
    {
        Kind = kind;
        Destination = destination;
        TargetIndex = targetIndex;
        Cost = cost;
        Path = path;
    }

    /// <summary>디버그용 계획 표현을 반환한다.</summary>
    public override string ToString() => Kind == EnemyPlannedActionKind.Move ? $"이동 {Destination}({Cost}AP)" : $"{(Kind == EnemyPlannedActionKind.Melee ? "근접" : "원거리")} 대상#{TargetIndex}({Cost}AP)";
}

/// <summary>계획의 최종 점수와 항목별 근거다.</summary>
public readonly struct EnemyPlanScore
{
    public float Damage { get; }
    public float Kills { get; }
    public float Target { get; }
    public float Position { get; }
    public float Cost { get; }
    public float Total => Damage + Kills + Target + Position - Cost;

    /// <summary>평가 항목을 저장한다.</summary>
    public EnemyPlanScore(float damage, float kills, float target, float position, float cost)
    {
        Damage = damage;
        Kills = kills;
        Target = target;
        Position = position;
        Cost = cost;
    }

    /// <summary>선택 이유를 한국어로 표시한다.</summary>
    public override string ToString() => $"총점 {Total:F2} [피해 {Damage:F2}, 처치 {Kills:F2}, 표적 {Target:F2}, 위치 {Position:F2}, 비용 -{Cost:F2}]";
}

/// <summary>가상 턴 상태다. 실제 유닛과 난수를 변경하지 않는다.</summary>
public sealed class EnemyPlanState
{
    // 현재 가상 위치와 소비한 AP·공격 횟수·이동 칸 수다.
    public GridPosition Position;
    public int SpentAP, Attacks, MovedSteps;
    // 대상별 남은 HP 확률 분포다. 여러 발 공격에서도 처치 보너스를 중복 지급하지 않는다.
    public float[][] HealthProbabilities;
    // 대상별 공격 선택 보너스의 누적값과 실행할 순서다.
    public float TargetUtility;
    public List<EnemyPlannedAction> Actions = new();
    public EnemyPlanScore Score;
    /// <summary>변경 가능한 배열과 행동 목록을 독립 복제한다.</summary>
    public EnemyPlanState Copy()
    {
        var copy = (EnemyPlanState)MemberwiseClone();
        copy.Actions = new List<EnemyPlannedAction>(Actions);
        copy.HealthProbabilities = new float[HealthProbabilities.Length][];
        for (int i = 0; i < HealthProbabilities.Length; i++)
            copy.HealthProbabilities[i] = (float[])HealthProbabilities[i].Clone();
        return copy;
    }
}
