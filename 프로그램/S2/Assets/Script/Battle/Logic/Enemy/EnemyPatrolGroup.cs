using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 순찰 그룹원과 리더 기준 편대 오프셋을 연결한다.
/// </summary>
[Serializable]
public struct EnemyPatrolGroupMember
{
    // 그룹에 속한 적 Context다.
    [SerializeField] private EnemyContext enemy;
    // 리더 위치를 기준으로 이 적이 유지할 상대 그리드 위치다.
    [SerializeField] private GridPosition formationOffset;

    public EnemyContext Enemy => enemy;
    public GridPosition FormationOffset => formationOffset;
}

/// <summary>
/// 하나의 PatrolPoint 그래프와 편대를 공유하는 적 그룹의 평상 순찰을 실행한다.
/// </summary>
public class EnemyPatrolGroup : MonoBehaviour
{
    private const int PatrolActionPointCost = 1;

    [Header("Route")]
    // 씬 시작 시 그룹이 서 있는 순찰 지점이다.
    [SerializeField] private PatrolPoint startPoint;
    // 그룹의 목적지와 경로 기준이 되는 리더 적이다.
    [SerializeField] private EnemyContext leader;
    // 리더와 함께 움직이는 그룹원과 편대 오프셋이다. 리더도 오프셋 0으로 포함한다.
    [SerializeField] private List<EnemyPatrolGroupMember> members = new();

    [Header("Log")]
    // true면 목적지 선택, 막힘과 도착 결과를 출력한다.
    [SerializeField] private bool logPatrol = true;
    // 같은 목표로 연속 이동하지 못했을 때 구성 경고를 출력할 턴 수다.
    [SerializeField] private int blockedWarningTurns = 3;

    [Header("Regroup")]
    // 조사 종료 후 현재 편대 기준 칸 주변에서 새 편대 Anchor를 찾을 최대 맨해튼 거리다.
    [SerializeField] private int regroupSearchRadius = 8;
    // 편대 복귀가 연속으로 막혔을 때 기존 Anchor를 폐기하고 다시 계산할 턴 수다.
    [SerializeField] private int regroupAnchorRetryTurns = 3;

    // 현재 그룹이 도착해 있는 순찰 지점이다.
    private PatrolPoint currentPoint;
    // 현재 지점에 오기 직전에 방문한 지점이다.
    private PatrolPoint previousPoint;
    // 여러 턴에 걸쳐 이동 중인 고정 목적지다.
    private PatrolPoint targetPoint;
    // 현재 지점에서 더 대기해야 하는 적 턴 수다.
    private int waitTurnsRemaining;
    // 같은 적 턴에 그룹이 여러 번 실행되는 것을 막는 마지막 실행 번호다.
    private int lastExecutedEnemyTurnIndex = -1;
    // 현재 목적지로 연속 이동하지 못한 턴 수다.
    private int blockedTurnCount;
    // 사망한 리더를 대신해 현재 편대와 경로 기준이 되는 생존 리더다.
    private EnemyContext activeLeader;
    // 현재 리더의 원래 직렬화 편대 오프셋이다. 계승 후 생존 편대 오프셋을 다시 맞추는 기준이다.
    private GridPosition activeLeaderSourceOffset;
    // 조사 종료 후 재구성할 편대 기준 칸을 찾았는지 나타낸다.
    private bool hasRegroupAnchor;
    // 조사 종료 후 생존 구성원이 모일 편대의 기준 칸이다.
    private GridPosition regroupAnchor;
    // 현재 편대 복귀 Anchor로 연속 이동하지 못한 적 턴 수다.
    private int regroupBlockedTurnCount;

