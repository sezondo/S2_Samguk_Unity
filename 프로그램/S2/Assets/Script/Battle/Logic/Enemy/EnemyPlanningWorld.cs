using System.Collections.Generic;
using UnityEngine;

/// <summary>한 번의 판단에 필요한 경로·명중·위험 정보를 캐시하는 실제 전투 조회기다.</summary>
public sealed class EnemyPlanningWorld : IEnemyPlanningWorld
{
    // 판단 중에는 실제 보드와 유닛을 변경하지 않는다.
    private readonly EnemyContext enemy;
    private readonly List<TacticalUnitContext> targets;
    private readonly GridManager grid;
    private readonly Dictionary<GridPosition, List<EnemyPlannedAction>> moves = new();
    private readonly Dictionary<int, Dictionary<GridPosition, int>> attackDistances = new();
    private readonly Dictionary<(GridPosition, int, EnemyPlannedActionKind), bool> attackCache = new();
    private readonly Dictionary<(GridPosition, int), float> threatCache = new();
    private readonly Dictionary<(GridPosition, int), float> coverCache = new();
    private static readonly GridPosition[] Directions =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0)
    };
    public GridPosition Start { get; }
    public int TargetCount => targets.Count;

    /// <summary>현재 보드의 읽기 전용 판단 문맥을 구성한다.</summary>
    public EnemyPlanningWorld(EnemyContext enemy, List<TacticalUnitContext> targets)
    {
        this.enemy = enemy;
        this.targets = targets;
        grid = GridManager.Instance;
        Start = enemy.GridActor.GridPosition;
    }

    /// <summary>대상의 현재 체력을 반환한다.</summary>
    public int Health(int target) => targets[target].Health.CurrentHitPoint;
    /// <summary>대상의 최대 체력을 반환한다.</summary>
    public int MaxHealth(int target) => targets[target].Health.MaxHitPoint;
    /// <summary>발각 후 공유하는 대상의 현재 위치다.</summary>
    public GridPosition TargetPosition(int target) => targets[target].GridActor.GridPosition;
    /// <summary>가상 이동에서 자신의 원래 칸만 점유 예외로 취급한다.</summary>
    private bool CanStand(GridPosition p) => grid.IsInside(p) && !grid.IsBlocked(p) && (!grid.TryGetActorAt(p, out var actor) || actor == enemy.GridActor);
    /// <summary>한 이동 비용으로 갈 수 있는 모든 칸과 최단 경로를 생성한다.</summary>
    public IEnumerable<EnemyPlannedAction> Moves(GridPosition from, int range, int cost)
    {
        if (moves.TryGetValue(from, out var cached))
            return cached;
        var result = new List<EnemyPlannedAction>();
        var queue = new Queue<GridPosition>();
        var distance = new Dictionary<GridPosition, int>
        {
            {
                from,
                0
            }
        };
        var parent = new Dictionary<GridPosition, GridPosition>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (distance[p] >= range)
                continue;
            foreach (var d in Directions)
            {
                var next = new GridPosition(p.x + d.x, p.y + d.y);
                if (distance.ContainsKey(next) || !CanStand(next))
                    continue;
                distance[next] = distance[p] + 1;
                parent[next] = p;
                queue.Enqueue(next);
                var path = new List<GridPosition>();
                for (var cursor = next; cursor != from; cursor = parent[cursor])
                    path.Add(cursor);
                path.Reverse();
                result.Add(new EnemyPlannedAction(EnemyPlannedActionKind.Move, next, -1, cost, path));
            }
        }

        moves[from] = result;
        return result;
    }

    /// <summary>가상 위치의 공격 종류별 거리·장애물 조건을 검사한다.</summary>
    public bool CanAttack(GridPosition from, int target, EnemyPlannedActionKind kind)
    {
        var key = (from, target, kind);
        if (!attackCache.TryGetValue(key, out bool valid))
            attackCache[key] = valid = enemy.AttackAction.CanAttackFrom(from, TargetPosition(target), kind);
        return valid;
    }

    /// <summary>실제 공격과 같은 엄폐·명중 계산의 확률만 조회한다.</summary>
    public float HitProbability(GridPosition from, int target, EnemyPlannedActionKind kind)
    {
        if (kind == EnemyPlannedActionKind.Melee)
            return 1;
        return AttackResolutionCoordinator.Instance.TryPreviewAttack(from, TargetPosition(target), enemy.EnemyData.RangedAttackAccuracy, out var preview) ? preview.FinalHitChance / 100f : 0;
    }

    /// <summary>공격 종류의 피해량을 조회한다.</summary>
    public int Damage(EnemyPlannedActionKind kind) => kind == EnemyPlannedActionKind.Melee ? enemy.EnemyData.Combat.meleeDamage : enemy.EnemyData.RangedAttackDamage;
    /// <summary>대상 방향에 대해 실제 판정에서 유효한 엄폐 강도를 조회한다.</summary>
    public float Cover(GridPosition position, int target)
    {
        var key = (position, target);
        if (!coverCache.TryGetValue(key, out float value))
            coverCache[key] = value = AttackResolutionCoordinator.Instance.TryPreviewAttack(TargetPosition(target), position, enemy.EnemyData.RangedAttackAccuracy, out var p) ? p.CoverResult.Effectiveness : 0;
        return value;
    }

    /// <summary>모든 표적의 현재 위치에서 받을 수 있는 최대 단일 공격의 예상 피해다.</summary>
    public float Threat(GridPosition position, int target)
    {
        var key = (position, target);
        if (threatCache.TryGetValue(key, out float cached))
            return cached;
        var unit = targets[target];
        var data = unit.UnitData;
        var from = TargetPosition(target);
        float value = 0;
        if (unit.HasAbility(UnitAbilityType.Melee) && CombatTargetRules.IsMeleeAdjacent(from, position))
            value = unit.SwordState != null && unit.SwordState.IsRecalled ? data.MeleeDamageWithSword : data.MeleeDamageWithoutSword;
        if (unit.HasAbility(UnitAbilityType.Gun) && CombatTargetRules.CanRangedAttack(from, position, data.GunAttackRange, CanPlayersObserve(position)) && AttackResolutionCoordinator.Instance.TryPreviewAttack(from, position, data.GunAttackAccuracy, out var p))
            value = Mathf.Max(value, data.GunAttackDamage * p.FinalHitChance / 100f);
        threatCache[key] = value;
        return value;
    }

    /// <summary>가상 위치에 대한 플레이어 합산 시야를 본체와 투척 검 기준으로 계산한다.</summary>
    private bool CanPlayersObserve(GridPosition position)
    {
        foreach (var unit in targets)
        {
            if (PlayerVisionGeometry.CanObserve(grid, unit.GridActor.GridPosition, position, unit.UnitData.VisionRange, true))
                return true;
            if (unit.HasAbility(UnitAbilityType.Sword) && unit.SwordState != null && !unit.SwordState.IsRecalled && PlayerVisionGeometry.CanObserve(grid, unit.SwordState.CurrentPosition, position, unit.UnitData.SwordVisionRange, false))
                return true;
        }

        return false;
    }

    /// <summary>벽과 점유를 우회해 공격 가능한 칸까지 남은 실제 이동 거리를 구한다.</summary>
    public int DistanceToAttack(GridPosition position, int target)
    {
        if (!attackDistances.TryGetValue(target, out var distances))
        {
            distances = new Dictionary<GridPosition, int>();
            var queue = new Queue<GridPosition>();
            var settings = enemy.EnemyData.Combat;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var p = new GridPosition(x, y);
                    if (!CanStand(p))
                        continue;
                    if (settings.allowMelee && CanAttack(p, target, EnemyPlannedActionKind.Melee) || settings.allowRanged && CanAttack(p, target, EnemyPlannedActionKind.Ranged))
                    {
                        distances[p] = 0;
                        queue.Enqueue(p);
                    }
                }

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Directions)
                {
                    var next = new GridPosition(p.x + d.x, p.y + d.y);
                    if (distances.ContainsKey(next) || !CanStand(next))
                        continue;
                    distances[next] = distances[p] + 1;
                    queue.Enqueue(next);
                }
            }

            attackDistances[target] = distances;
        }

        return distances.TryGetValue(position, out int distance) ? distance : grid.Width * grid.Height;
    }
}
