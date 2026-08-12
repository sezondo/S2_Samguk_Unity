using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 하나의 의심 즉시 반응, 3턴 조사와 평상 루틴 복귀를 실행한다.
/// </summary>
public class EnemyInvestigationAgent : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Reference")]
    // 의심 행동을 수행할 적 Context다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("Log")]
    // true면 조사 이동, 관찰과 복귀 결과를 출력한다.
    [SerializeField] private bool logInvestigation = true;

    private readonly Dictionary<GridPosition, int> reachableDistances = new();
    private readonly List<GridPosition> pathBuffer = new();

    /// <summary>
    /// 의심 논리 이벤트를 받기 위해 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 의심 논리 이벤트 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 시작 시 필수 참조와 조사 데이터를 검사한다.
    /// </summary>
    private void Start()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 이 적에게 전달된 의심 이벤트만 처리할 수 있다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is EnemySuspicionTriggeredLogicEvent suspicion && suspicion.Enemy == enemyContext;
    }

    /// <summary>
    /// 의심 발생 즉시 AP와 별개의 반응 이동을 실행하고 의심 지점을 바라본다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not EnemySuspicionTriggeredLogicEvent suspicion ||
            suspicion.Enemy != enemyContext ||
            !enemyContext.AlertState.IsSuspicious)
        {
            return;
        }

        TryMoveToInvestigationPosition(suspicion.SuspicionInfo, context);
        if (enemyContext.AlertState.IsSuspicious)
        {
            EnemyMovementUtility.FacePosition(enemyContext, suspicion.SuspicionInfo.Position, context);
            enemyContext.AlertState.SetSuspiciousPhase(SuspiciousBehaviorPhase.Observing);
        }
    }

    /// <summary>
    /// 의심 상태의 적 턴을 진행하고 3턴 종료 시 원래 루틴으로 복귀시킨다.
    /// </summary>
    public bool TryExecuteSuspiciousTurn(ActionResolutionContext context)
    {
        if (!enabled || context == null || !enemyContext.IsAlive || !enemyContext.AlertState.IsSuspicious)
        {
            return false;
        }

        if (enemyContext.AlertState.SuspiciousPhase == SuspiciousBehaviorPhase.ReturningToRoutine)
        {
            return TryReturnToRoutine(context);
        }

        GridDirection nextDirection = GetNextSearchDirection(enemyContext.GridSight.FacingDirection);
        EnemyMovementUtility.FaceDirection(enemyContext, nextDirection, context);
        if (!enemyContext.AlertState.IsSuspicious)
        {
            return true;
        }

        bool hasRemainingTurn = enemyContext.AlertState.ConsumeSuspicionTurn();
        if (hasRemainingTurn)
        {
            enemyContext.AlertState.SetSuspiciousPhase(SuspiciousBehaviorPhase.Searching);
            return true;
        }

        enemyContext.AlertState.SetSuspiciousPhase(SuspiciousBehaviorPhase.ReturningToRoutine);
        TryReturnToRoutine(context);
        return true;
    }

    /// <summary>
    /// 의심 지점을 볼 수 있고 적정 거리를 유지하는 엄폐 후보 중 최고 위치로 이동한다.
    /// </summary>
    private void TryMoveToInvestigationPosition(EnemySuspicionInfo info, ActionResolutionContext context)
    {
        EnemyData data = enemyContext.EnemyData;
        GridPosition start = enemyContext.GridActor.GridPosition;
        GridPathfinder.FindReachablePositionDistances(GridManager.Instance, start, data.SuspicionReactionMoveRange, reachableDistances);

        bool found = false;
        GridPosition bestPosition = start;
        int bestScore = int.MinValue;
        foreach (KeyValuePair<GridPosition, int> pair in reachableDistances)
        {
            GridPosition candidate = pair.Key;
            int distanceToSuspicion = candidate.ManhattanDistanceTo(info.Position);
            if (distanceToSuspicion < data.InvestigationMinimumDistance ||
                distanceToSuspicion > data.InvestigationMaximumDistance ||
                !GridLineOfSight.HasLineOfSight(GridManager.Instance, candidate, info.Position))
            {
                continue;
            }

            int score = ScoreCandidate(candidate, pair.Value, info);
            if (!found || score > bestScore || score == bestScore && ComparePosition(candidate, bestPosition) < 0)
            {
                found = true;
                bestPosition = candidate;
                bestScore = score;
            }
        }

        if (!found || bestPosition == start ||
            !GridPathfinder.TryFindPath(GridManager.Instance, start, bestPosition, data.SuspicionReactionMoveRange, pathBuffer))
        {
            if (logInvestigation)
            {
                Debug.Log($"{nameof(EnemyInvestigationAgent)}: {enemyContext.name} 적은 현재 위치에서 {info.Position} 칸을 조사합니다.", this);
            }

            return;
        }

        EnemyMovementUtility.MoveAlongPath(enemyContext, pathBuffer, context, "적 의심 조사 반응 이동 연출");
    }

    /// <summary>
    /// 조사 후보의 엄폐, 이동 거리와 다른 의심 적과의 분산 정도를 평가한다.
    /// </summary>
    private int ScoreCandidate(GridPosition candidate, int moveDistance, EnemySuspicionInfo info)
    {
        int score = -moveDistance * 3;
        if (EnemyTacticalPositionScorer.IsCoverCandidate(GridManager.Instance, candidate, info.Position))
        {
            score += 40;
        }

        if (info.Role == EnemySuspicionRole.Support)
        {
            score += candidate.ManhattanDistanceTo(info.Position) * 4;
        }

        IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext other = enemies[i];
            if (other == null || other == enemyContext || !other.IsAlive || !other.AlertState.IsSuspicious)
            {
                continue;
            }

            int separation = candidate.ManhattanDistanceTo(other.GridActor.GridPosition);
            if (separation <= 1)
            {
                score -= 30;
            }
        }

        return score;
    }

    /// <summary>
    /// Guard는 원래 자리로 이동하고 Patrol 그룹은 편대를 재구성한 뒤 평상 상태로 전환한다.
    /// </summary>
    private bool TryReturnToRoutine(ActionResolutionContext context)
    {
        EnemyRoutineController routine = enemyContext.RoutineController;
        if (routine.RoutineType == EnemyRoutineType.Patrol)
        {
            if (routine.PatrolGroup != null)
            {
                return routine.PatrolGroup.TryRegroupAfterInvestigation(enemyContext, context);
            }

            enemyContext.AlertState.RequestUnaware();
            return true;
        }

        bool completed = routine.TryReturnGuard(context);
        if (completed)
        {
            enemyContext.AlertState.RequestUnaware();
        }

        return completed;
    }

    /// <summary>
    /// 조사 시야를 시계 방향으로 90도 돌린다.
    /// </summary>
    private static GridDirection GetNextSearchDirection(GridDirection current)
    {
        return current switch
        {
            GridDirection.Up => GridDirection.Right,
            GridDirection.Right => GridDirection.Down,
            GridDirection.Down => GridDirection.Left,
            _ => GridDirection.Up,
        };
    }

    /// <summary>
    /// 동일 점수 후보를 재현 가능한 좌표 순서로 정렬한다.
    /// </summary>
    private static int ComparePosition(GridPosition left, GridPosition right)
    {
        int xComparison = left.x.CompareTo(right.x);
        return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
    }

    /// <summary>
    /// 의심 행동에 필요한 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyContext == null || enemyContext.RoutineController == null || GridManager.Instance == null || EnemyRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyInvestigationAgent)} on {name}에는 EnemyContext, RoutineController, GridManager와 EnemyRegistry가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 의심 이동 범위와 조사 거리 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        EnemyData data = enemyContext.EnemyData;
        if (data.SuspicionReactionMoveRange < 0 || data.InvestigationMinimumDistance < 0 ||
            data.InvestigationMaximumDistance < data.InvestigationMinimumDistance)
        {
            Debug.LogError($"{nameof(EnemyInvestigationAgent)} on {name}의 의심 조사 거리 데이터가 올바르지 않습니다.", this);
            return false;
        }

        return true;
    }
}