    // 리더의 전체 경로 계산 버퍼다.
    private readonly List<GridPosition> leaderPathBuffer = new();
    // 이번 턴에 실제 적용할 공통 이동 오프셋 버퍼다.
    private readonly List<GridPosition> sharedStepOffsets = new();
    // 그룹원 조회와 중복 검증에 사용하는 집합이다.
    private readonly HashSet<EnemyContext> memberSet = new();
    // 리더 경로 계산에서 편대 구성원 점유만 제외하기 위한 집합이다.
    private readonly HashSet<GridActor> memberActorSet = new();
    // 한 단계 이동 순서를 정할 때 사용하는 그룹원 버퍼다.
    private readonly List<EnemyContext> orderedMembers = new();
    // 연결 후보를 재사용하는 버퍼다.
    private readonly List<PatrolPoint> candidatePoints = new();
    // 조사 후 편대 재구성 경로를 계산할 때 사용하는 버퍼다.
    private readonly List<GridPosition> regroupPathBuffer = new();
    // 이번 재구성 행동에서 실제 이동할 경로 구간을 담는 버퍼다.
    private readonly List<GridPosition> regroupStepBuffer = new();

    public PatrolPoint CurrentPoint => currentPoint;
    public PatrolPoint TargetPoint => targetPoint;
    public IReadOnlyList<EnemyPatrolGroupMember> Members => members;

