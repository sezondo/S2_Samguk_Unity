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
    // 현재 위치에서 동시 이동을 시작할 수 없어 다음 Anchor 탐색에서 제외할 기준 칸들이다.
    private readonly HashSet<GridPosition> rejectedRegroupAnchors = new();
    // 현재 재구성 단계에서 일부 구성원이 충돌 회피를 위해 기다려야 하는지 나타낸다.
    private bool regroupStepHasWaitingMember;
    // 마지막 재구성 경로 또는 동시 이동 실패 원인이다.
    private string lastRegroupFailureReason;

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
    // 이번 재구성 행동에서 구성원별로 실제 이동할 경로를 보관한다.
    private readonly Dictionary<EnemyContext, List<GridPosition>> regroupPathByEnemy = new();
    // 현재 재구성 단계에서 이동할 구성원과 목표 칸을 보관한다.
    private readonly Dictionary<EnemyContext, GridPosition> regroupNextPositionByEnemy = new();
    // 현재 재구성 단계에서 두 구성원이 같은 목표 칸을 선택하는지 검사한다.
    private readonly HashSet<GridPosition> regroupTargetPositions = new();
    // 재구성 점유 갱신 순서를 계산할 때 아직 처리 중인 구성원을 추적한다.
    private readonly HashSet<EnemyContext> regroupMoveVisiting = new();
    // 재구성 점유 갱신 순서를 계산할 때 처리가 끝난 구성원을 추적한다.
    private readonly HashSet<EnemyContext> regroupMoveVisited = new();
    // 다른 구성원이 먼저 비워야 하는 칸을 고려한 재구성 점유 갱신 순서다.
    private readonly List<EnemyContext> regroupMovementOrder = new();
    // 순찰 또는 재구성 그룹 이동 한 번의 구성원별 시작 위치를 보관한다.
    private readonly Dictionary<EnemyContext, GridPosition> groupMoveStartByEnemy = new();
    // 순찰 또는 재구성 그룹 이동에서 실제로 확정된 구성원별 경로를 보관한다.
    private readonly Dictionary<EnemyContext, List<GridPosition>> groupMovePathByEnemy = new();
    // 그룹 이동 연출 이벤트를 만들 때 사용하는 구성원 스냅샷 버퍼다.
    private readonly List<GroupMovePresentationMemberSnapshot> groupMoveSnapshotBuffer = new();
    // 편대 동시 방향 전환 연출을 만들 때 사용하는 구성원 스냅샷 버퍼다.
    private readonly List<GroupFacingTurnPresentationMemberSnapshot> groupFacingTurnSnapshotBuffer = new();
    // 편대 이동 단계가 끝난 뒤 감지할 살아 있는 플레이어를 조회하는 버퍼다.
    private readonly List<ITacticalUnit> playerDetectionBuffer = new();

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

        int movedSteps = ExecuteSharedSteps(context, out bool alertTriggered);
        if (movedSteps <= 0)
        {
            HandleBlockedTurn();
            return false;
        }

        blockedTurnCount = 0;
        SpendPatrolActionPoint();
        if (!alertTriggered)
        {
            TryCompleteArrival(context);
        }

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

        if (!TryMoveMembersForRegroup(context, out bool alertTriggered))
        {
            HandleBlockedRegroupTurn();
            return false;
        }

        regroupBlockedTurnCount = 0;
        if (!alertTriggered && IsRegroupComplete())
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

                if (rejectedRegroupAnchors.Contains(candidateAnchor))
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
            // 현재 위치에서 가능한 후보를 모두 제외했다면 다음 복귀 턴에 전체 후보를 다시 평가한다.
            rejectedRegroupAnchors.Clear();
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
        memberActorSet.Clear();
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

            if (enemy.GridActor.GridPosition != target)
            {
                memberActorSet.Add(enemy.GridActor);
            }
        }

        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            GridPosition target = candidateAnchor + GetActiveFormationOffset(enemy);
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
    /// 아직 목표 편대 칸에 도착하지 않은 구성원들의 경로를 계산해 단계별로 동시에 이동시킨다.
    /// </summary>
    private bool TryMoveMembersForRegroup(ActionResolutionContext context, out bool alertTriggered)
    {
        alertTriggered = false;
        lastRegroupFailureReason = null;
        if (!TryBuildRegroupPaths(out int maxStepCount))
        {
            return false;
        }

        int completedSteps = ExecuteRegroupSteps(context, maxStepCount, out alertTriggered);
        if (completedSteps <= 0)
        {
            if (hasRegroupAnchor)
            {
                rejectedRegroupAnchors.Add(regroupAnchor);
                string reason = string.IsNullOrWhiteSpace(lastRegroupFailureReason)
                    ? "첫 동시 이동 단계를 안전하게 구성하지 못했습니다."
                    : lastRegroupFailureReason;
                InvalidateRegroupAnchor($"{reason} 이 Anchor는 현재 위치의 다음 후보 탐색에서 제외합니다.");
            }

            return false;
        }

        // 한 칸이라도 이동해 배치가 달라졌다면 이전 위치에서 실패한 Anchor 제외 기록은 더 이상 유효하지 않다.
        rejectedRegroupAnchors.Clear();
        if (logPatrol)
        {
            Debug.Log($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 조사 후 편대 칸으로 {completedSteps}단계 동시 복귀했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 생존 구성원마다 현재 칸에서 편대 목표 칸까지 이번 턴에 이동할 경로를 계산한다.
    /// </summary>
    private bool TryBuildRegroupPaths(out int maxStepCount)
    {
        maxStepCount = 0;
        regroupPathByEnemy.Clear();
        memberActorSet.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive &&
                enemy.GridActor.GridPosition != regroupAnchor + GetActiveFormationOffset(enemy))
            {
                memberActorSet.Add(enemy.GridActor);
            }
        }

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            lastRegroupFailureReason = "편대 재구성 경로를 계산할 GridManager가 없습니다.";
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
                lastRegroupFailureReason = $"{enemy.name} 적이 현재 칸에서 목표 편대 칸 {target}까지 경로를 찾지 못했습니다.";
                return false;
            }

            int moveRange = Mathf.Min(enemy.EnemyData.PatrolMoveRange, regroupPathBuffer.Count);
            if (moveRange <= 0)
            {
                lastRegroupFailureReason = $"{enemy.name} 적의 이번 편대 복귀 이동 가능 거리가 0입니다.";
                return false;
            }

            List<GridPosition> movementPath = new(moveRange);
            for (int pathIndex = 0; pathIndex < moveRange; pathIndex++)
            {
                movementPath.Add(regroupPathBuffer[pathIndex]);
            }

            regroupPathByEnemy.Add(enemy, movementPath);
            maxStepCount = Mathf.Max(maxStepCount, movementPath.Count);
        }

        if (regroupPathByEnemy.Count <= 0)
        {
            lastRegroupFailureReason = "목표 편대 칸에 도착하지 않은 구성원의 복귀 경로가 없습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 구성원별 재구성 경로를 한 단계씩 검증하고 실제 확정된 경로를 그룹 연출로 전달한다.
    /// </summary>
    private int ExecuteRegroupSteps(
        ActionResolutionContext context,
        int maxStepCount,
        out bool alertTriggered)
    {
        alertTriggered = false;
        PrepareGroupMoveSnapshotBuffers(maxStepCount);

        int completedSteps = 0;
        for (int stepIndex = 0; stepIndex < maxStepCount; stepIndex++)
        {
            if (!TryBuildRegroupMovementOrder(stepIndex))
            {
                break;
            }

            for (int i = 0; i < regroupMovementOrder.Count; i++)
            {
                EnemyContext enemy = regroupMovementOrder[i];
                GridPosition next = regroupNextPositionByEnemy[enemy];
                if (!enemy.GridActor.TryMoveTo(next))
                {
                    Debug.LogError(
                        $"{nameof(EnemyPatrolGroup)}: {name} 그룹의 사전 검증된 재구성 이동 중 {enemy.name} 적을 {next} 칸으로 이동시키지 못했습니다.",
                        enemy);
                    EnqueueCompletedGroupMove(context, "적 조사 후 편대 동시 복귀 연출");
                    return completedSteps;
                }
            }

            // 점유를 모두 확정한 뒤 이동한 구성원의 방향·시야와 외부 이벤트를 공개한다.
            for (int i = 0; i < members.Count; i++)
            {
                EnemyContext enemy = members[i].Enemy;
                if (enemy == null || !regroupNextPositionByEnemy.TryGetValue(enemy, out GridPosition toPosition))
                {
                    continue;
                }

                GridPosition fromPosition = stepIndex == 0
                    ? groupMoveStartByEnemy[enemy]
                    : regroupPathByEnemy[enemy][stepIndex - 1];
                if (!EnemyMovementUtility.RefreshAfterFormationStep(enemy, fromPosition, toPosition))
                {
                    Debug.LogError($"{nameof(EnemyPatrolGroup)}: {enemy.name} 적의 재구성 이동 방향과 시야를 갱신하지 못했습니다.", enemy);
                    EnqueueCompletedGroupMove(context, "적 조사 후 편대 동시 복귀 연출");
                    return completedSteps;
                }

                groupMovePathByEnemy[enemy].Add(toPosition);
                context.Publish(new MoveStepEnteredLogicEvent(enemy.GridActor, toPosition));
            }

            completedSteps++;
            if (TryFindFirstPlayerDetector(out EnemyContext detectingEnemy, out GridPosition detectedPosition))
            {
                context.Publish(new AlertTriggeredLogicEvent(
                    detectedPosition,
                    detectingEnemy,
                    detectingEnemy.GridSight));
                alertTriggered = true;
                break;
            }

            if (regroupStepHasWaitingMember)
            {
                // 기다린 구성원의 남은 경로는 현재 위치와 달라졌으므로 다음 적 턴에 전부 다시 계산한다.
                if (logPatrol)
                {
                    string reason = string.IsNullOrWhiteSpace(lastRegroupFailureReason)
                        ? "구성원 사이의 다음 칸 충돌"
                        : lastRegroupFailureReason;
                    Debug.Log($"{nameof(EnemyPatrolGroup)}: {name} 그룹이 {reason} 때문에 안전한 구성원만 먼저 이동했습니다. 기다린 구성원은 다음 적 턴에 경로를 다시 계산합니다.", this);
                }

                break;
            }
        }

        if (completedSteps > 0)
        {
            PublishCompletedGroupMoves(context);
            EnqueueCompletedGroupMove(context, "적 조사 후 편대 동시 복귀 연출");
        }

        return completedSteps;
    }

    /// <summary>
    /// 현재 재구성 단계의 목표 충돌을 검사하고 점유 칸을 안전하게 비울 수 있는 이동 순서를 만든다.
    /// </summary>
    private bool TryBuildRegroupMovementOrder(int stepIndex)
    {
        regroupNextPositionByEnemy.Clear();
        regroupTargetPositions.Clear();
        regroupMovementOrder.Clear();
        regroupMoveVisiting.Clear();
        regroupMoveVisited.Clear();
        regroupStepHasWaitingMember = false;

        GridManager gridManager = GridManager.Instance;
        int requestedMoveCount = 0;
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive || !regroupPathByEnemy.TryGetValue(enemy, out List<GridPosition> path) ||
                stepIndex >= path.Count)
            {
                continue;
            }

            requestedMoveCount++;
            GridPosition target = path[stepIndex];
            if (!gridManager.IsInside(target) || gridManager.IsBlocked(target))
            {
                lastRegroupFailureReason = $"{enemy.name} 적의 다음 복귀 칸 {target}이 보드 밖이거나 장애물 칸입니다.";
                continue;
            }

            if (!regroupTargetPositions.Add(target))
            {
                // 같은 칸을 원하는 후순위 구성원은 이번 단계에 기다리고 다음 턴에 새 경로를 계산한다.
                lastRegroupFailureReason = $"둘 이상의 편대 구성원이 다음 복귀 칸 {target}을 동시에 선택했습니다.";
                continue;
            }

            regroupNextPositionByEnemy.Add(enemy, target);
        }

        if (regroupNextPositionByEnemy.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(lastRegroupFailureReason))
            {
                lastRegroupFailureReason = "이번 단계에 이동할 수 있는 편대 구성원이 없습니다.";
            }

            return false;
        }

        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !regroupNextPositionByEnemy.ContainsKey(enemy))
            {
                continue;
            }

            TryAddRegroupMovementOrder(enemy, gridManager);
        }

        // 의존 관계나 자리 교환 때문에 움직일 수 없는 구성원은 이번 단계 목표에서 제외한다.
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && regroupNextPositionByEnemy.ContainsKey(enemy) && !regroupMoveVisited.Contains(enemy))
            {
                regroupNextPositionByEnemy.Remove(enemy);
            }
        }

        regroupStepHasWaitingMember = regroupMovementOrder.Count < requestedMoveCount;
        if (regroupMovementOrder.Count <= 0)
        {
            if (string.IsNullOrWhiteSpace(lastRegroupFailureReason))
            {
                lastRegroupFailureReason = "편대 구성원 사이의 점유 의존 관계 때문에 안전하게 먼저 움직일 구성원이 없습니다.";
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 목표 칸을 점유한 그룹원이 먼저 이동하도록 재귀적으로 순서를 추가하고 자리 교환 순환은 거부한다.
    /// </summary>
    private bool TryAddRegroupMovementOrder(EnemyContext enemy, GridManager gridManager)
    {
        if (regroupMoveVisited.Contains(enemy))
        {
            return true;
        }

        if (!regroupMoveVisiting.Add(enemy))
        {
            lastRegroupFailureReason = $"{enemy.name} 적을 포함한 편대 구성원 사이에 직접 자리 교환 순환이 발생했습니다.";
            return false;
        }

        GridPosition target = regroupNextPositionByEnemy[enemy];
        if (gridManager.TryGetActorAt(target, out GridActor occupied) && occupied != enemy.GridActor)
        {
            EnemyContext occupyingMember = FindAliveMember(occupied);
            if (occupyingMember == null)
            {
                lastRegroupFailureReason = $"{enemy.name} 적의 다음 복귀 칸 {target}을 외부 Actor {occupied.name}이 점유하고 있습니다.";
                regroupMoveVisiting.Remove(enemy);
                return false;
            }

            if (!regroupNextPositionByEnemy.ContainsKey(occupyingMember))
            {
                lastRegroupFailureReason = $"{enemy.name} 적의 다음 복귀 칸 {target}을 이번 단계에 기다리는 {occupyingMember.name} 적이 점유하고 있습니다.";
                regroupMoveVisiting.Remove(enemy);
                return false;
            }

            if (!TryAddRegroupMovementOrder(occupyingMember, gridManager))
            {
                regroupMoveVisiting.Remove(enemy);
                return false;
            }
        }

        regroupMoveVisiting.Remove(enemy);
        regroupMoveVisited.Add(enemy);
        regroupMovementOrder.Add(enemy);
        return true;
    }

    /// <summary>
    /// 지정한 GridActor를 사용하는 살아 있는 편대 구성원을 찾는다.
    /// </summary>
    private EnemyContext FindAliveMember(GridActor actor)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive && enemy.GridActor == actor)
            {
                return enemy;
            }
        }

        return null;
    }

    /// <summary>
    /// 실제로 이동한 모든 구성원의 최종 위치 변경 이벤트를 발행한다.
    /// </summary>
    private void PublishCompletedGroupMoves(ActionResolutionContext context)
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive && groupMovePathByEnemy.TryGetValue(enemy, out List<GridPosition> path) &&
                path.Count > 0)
            {
                context.Publish(new MoveCompletedLogicEvent(enemy.GridActor, enemy.GridActor.GridPosition));
            }
        }
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
        rejectedRegroupAnchors.Clear();
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
    private int ExecuteSharedSteps(ActionResolutionContext context, out bool alertTriggered)
    {
        alertTriggered = false;
        PrepareGroupMoveSnapshotBuffers(sharedStepOffsets.Count);

        int completedSteps = 0;
        for (int stepIndex = 0; stepIndex < sharedStepOffsets.Count; stepIndex++)
        {
            GridPosition offset = sharedStepOffsets[stepIndex];
            if (!CanApplyFormationStep(offset))
            {
                break;
            }

            BuildMovementOrder(offset);
            for (int i = 0; i < orderedMembers.Count; i++)
            {
                EnemyContext enemy = orderedMembers[i];
                GridPosition next = enemy.GridActor.GridPosition + offset;
                if (!enemy.GridActor.TryMoveTo(next))
                {
                    Debug.LogError(
                        $"{nameof(EnemyPatrolGroup)}: {name} 그룹의 사전 검증된 편대 이동 중 {enemy.name} 적을 {next} 칸으로 이동시키지 못했습니다.",
                        enemy);
                    EnqueueCompletedGroupMove(context);
                    return completedSteps;
                }
            }

            // 내부 점유 갱신은 순차 적용하지만 방향·시야와 외부 이벤트는 전원 위치가 확정된 뒤 공개한다.
            for (int i = 0; i < members.Count; i++)
            {
                EnemyContext enemy = members[i].Enemy;
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                GridPosition toPosition = enemy.GridActor.GridPosition;
                GridPosition fromPosition = toPosition - offset;
                if (!EnemyMovementUtility.RefreshAfterFormationStep(enemy, fromPosition, toPosition))
                {
                    Debug.LogError($"{nameof(EnemyPatrolGroup)}: {enemy.name} 적의 편대 이동 방향과 시야를 갱신하지 못했습니다.", enemy);
                    EnqueueCompletedGroupMove(context);
                    return completedSteps;
                }

                groupMovePathByEnemy[enemy].Add(toPosition);
                context.Publish(new MoveStepEnteredLogicEvent(enemy.GridActor, toPosition));
            }

            completedSteps++;
            if (TryFindFirstPlayerDetector(out EnemyContext detectingEnemy, out GridPosition detectedPosition))
            {
                context.Publish(new AlertTriggeredLogicEvent(
                    detectedPosition,
                    detectingEnemy,
                    detectingEnemy.GridSight));
                alertTriggered = true;
                break;
            }
        }

        if (completedSteps > 0)
        {
            for (int i = 0; i < members.Count; i++)
            {
                EnemyContext enemy = members[i].Enemy;
                if (enemy != null && enemy.IsAlive && groupMovePathByEnemy.ContainsKey(enemy))
                {
                    context.Publish(new MoveCompletedLogicEvent(enemy.GridActor, enemy.GridActor.GridPosition));
                }
            }

            EnqueueCompletedGroupMove(context);
        }

        return completedSteps;
    }

    /// <summary>
    /// 현재 한 칸 오프셋을 모든 생존 구성원이 함께 적용할 수 있는지 외부 점유까지 먼저 검사한다.
    /// </summary>
    private bool CanApplyFormationStep(GridPosition offset)
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

            GridPosition target = enemy.GridActor.GridPosition + offset;
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
    /// 그룹 이동 시작 시 생존 구성원의 시작 위치와 빈 실제 경로 버퍼를 등록 순서대로 준비한다.
    /// </summary>
    private void PrepareGroupMoveSnapshotBuffers(int pathCapacity)
    {
        groupMoveStartByEnemy.Clear();
        groupMovePathByEnemy.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            groupMoveStartByEnemy.Add(enemy, enemy.GridActor.GridPosition);
            groupMovePathByEnemy.Add(enemy, new List<GridPosition>(pathCapacity));
        }
    }

    /// <summary>
    /// 모든 구성원의 시야 갱신 뒤 편대 등록 순서상 첫 플레이어 감지자와 감지 칸을 찾는다.
    /// </summary>
    private bool TryFindFirstPlayerDetector(out EnemyContext detectingEnemy, out GridPosition detectedPosition)
    {
        detectingEnemy = null;
        detectedPosition = GridPosition.Zero;
        if (TacticalUnitRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyPatrolGroup)} on {name}에는 편대 이동 감지에 사용할 {nameof(TacticalUnitRegistry)}가 필요합니다.", this);
            return false;
        }

        TacticalUnitRegistry.Instance.GetAliveUnits(UnitFaction.Player, playerDetectionBuffer);
        for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
        {
            EnemyContext enemy = members[memberIndex].Enemy;
            if (enemy == null || !enemy.IsAlive || enemy.AlertState == null || enemy.AlertState.IsAlerted || enemy.GridSight == null)
            {
                continue;
            }

            for (int playerIndex = 0; playerIndex < playerDetectionBuffer.Count; playerIndex++)
            {
                if (playerDetectionBuffer[playerIndex] is not TacticalUnitContext player || player.GridActor == null ||
                    !enemy.GridSight.CanDetectPlayer(player.GridActor.GridPosition))
                {
                    continue;
                }

                detectingEnemy = enemy;
                detectedPosition = player.GridActor.GridPosition;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 실제로 확정된 구성원별 경로를 복사해 하나의 편대 이동 연출 이벤트로 추가한다.
    /// </summary>
    private void EnqueueCompletedGroupMove(
        ActionResolutionContext context,
        string message = "적 그룹 순찰 동시 이동 연출")
    {
        groupMoveSnapshotBuffer.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy == null || !groupMoveStartByEnemy.TryGetValue(enemy, out GridPosition startPosition) ||
                !groupMovePathByEnemy.TryGetValue(enemy, out List<GridPosition> path) || path.Count == 0)
            {
                continue;
            }

            groupMoveSnapshotBuffer.Add(new GroupMovePresentationMemberSnapshot(
                enemy.GridActor,
                startPosition,
                path));
        }

        if (groupMoveSnapshotBuffer.Count > 0)
        {
            context.EnqueuePresentation(PresentationEvent.GroupMove(
                new GroupMovePresentationSnapshot(this, groupMoveSnapshotBuffer),
                message));
        }
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
        groupFacingTurnSnapshotBuffer.Clear();
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive)
            {
                GridDirection previousDirection = enemy.GridSight.FacingDirection;
                if (!EnemyMovementUtility.RefreshFacingDirection(enemy, direction))
                {
                    Debug.LogError($"{nameof(EnemyPatrolGroup)}: {enemy.name} 적의 편대 공통 시야 방향을 갱신하지 못했습니다.", enemy);
                    continue;
                }

                if (previousDirection != direction)
                {
                    groupFacingTurnSnapshotBuffer.Add(new GroupFacingTurnPresentationMemberSnapshot(
                        enemy.GridActor,
                        previousDirection,
                        direction));
                }
            }
        }

        if (groupFacingTurnSnapshotBuffer.Count > 0)
        {
            context.EnqueuePresentation(PresentationEvent.GroupFacingTurn(
                new GroupFacingTurnPresentationSnapshot(this, groupFacingTurnSnapshotBuffer),
                "적 편대 공통 시야 방향 전환 연출"));
        }

        // 방향 전환 연출을 먼저 큐에 넣은 뒤 새 시야의 감지 논리 이벤트를 편대 등록 순서대로 발행한다.
        for (int i = 0; i < members.Count; i++)
        {
            EnemyContext enemy = members[i].Enemy;
            if (enemy != null && enemy.IsAlive && enemy.GridSight != null)
            {
                context.Publish(EnemyPerceptionCoordinator.CapturePlayerDetection(enemy));
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
