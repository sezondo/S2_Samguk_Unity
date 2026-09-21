using System.Collections.Generic;

/// <summary>판단기를 Unity 실행 상태와 분리하는 읽기 전용 전투 조회 계약이다.</summary>
public interface IEnemyPlanningWorld
{
    GridPosition Start { get; }

    int TargetCount { get; }

    int Health(int target);
    int MaxHealth(int target);
    GridPosition TargetPosition(int target);
    IEnumerable<EnemyPlannedAction> Moves(GridPosition from, int range, int cost);
    bool CanAttack(GridPosition from, int target, EnemyPlannedActionKind kind);
    float HitProbability(GridPosition from, int target, EnemyPlannedActionKind kind);
    int Damage(EnemyPlannedActionKind kind);
    float Cover(GridPosition position, int target);
    float Threat(GridPosition position, int target);
    int DistanceToAttack(GridPosition position, int target);
}
