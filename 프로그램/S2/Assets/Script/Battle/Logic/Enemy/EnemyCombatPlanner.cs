using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>공통 AP 계획기다. 특수 적은 후보 생성·평가를 상속해 확장하며 자원 검증은 공유한다.</summary>
[Serializable]
public class EnemyCombatPlanner
{
    /// <summary>AP별 상위 상태를 탐색하고 중도 종료까지 포함한 최선 계획을 반환한다.</summary>
    public EnemyPlanState Plan(IEnemyPlanningWorld world, EnemyCombatSettings settings, int ap, int attacksLeft, int moveRange)
    {
        var initial = new EnemyPlanState
        {
            Position = world.Start,
            HealthProbabilities = new float[world.TargetCount][]
        };
        for (int i = 0; i < world.TargetCount; i++)
        {
            initial.HealthProbabilities[i] = new float[world.Health(i) + 1];
            initial.HealthProbabilities[i][world.Health(i)] = 1;
        }

        initial.Score = Score(world, settings, initial, attacksLeft);
        var best = initial;
        var layers = new Dictionary<string, EnemyPlanState>[ap + 1];
        for (int i = 0; i < layers.Length; i++)
            layers[i] = new Dictionary<string, EnemyPlanState>();
        layers[0].Add(Key(initial), initial);
        for (int spent = 0; spent <= ap; spent++)
        {
            var states = new List<EnemyPlanState>(layers[spent].Values);
            states.Sort(Compare);
            int count = Math.Min(states.Count, settings.beamWidth);
            for (int n = 0; n < count; n++)
            {
                var state = states[n];
                if (Compare(state, best) < 0)
                    best = state;
                foreach (var action in BuildCandidates(world, settings, state, ap - spent, attacksLeft, moveRange))
                {
                    if (action.Cost <= 0 || spent + action.Cost > ap || action.Kind != EnemyPlannedActionKind.Move && state.Attacks >= attacksLeft)
                        continue;
                    var next = Advance(world, settings, state, action);
                    next.Score = Score(world, settings, next, attacksLeft);
                    string key = Key(next);
                    var bucket = layers[next.SpentAP];
                    if (!bucket.TryGetValue(key, out var previous) || Compare(next, previous) < 0)
                        bucket[key] = next;
                }
            }

            layers[spent].Clear();
        }

        return best;
    }

    /// <summary>사용 가능한 공격과 엄폐에 제한되지 않는 이동 후보를 구성한다.</summary>
    protected virtual IEnumerable<EnemyPlannedAction> BuildCandidates(IEnemyPlanningWorld world, EnemyCombatSettings s, EnemyPlanState state, int ap, int attacksLeft, int moveRange)
    {
        if (ap >= s.moveCost)
            foreach (var move in world.Moves(state.Position, moveRange, s.moveCost))
                yield return move;
        if (state.Attacks >= attacksLeft)
            yield break;
        for (int i = 0; i < world.TargetCount; i++)
        {
            if (state.HealthProbabilities[i][0] >= 0.999999f)
                continue;
            if (s.allowMelee && ap >= s.meleeCost && world.CanAttack(state.Position, i, EnemyPlannedActionKind.Melee))
                yield return new EnemyPlannedAction(EnemyPlannedActionKind.Melee, world.TargetPosition(i), i, s.meleeCost);
            if (s.allowRanged && ap >= s.rangedCost && world.CanAttack(state.Position, i, EnemyPlannedActionKind.Ranged))
                yield return new EnemyPlannedAction(EnemyPlannedActionKind.Ranged, world.TargetPosition(i), i, s.rangedCost);
        }
    }

