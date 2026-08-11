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

        lastExecutedEnemyTurnIndex = enemyTurnIndex;
        if (!CanPatrol())
        {
            return false;
        }

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
            GridPosition target = anchor + members[i].FormationOffset;
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
        if (leader == null || targetPoint == null || !targetPoint.TryGetGridPosition(out GridPosition anchor))
        {
            return false;
        }

        GridPosition leaderTarget = anchor + GetFormationOffset(leader);
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
                leader.GridActor.GridPosition,
                leaderTarget,
                searchDistance,
                leaderPathBuffer,
                memberActorSet))
        {
            return false;
        }

        int maxSteps = Mathf.Min(leader.EnemyData.PatrolMoveRange, leaderPathBuffer.Count);
        GridPosition previous = leader.GridActor.GridPosition;
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

        if (leader.GridActor.GridPosition != anchor + GetFormationOffset(leader))
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
    /// 지정한 적의 편대 오프셋을 반환한다.
    /// </summary>
    private GridPosition GetFormationOffset(EnemyContext enemy)
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