    /// <summary>
    /// 그룹 경로와 구성원 설정을 확인하고 시작 지점 상태를 준비한다.
    /// </summary>
    private void Start()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        currentPoint = startPoint;
        waitTurnsRemaining = startPoint.WaitTurns;
        activeLeader = leader;
        activeLeaderSourceOffset = GetConfiguredFormationOffset(activeLeader);
        EnsureActiveLeader();
    }

    /// <summary>
    /// 지정한 적이 이 순찰 그룹에 포함되어 있는지 확인한다.
    /// </summary>
    public bool Contains(EnemyContext enemy)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].Enemy == enemy)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 첫 호출 그룹원을 기준으로 이번 적 턴의 그룹 순찰을 한 번만 실행한다.
    /// </summary>
    public bool TryExecuteTurn(EnemyContext requester, ActionResolutionContext context)
    {
        if (!enabled || requester == null || context == null || !Contains(requester) || TurnManager.Instance == null)
        {
            return false;
        }

        int enemyTurnIndex = TurnManager.Instance.EnemyTurnIndex;
        if (lastExecutedEnemyTurnIndex == enemyTurnIndex)
        {
            return false;
        }

        if (!EnsureActiveLeader() || !CanPatrol())
        {
            return false;
        }

        lastExecutedEnemyTurnIndex = enemyTurnIndex;

        if (targetPoint == null && waitTurnsRemaining > 0)
        {
            waitTurnsRemaining--;
            FaceGroup(currentPoint.LookDirection, context);
            return false;
        }

        if (targetPoint == null && !TrySelectNextPoint())
        {
            return false;
        }

        if (!TryBuildSharedSteps())
        {
            HandleBlockedTurn();
            return false;
        }

        int movedSteps = ExecuteSharedSteps(context);
        if (movedSteps <= 0)
        {
            HandleBlockedTurn();
            return false;
        }

        blockedTurnCount = 0;
        SpendPatrolActionPoint();
        TryCompleteArrival(context);
        return true;
    }

    /// <summary>
    /// 모든 생존 그룹원의 조사가 끝났을 때 가까운 유효 칸에 편대를 다시 구성하고 평상 상태로 복귀시킨다.
    /// </summary>
    public bool TryRegroupAfterInvestigation(EnemyContext requester, ActionResolutionContext context)
    {
        if (!enabled || requester == null || context == null || !Contains(requester) || TurnManager.Instance == null)
        {
            return false;
        }

        int enemyTurnIndex = TurnManager.Instance.EnemyTurnIndex;
        if (lastExecutedEnemyTurnIndex == enemyTurnIndex || !EnsureActiveLeader() || !CanRegroupAfterInvestigation())
        {
            return false;
        }

        lastExecutedEnemyTurnIndex = enemyTurnIndex;
        if (hasRegroupAnchor && !IsRegroupAnchorValid())
        {
            InvalidateRegroupAnchor("편대 목표 칸이 장애물 또는 외부 유닛 점유로 유효하지 않게 됐습니다.");
        }

        if (!hasRegroupAnchor && !TrySelectRegroupAnchor())
        {
            HandleBlockedRegroupTurn();
            return false;
        }

        if (IsRegroupComplete())
        {
            CompleteRegroup(context);
            return true;
        }

        if (!TryMoveOneMemberForRegroup(context))
        {
            HandleBlockedRegroupTurn();
            return false;
        }

        regroupBlockedTurnCount = 0;
        if (IsRegroupComplete())
        {
            CompleteRegroup(context);
        }

        return true;
    }

    /// <summary>
    /// 모든 생존 구성원이 발각되지 않았고 조사 복귀 단계까지 진행했는지 확인한다.
    /// </summary>
    private bool CanRegroupAfterInvestigation()
    {
        bool hasReturningMember = false;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (enemy.AlertState == null || enemy.AlertState.IsAlerted)
            {
                return false;
            }

            if (!enemy.AlertState.IsSuspicious)
            {
                continue;
            }

            if (enemy.AlertState.SuspiciousPhase != SuspiciousBehaviorPhase.ReturningToRoutine)
            {
                return false;
            }

            hasReturningMember = true;
        }

        return hasReturningMember;
    }

    /// <summary>
    /// 현재 편대 기준 칸 주변에서 생존 편대를 놓을 수 있고 전체 경로가 가장 짧은 Anchor를 선택한다.
    /// </summary>
    private bool TrySelectRegroupAnchor()
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            return false;
        }

        memberActorSet.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive)
            {
                memberActorSet.Add(enemy.GridActor);
            }
        }

        bool found = false;
        int bestScore = int.MaxValue;
        GridPosition bestAnchor = GridPosition.Zero;
        GridPosition searchCenter = GetRegroupSearchCenter();
        int minX = Mathf.Max(0, searchCenter.x - regroupSearchRadius);
        int maxX = Mathf.Min(gridManager.Width - 1, searchCenter.x + regroupSearchRadius);
        int minY = Mathf.Max(0, searchCenter.y - regroupSearchRadius);
        int maxY = Mathf.Min(gridManager.Height - 1, searchCenter.y + regroupSearchRadius);
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                GridPosition candidateAnchor = new(x, y);
                if (searchCenter.ManhattanDistanceTo(candidateAnchor) > regroupSearchRadius)
                {
                    continue;
                }

                if (!TryScoreRegroupAnchor(candidateAnchor, out int score))
                {
                    continue;
                }

                if (!found || score < bestScore || score == bestScore && ComparePosition(candidateAnchor, bestAnchor) < 0)
                {
                    found = true;
                    bestScore = score;
                    bestAnchor = candidateAnchor;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        regroupAnchor = bestAnchor;
        hasRegroupAnchor = true;
        regroupBlockedTurnCount = 0;
        if (logPatrol)
        {
            Debug.Log($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 {searchCenter} 주변 {regroupSearchRadius}칸 안에서 편대 재구성 기준 칸 {regroupAnchor}을 선택했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 지정한 기준 칸에 생존 편대를 놓을 수 있는지 확인하고 현재 위치부터의 총 거리를 계산한다.
    /// </summary>
    private bool TryScoreRegroupAnchor(GridPosition candidateAnchor, out int score)
    {
        score = 0;
        GridManager gridManager = GridManager.Instance;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            GridPosition target = candidateAnchor + GetActiveFormationOffset(enemy);
            if (!gridManager.IsInside(target) || gridManager.IsBlocked(target))
            {
                return false;
            }

            if (gridManager.TryGetActorAt(target, out GridActor occupied) && occupied != enemy.GridActor)
            {
                return false;
            }

            if (enemy.GridActor.GridPosition == target)
            {
                continue;
            }

            if (!GridPathfinder.TryFindPath(
                    gridManager,
                    enemy.GridActor.GridPosition,
                    target,
                    regroupSearchRadius,
                    regroupPathBuffer,
                    memberActorSet))
            {
                return false;
            }

            score += regroupPathBuffer.Count;
        }

        return true;
    }

    /// <summary>
    /// 현재 활성 리더가 오프셋 0인 편대의 기준 칸을 Anchor 탐색 중심으로 반환한다.
    /// </summary>
    private GridPosition GetRegroupSearchCenter()
    {
        return activeLeader.GridActor.GridPosition - GetActiveFormationOffset(activeLeader);
    }

    /// <summary>
    /// 현재 Anchor의 모든 편대 목표 칸이 고정 장애물과 외부 유닛 점유를 피하는지 확인한다.
    /// </summary>
    private bool IsRegroupAnchorValid()
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            GridPosition target = regroupAnchor + GetActiveFormationOffset(enemy);
            if (!gridManager.IsInside(target) || gridManager.IsBlocked(target))
            {
                return false;
            }

            if (gridManager.TryGetActorAt(target, out GridActor occupied) &&
                occupied != enemy.GridActor &&
                !IsGroupActor(occupied))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 아직 목표 편대 칸에 도착하지 않은 구성원 하나를 이번 조사 복귀 이동 범위만큼 이동시킨다.
    /// </summary>
    private bool TryMoveOneMemberForRegroup(ActionResolutionContext context)
    {
        GridManager gridManager = GridManager.Instance;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            GridPosition target = regroupAnchor + GetActiveFormationOffset(enemy);
            if (enemy.GridActor.GridPosition == target)
            {
                continue;
            }

            // 다른 그룹원이 먼저 비워야 하는 목표 칸은 다음 구성원 또는 다음 적 턴에 다시 시도한다.
            if (gridManager.TryGetActorAt(target, out GridActor occupied) && occupied != enemy.GridActor)
            {
                continue;
            }

            if (!GridPathfinder.TryFindPath(
                    gridManager,
                    enemy.GridActor.GridPosition,
                    target,
                    regroupSearchRadius,
                    regroupPathBuffer))
            {
                continue;
            }

            regroupStepBuffer.Clear();
            int moveRange = Mathf.Min(enemy.EnemyData.PatrolMoveRange, regroupPathBuffer.Count);
            for (int pathIndex = 0; pathIndex < moveRange; pathIndex++)
            {
                regroupStepBuffer.Add(regroupPathBuffer[pathIndex]);
            }

            if (EnemyMovementUtility.MoveAlongPath(enemy, regroupStepBuffer, context, "적 조사 후 편대 재구성 이동 연출") <= 0)
            {
                continue;
            }

            if (logPatrol)
            {
                Debug.Log($"{nameof(EnemyPatrolGroup)}: {enemy.name} 적이 조사 후 편대 칸 {target}으로 복귀 중입니다.", this);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 모든 생존 구성원이 선택된 편대 칸에 도착했는지 확인한다.
    /// </summary>
    private bool IsRegroupComplete()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive &&
                enemy.GridActor.GridPosition != regroupAnchor + GetActiveFormationOffset(enemy))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 재구성된 그룹원을 평상 상태로 되돌리고 다음 적 턴부터 기존 순찰 경로를 이어가게 한다.
    /// </summary>
    private void CompleteRegroup(ActionResolutionContext context)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive && enemy.AlertState != null && enemy.AlertState.IsSuspicious)
            {
                enemy.AlertState.RequestUnaware();
            }
        }

        hasRegroupAnchor = false;
        regroupBlockedTurnCount = 0;
        if (currentPoint != null)
        {
            FaceGroup(currentPoint.LookDirection, context);
        }

        if (logPatrol)
        {
            Debug.Log($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 조사 후 편대 재구성을 완료했습니다.", this);
        }
    }

    /// <summary>
    /// 편대 재구성 막힘 횟수를 기록하고 임계치에 도달하면 기존 Anchor를 폐기한다.
    /// </summary>
    private void HandleBlockedRegroupTurn()
    {
        regroupBlockedTurnCount++;
        if (regroupBlockedTurnCount < regroupAnchorRetryTurns)
        {
            return;
        }

        if (hasRegroupAnchor)
        {
            InvalidateRegroupAnchor($"{regroupBlockedTurnCount}턴 연속 편대를 재구성하지 못했습니다.");
            return;
        }

        regroupBlockedTurnCount = 0;
        Debug.LogWarning($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 현재 편대 기준 칸 주변 {regroupSearchRadius}칸 안에서 유효한 재구성 Anchor를 찾지 못했습니다. 이후 편대 복귀 실행에서 다시 시도합니다.", this);
    }

    /// <summary>
    /// 현재 편대 복귀 Anchor와 막힘 횟수를 초기화하고 이후 재계산 원인을 기록한다.
    /// </summary>
    private void InvalidateRegroupAnchor(string reason)
    {
        hasRegroupAnchor = false;
        regroupBlockedTurnCount = 0;
        Debug.LogWarning($"{nameof(EnemyPatrolGroup)}: {name} 그룹의 편대 재구성 Anchor를 폐기합니다. {reason} 이후 편대 복귀 실행에서 다시 계산합니다.", this);
    }

    /// <summary>
    /// 현재 그룹의 모든 생존 구성원이 평상 상태로 순찰 가능한지 확인한다.
    /// </summary>
    private bool CanPatrol()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            if (enemy.AlertState == null || !enemy.AlertState.IsUnaware)
            {
                return false;
            }

            if (enemy.ActionPoint == null || !enemy.ActionPoint.CanSpend(PatrolActionPointCost))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 현재 지점의 연결 중 직전 지점을 우선 제외해 다음 목적지를 선택한다.
    /// </summary>
    private bool TrySelectNextPoint()
    {
        if (currentPoint == null)
        {
            return false;
        }

        candidatePoints.Clear();
        IReadOnlyList<PatrolPoint> connections = currentPoint.ConnectedPoints;
        for (int i = 0; i < connections.Count; i++)
        {
            PatrolPoint candidate = connections[i];
            if (candidate != null && candidate != previousPoint && IsStaticGroupTargetValid(candidate))
            {
                candidatePoints.Add(candidate);
            }
        }

        if (candidatePoints.Count == 0 && previousPoint != null && IsStaticGroupTargetValid(previousPoint))
        {
            candidatePoints.Add(previousPoint);
        }

        if (candidatePoints.Count == 0)
        {
            if (logPatrol)
            {
                Debug.LogWarning($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 {currentPoint.name}에서 이동할 유효 순찰 지점을 찾지 못했습니다.", this);
            }

            return false;
        }

        targetPoint = candidatePoints[UnityEngine.Random.Range(0, candidatePoints.Count)];
        if (logPatrol)
        {
            Debug.Log($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 다음 순찰 지점으로 {targetPoint.name}을 선택했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 목적지에서 구성원별 편대 오프셋을 적용한 칸이 고정 장애물 없이 유효한지 확인한다.
    /// </summary>
    private bool IsStaticGroupTargetValid(PatrolPoint point)
    {
        if (point == null || GridManager.Instance == null || !point.TryGetGridPosition(out GridPosition anchor))
        {
            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            GridPosition target = anchor + GetActiveFormationOffset(enemy);
            if (!GridManager.Instance.IsInside(target) || GridManager.Instance.IsBlocked(target))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 리더 경로의 이번 턴 최대 이동 구간이 모든 그룹원에게 공통 적용 가능한지 계산한다.
    /// </summary>
    private bool TryBuildSharedSteps()
    {
        sharedStepOffsets.Clear();
        leaderPathBuffer.Clear();
        if (!EnsureActiveLeader() || targetPoint == null || !targetPoint.TryGetGridPosition(out GridPosition anchor))
        {
            return false;
        }

        GridPosition leaderTarget = anchor + GetActiveFormationOffset(activeLeader);
        int searchDistance = GridManager.Instance.Width * GridManager.Instance.Height;
        memberActorSet.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].Enemy != null && members[i].Enemy.IsAlive)
            {
                memberActorSet.Add(members[i].Enemy.GridActor);
            }
        }

        if (!GridPathfinder.TryFindPath(
                GridManager.Instance,
                activeLeader.GridActor.GridPosition,
                leaderTarget,
                searchDistance,
                leaderPathBuffer,
                memberActorSet))
        {
            return false;
        }

        int maxSteps = Mathf.Min(activeLeader.EnemyData.PatrolMoveRange, leaderPathBuffer.Count);
        GridPosition previous = activeLeader.GridActor.GridPosition;
        GridPosition cumulativeOffset = GridPosition.Zero;
        for (int i = 0; i < maxSteps; i++)
        {
            GridPosition offset = leaderPathBuffer[i] - previous;
            cumulativeOffset += offset;
            if (!CanApplyProjectedOffset(cumulativeOffset))
            {
                break;
            }

            sharedStepOffsets.Add(offset);
            previous = leaderPathBuffer[i];
        }

        return sharedStepOffsets.Count > 0;
    }

    /// <summary>
    /// 모든 생존 그룹원이 같은 한 칸 오프셋으로 이동할 수 있는지 점유를 포함해 검사한다.
    /// </summary>
    private bool CanApplyProjectedOffset(GridPosition cumulativeOffset)
    {
        GridManager gridManager = GridManager.Instance;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            GridPosition next = enemy.GridActor.GridPosition + cumulativeOffset;
            if (!gridManager.IsInside(next) || gridManager.IsBlocked(next))
            {
                return false;
            }

            if (gridManager.TryGetActorAt(next, out GridActor occupied) && occupied != enemy.GridActor && !IsGroupActor(occupied))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 공통 이동 단계를 앞쪽 그룹원부터 적용해 그룹 내부 점유 충돌을 피한다.
    /// </summary>
    private int ExecuteSharedSteps(ActionResolutionContext context)
    {
        int completedSteps = 0;
        for (int stepIndex = 0; stepIndex < sharedStepOffsets.Count; stepIndex++)
        {
            GridPosition offset = sharedStepOffsets[stepIndex];
            BuildMovementOrder(offset);
            bool stepCompleted = true;
            for (int i = 0; i < orderedMembers.Count; i++)
            {
                EnemyContext enemy = orderedMembers[i];
                GridPosition next = enemy.GridActor.GridPosition + offset;
                GridPosition[] singleStepPath = { next };
                if (EnemyMovementUtility.MoveAlongPath(enemy, singleStepPath, context, "적 그룹 순찰 이동 연출") != 1)
                {
                    stepCompleted = false;
                    break;
                }
            }

            if (!stepCompleted)
            {
                break;
            }

            completedSteps++;
        }

        return completedSteps;
    }

    /// <summary>
    /// 이동 방향에서 가장 앞에 있는 그룹원부터 움직이도록 순서를 만든다.
    /// </summary>
    private void BuildMovementOrder(GridPosition offset)
    {
        orderedMembers.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive)
            {
                orderedMembers.Add(enemy);
            }
        }

        orderedMembers.Sort((left, right) =>
        {
            int leftProjection = left.GridActor.GridPosition.x * offset.x + left.GridActor.GridPosition.y * offset.y;
            int rightProjection = right.GridActor.GridPosition.x * offset.x + right.GridActor.GridPosition.y * offset.y;
            return rightProjection.CompareTo(leftProjection);
        });
    }

    /// <summary>
    /// 리더가 목적지에 도착하면 방문 기록, 대기 턴과 공통 시야 방향을 적용한다.
    /// </summary>
    private void TryCompleteArrival(ActionResolutionContext context)
    {
        if (targetPoint == null || !targetPoint.TryGetGridPosition(out GridPosition anchor))
        {
            return;
        }

        if (!EnsureActiveLeader() || activeLeader.GridActor.GridPosition != anchor + GetActiveFormationOffset(activeLeader))
        {
            return;
        }

        previousPoint = currentPoint;
        currentPoint = targetPoint;
        targetPoint = null;
        waitTurnsRemaining = currentPoint.WaitTurns;
        FaceGroup(currentPoint.LookDirection, context);
    }

    /// <summary>
    /// 모든 생존 그룹원에게 같은 감시 방향을 적용한다.
    /// </summary>
    private void FaceGroup(GridDirection direction, ActionResolutionContext context)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive)
            {
                EnemyMovementUtility.FaceDirection(enemy, direction, context);
            }
        }
    }

    /// <summary>
    /// 이동한 모든 생존 그룹원에게 평상 순찰 1AP 비용을 적용한다.
    /// </summary>
    private void SpendPatrolActionPoint()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive && !enemy.ActionPoint.TrySpend(PatrolActionPointCost))
            {
                Debug.LogError($"{nameof(EnemyPatrolGroup)}: {enemy.name} 적의 순찰 AP 소비에 실패했습니다.", enemy);
            }
        }
    }

    /// <summary>
    /// 연속 막힘 횟수를 기록하고 구성 오류 가능성을 경고한다.
    /// </summary>
    private void HandleBlockedTurn()
    {
        blockedTurnCount++;
        if (blockedWarningTurns > 0 && blockedTurnCount >= blockedWarningTurns)
        {
            Debug.LogWarning($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 {blockedTurnCount}턴 연속 순찰하지 못했습니다. 경로의 고정 장애물과 점유 상태를 확인하세요.", this);
        }
    }

    /// <summary>
    /// 사망한 현재 리더 대신 구성원 목록에서 가장 먼저 등록된 생존 적에게 리더를 계승한다.
    /// </summary>
    private bool EnsureActiveLeader()
    {
        if (activeLeader != null && activeLeader.IsAlive)
        {
            return true;
        }

        EnemyContext previousLeader = activeLeader;
        activeLeader = null;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext candidate = members[i].Enemy;
            if (candidate != null && candidate.IsAlive)
            {
                activeLeader = candidate;
                activeLeaderSourceOffset = GetConfiguredFormationOffset(candidate);
                hasRegroupAnchor = false;
                regroupBlockedTurnCount = 0;
                break;
            }
        }

        if (activeLeader == null)
        {
            return false;
        }

        if (previousLeader != activeLeader && logPatrol)
        {
            string previousName = previousLeader != null ? previousLeader.name : "없음";
            Debug.Log($"{nameof(EnemyPatrolGroup)}: {name} 그룹 리더가 {previousName}에서 {activeLeader.name}(으)로 계승됐습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 지정한 적의 직렬화된 원래 편대 오프셋을 반환한다.
    /// </summary>
    private GridPosition GetConfiguredFormationOffset(EnemyContext enemy)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].Enemy == enemy)
            {
                return members[i].FormationOffset;
            }
        }

        return GridPosition.Zero;
    }

    /// <summary>
    /// 현재 생존 리더가 오프셋 0이 되도록 다시 맞춘 지정 구성원의 런타임 편대 오프셋을 반환한다.
    /// </summary>
    private GridPosition GetActiveFormationOffset(EnemyContext enemy)
    {
        return GetConfiguredFormationOffset(enemy) - activeLeaderSourceOffset;
    }

    /// <summary>
    /// 두 칸을 재현 가능한 x, y 좌표 순서로 비교한다.
    /// </summary>
    private static int ComparePosition(GridPosition left, GridPosition right)
    {
        int xComparison = left.x.CompareTo(right.x);
        return xComparison != 0 ? xComparison : left.y.CompareTo(right.y);
    }

    /// <summary>
    /// 지정한 GridActor가 현재 그룹의 생존 구성원인지 확인한다.
    /// </summary>
    private bool IsGroupActor(GridActor actor)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.GridActor == actor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 그룹 순찰에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (startPoint == null || leader == null)
        {
            Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}에는 시작 순찰 지점과 리더 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 그룹원, 편대와 시작 지점 배치가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (!startPoint.HasValidData() || members.Count == 0 || blockedWarningTurns < 0)
        {
            Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 순찰 데이터가 올바르지 않습니다.", this);
            return false;
        }

        if (regroupSearchRadius <= 0)
        {
            Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 편대 재구성 탐색 반경은 0보다 커야 합니다.", this);
            return false;
        }

        if (regroupAnchorRetryTurns <= 0)
        {
            Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 편대 Anchor 재계산 대기 턴은 0보다 커야 합니다.", this);
            return false;
        }

        memberSet.Clear();
        bool containsLeader = false;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !memberSet.Add(enemy))
            {
                Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 그룹원에 빈 값 또는 중복 적이 있습니다.", this);
                return false;
            }

            containsLeader |= enemy == leader;
            if (enemy.RoutineController == null || enemy.RoutineController.PatrolGroup != this)
            {
                Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 {enemy.name} 적과 RoutineController 그룹 참조가 서로 일치하지 않습니다.", this);
                return false;
            }

            if (enemy.EnemyData == null || enemy.EnemyData.PatrolMoveRange <= 0)
            {
                Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 {enemy.name} 적 순찰 이동 거리는 0보다 커야 합니다.", this);
                return false;
            }
        }

        if (!containsLeader)
        {
            Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}의 그룹원 목록에 리더가 포함되어야 합니다.", this);
            return false;
        }

        return true;
    }
}