    /// <summary>미래 공격의 HP 분포를 계산한다. 실제 공격 난수는 읽거나 소비하지 않는다.</summary>
    private static EnemyPlanState Advance(IEnemyPlanningWorld world, EnemyCombatSettings s, EnemyPlanState state, EnemyPlannedAction action)
    {
        var next = state.Copy();
        next.SpentAP += action.Cost;
        next.Actions.Add(action);
        if (action.Kind == EnemyPlannedActionKind.Move)
        {
            next.Position = action.Destination;
            next.MovedSteps += action.Path.Count;
            return next;
        }

        next.Attacks++;
        int t = action.TargetIndex, damage = world.Damage(action.Kind);
        float p = Mathf.Clamp01(world.HitProbability(state.Position, t, action.Kind));
        var before = state.HealthProbabilities[t];
        var after = new float[before.Length];
        for (int hp = 0; hp < before.Length; hp++)
        {
            after[hp] += before[hp] * (1 - p);
            after[Math.Max(0, hp - damage)] += before[hp] * p;
        }

        next.HealthProbabilities[t] = after;
        float alive = 1 - before[0];
        // 가까운 표적 선호는 판단 시작 위치 기준이다. 보너스만 얻으려고 불필요하게 접근하지 않는다.
        next.TargetUtility += alive * (s.attackWeight + s.targetDistanceWeight / (1 + world.Start.ManhattanDistanceTo(world.TargetPosition(t))) + s.woundedTargetWeight * (1 - ExpectedHP(before) / Math.Max(1, world.MaxHealth(t))));
        return next;
    }

    /// <summary>예상 실피해·처치 확률과 최종 위치의 모든 생존 위협을 공통 평가한다.</summary>
    protected virtual EnemyPlanScore Score(IEnemyPlanningWorld world, EnemyCombatSettings s, EnemyPlanState state, int attacksLeft)
    {
        float damage = 0, kills = 0, position = 0, approach = 0;
        for (int i = 0; i < world.TargetCount; i++)
        {
            float dead = state.HealthProbabilities[i][0], alive = 1 - dead;
            damage += (world.Health(i) - ExpectedHP(state.HealthProbabilities[i])) * s.damageWeight;
            kills += dead * s.killWeight;
            position += alive * (world.Cover(state.Position, i) * s.coverWeight - world.Threat(state.Position, i) * s.dangerWeight - Mathf.Abs(state.Position.ManhattanDistanceTo(world.TargetPosition(i)) - s.preferredDistance) * s.distanceWeight);
            // 공격 횟수를 소진한 뒤 접근 보너스 때문에 다시 적에게 달려가지 않게 한다.
            if (state.Attacks < attacksLeft)
                approach = Mathf.Max(approach, alive * (world.DistanceToAttack(world.Start, i) - world.DistanceToAttack(state.Position, i)) * s.approachWeight);
        }

        return new EnemyPlanScore(damage, kills, state.TargetUtility, position + approach, state.SpentAP * s.actionPointPenalty + state.MovedSteps * s.movementPenalty);
    }

    /// <summary>분포의 기대 HP를 구한다.</summary>
    private static float ExpectedHP(float[] distribution)
    {
        float hp = 0;
        for (int i = 1; i < distribution.Length; i++)
            hp += i * distribution[i];
        return hp;
    }

    /// <summary>점수 동률은 적은 AP·적은 이동·일정한 행동 순서로 해소한다.</summary>
    private static int Compare(EnemyPlanState a, EnemyPlanState b)
    {
        int order = b.Score.Total.CompareTo(a.Score.Total);
        if (order != 0)
            return order;
        order = a.SpentAP.CompareTo(b.SpentAP);
        if (order != 0)
            return order;
        order = a.MovedSteps.CompareTo(b.MovedSteps);
        if (order != 0)
            return order;
        return string.CompareOrdinal(ActionKey(a), ActionKey(b));
    }

    /// <summary>안정적인 행동 순서 비교 문자열을 만든다.</summary>
    private static string ActionKey(EnemyPlanState state)
    {
        var s = new StringBuilder();
        foreach (var a in state.Actions)
            s.Append((int)a.Kind).Append(':').Append(a.TargetIndex).Append(':').Append(a.Destination.x).Append(',').Append(a.Destination.y).Append(';');
        return s.ToString();
    }

    /// <summary>동일 자원·위치·HP 분포의 중복 탐색을 합친다.</summary>
    private static string Key(EnemyPlanState state)
    {
        var s = new StringBuilder();
        s.Append(state.Position.x).Append(',').Append(state.Position.y).Append('/').Append(state.Attacks);
        foreach (var d in state.HealthProbabilities)
            foreach (float p in d)
                s.Append('/').Append(p.ToString("R", CultureInfo.InvariantCulture));
        return s.ToString();
    }
}
