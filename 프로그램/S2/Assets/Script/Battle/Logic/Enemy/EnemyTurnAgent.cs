using System;
using UnityEngine;

/// <summary>순찰·의심은 기존 흐름으로, 전투는 AP 계획의 첫 행동만 실행한다.</summary>
public class EnemyTurnAgent : MonoBehaviour
{
    // 필수 구성 요소를 제공하는 적 참조 주머니다.
    [SerializeField]
    private EnemyContext enemyContext;
    // 특수 행동은 공통 판단기의 후보·평가 함수를 상속해 확장할 수 있다.
    [SerializeReference]
    private EnemyCombatPlanner planner = new();
    // 은신 특화 플레이어를 제외할 때 확장할 공통 표적 관문이다.
    [SerializeReference]
    private EnemyTargetProvider targetProvider = new();
    // 선택한 계획과 세부 점수의 출력 여부다.
    [SerializeField]
    private bool logTurnAction = true;
    [SerializeField]
    private bool logCandidateScores;
    public int AttacksUsed { get; private set; }
    public string LastDecision { get; private set; }
    public EnemyPlanState LastPlan { get; private set; }

    /// <summary>기본 참조와 전투 튜닝을 검사한다.</summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
            enabled = false;
    }

    /// <summary>AP 충전 시 공격 횟수를 한 번만 초기화한다.</summary>
    public void BeginTurn()
    {
        AttacksUsed = 0;
        LastPlan = null;
        LastDecision = "턴 시작";
    }

    /// <summary>전투에서는 한 행동만 예약한다. 결과 확정과 재판단은 턴 조정자가 반복한다.</summary>
    public bool TryExecuteTurn(ActionResolutionContext context)
    {
        if (!enabled || !HasValidReference() || !HasValidData() || !enemyContext.IsAlive)
            return false;
        if (context == null || GridManager.Instance == null)
        {
            Debug.LogError($"{name}: 적 판단에 행동 문맥과 그리드가 필요합니다.", this);
            return false;
        }

        if (enemyContext.AlertState.IsUnaware)
            return enemyContext.RoutineController.TryExecuteRoutineTurn(context);
        if (enemyContext.AlertState.IsSuspicious)
            return enemyContext.InvestigationAgent.TryExecuteSuspiciousTurn(context);
        if (AttackResolutionCoordinator.Instance == null || TacticalUnitRegistry.Instance == null || EnemyRegistry.Instance == null)
        {
            Debug.LogError($"{name}: 전투 판단에 공격 판정기와 유닛 등록소가 필요합니다.", this);
            return false;
        }

        var targets = targetProvider.Collect();
        if (targets.Count == 0)
        {
            LastDecision = "공격 가능한 표적 없음";
            return false;
        }

        var settings = enemyContext.EnemyData.Combat;
        int ap = enemyContext.ActionPoint.Current;
        LastPlan = planner.Plan(new EnemyPlanningWorld(enemyContext, targets), settings, ap, Math.Max(0, settings.maximumAttacks - AttacksUsed), enemyContext.EnemyData.TurnMoveRange);
        LastDecision = $"AP {ap}, 공격 {AttacksUsed}/{settings.maximumAttacks}: {string.Join(" → ", LastPlan.Actions)}; {LastPlan.Score}";
        if (logTurnAction || logCandidateScores)
            Debug.Log($"{name}: {LastDecision}", this);
        if (LastPlan.Actions.Count == 0)
            return false;
        var action = LastPlan.Actions[0];
        if (!enemyContext.ActionPoint.CanSpend(action.Cost))
            return false;
        if (action.Kind == EnemyPlannedActionKind.Move)
        {
            var previous = enemyContext.GridActor.GridPosition;
            var grid = GridManager.Instance;
            foreach (var p in action.Path)
            {
                if (previous.ManhattanDistanceTo(p) != 1 || !grid.IsInside(p) || grid.IsBlocked(p) || grid.TryGetActorAt(p, out var occupied) && occupied != enemyContext.GridActor)
                    return false;
                previous = p;
            }

            if (!enemyContext.ActionPoint.TrySpend(action.Cost))
                return false;
            int moved = EnemyMovementUtility.MoveAlongPath(enemyContext, action.Path, context, "적 AP 계획 이동");
            if (moved != action.Path.Count)
            {
                Debug.LogError($"{name}: 검증한 이동 경로 실행이 중단되어 전투 판단을 정지합니다.", this);
                enabled = false;
            }

            return moved > 0;
        }

        var target = targets[action.TargetIndex];
        if (!targetProvider.IsEligible(target) || AttacksUsed >= settings.maximumAttacks)
            return false;
        if (!enemyContext.AttackAction.TryExecuteAttack(target.GridActor, context, action.Kind))
            return false;
        enemyContext.ActionPoint.TrySpend(action.Cost);
        AttacksUsed++;
        return true;
    }

    /// <summary>필수 참조를 검사한다.</summary>
    public bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.enabled || !enemyContext.HasValidReference())
        {
            return false;
        }

        if (enemyContext.AlertState != null && enemyContext.AlertState.IsAlerted && (enemyContext.AttackAction == null || !enemyContext.AttackAction.enabled))
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 {nameof(EnemyContext)}에 연결된 활성 {nameof(EnemyAttackAction)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyContext.ActionPoint == null || !enemyContext.ActionPoint.enabled)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 {nameof(EnemyContext)}에 연결된 활성 {nameof(EnemyActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyContext.AlertState != null && enemyContext.AlertState.IsUnaware && enemyContext.RoutineController == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 평상 행동에는 {nameof(EnemyRoutineController)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyContext.AlertState != null && enemyContext.AlertState.IsSuspicious && enemyContext.InvestigationAgent == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 의심 행동에는 {nameof(EnemyInvestigationAgent)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>비용·공격 한도·점수·탐색 상한을 사용하는 쪽에서 검사한다.</summary>
    public bool HasValidData()
    {
        var data = enemyContext.EnemyData;
        var s = data.Combat;
        if (planner == null || targetProvider == null || s == null || data.TurnActionPoint < 0 || data.TurnActionPoint > 12 || data.TurnMoveRange < 0 || (!s.allowMelee && !s.allowRanged) || s.moveCost <= 0 || s.meleeCost <= 0 || s.rangedCost <= 0 || s.maximumAttacks < 0 || s.meleeDamage <= 0 || s.beamWidth < 1 || s.beamWidth > 1024)
        {
            Debug.LogError($"{name}: 전투 판단기·표적 관문·양수 비용·공격 구성·AP(0~12)·탐색 폭(1~1024)을 확인하세요.", this);
            return false;
        }

        float[] weights =
        {
            s.damageWeight,
            s.killWeight,
            s.attackWeight,
            s.targetDistanceWeight,
            s.woundedTargetWeight,
            s.approachWeight,
            s.coverWeight,
            s.dangerWeight,
            s.preferredDistance,
            s.distanceWeight,
            s.actionPointPenalty,
            s.movementPenalty
        };
        foreach (float value in weights)
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            {
                Debug.LogError($"{name}: 전투 가중치는 유한한 0 이상 값이어야 합니다.", this);
                return false;
            }

        return true;
    }
}
