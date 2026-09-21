using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 하나의 평상 경비 또는 PatrolPoint 그래프 순찰 진행 상태를 관리한다.
/// </summary>
public class EnemyRoutineController : MonoBehaviour
{
    private const int PatrolActionPointCost = 1;

    [Header("Reference")]
    // 평상 행동을 수행할 적 Context다.
    [SerializeField] private EnemyContext enemyContext;
    // 그룹 순찰을 사용하는 적이 공유할 순찰 그룹이다.
    [SerializeField] private EnemyPatrolGroup patrolGroup;

    [Header("Routine")]
    // 이 적의 평상 행동 종류다.
    [SerializeField] private EnemyRoutineType routineType = EnemyRoutineType.Guard;
    // 단독 순찰 적이 시작할 PatrolPoint다.
    [SerializeField] private PatrolPoint startPoint;
    // 같은 목적지로 연속 이동하지 못했을 때 경고할 턴 수다.
    [SerializeField] private int blockedWarningTurns = 3;

    [Header("Log")]
    // true면 목적지 선택과 막힘 결과를 출력한다.
    [SerializeField] private bool logRoutine = true;

    // 경비 적이 조사 후 돌아올 원래 칸이다.
    private GridPosition guardPosition;
    // 경비 적이 조사 후 복원할 원래 시야 방향이다.
    private GridDirection guardLookDirection;
    // 단독 순찰 적이 현재 도착해 있는 지점이다.
    private PatrolPoint currentPoint;
    // 현재 지점 직전에 방문한 지점이다.
    private PatrolPoint previousPoint;
    // 도착할 때까지 유지하는 현재 목적지다.
    private PatrolPoint targetPoint;
    // 현재 순찰 지점에서 남은 대기 턴 수다.
    private int waitTurnsRemaining;
    // 현재 목적지로 연속 이동하지 못한 턴 수다.
    private int blockedTurnCount;

    private readonly List<PatrolPoint> candidatePoints = new();
    private readonly List<GridPosition> pathBuffer = new();

    public EnemyRoutineType RoutineType => routineType;
    public EnemyPatrolGroup PatrolGroup => patrolGroup;
    public PatrolPoint CurrentPoint => currentPoint;
    public PatrolPoint TargetPoint => targetPoint;
    public GridPosition GuardPosition => guardPosition;
    public GridDirection GuardLookDirection => guardLookDirection;

    /// <summary>
    /// 원래 경비 위치와 단독 순찰 시작 상태를 저장한다.
    /// </summary>
    private void Start()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        guardPosition = enemyContext.GridActor.GridPosition;
        guardLookDirection = enemyContext.GridSight.FacingDirection;

        if (routineType == EnemyRoutineType.Patrol && patrolGroup == null)
        {
            currentPoint = startPoint;
            waitTurnsRemaining = startPoint.WaitTurns;
        }
    }

    /// <summary>
    /// 현재 평상 행동을 적 턴에 한 번 실행한다.
    /// </summary>
    public bool TryExecuteRoutineTurn(ActionResolutionContext context)
    {
        if (!enabled || context == null || !enemyContext.IsAlive || !enemyContext.AlertState.IsUnaware)
        {
            return false;
        }

        if (routineType == EnemyRoutineType.Guard)
        {
            return false;
        }

        if (patrolGroup != null)
        {
            return patrolGroup.TryExecuteTurn(enemyContext, context);
        }

        return TryExecuteSoloPatrol(context);
    }

    /// <summary>
    /// 조사 중단 전 목적지를 그대로 유지하며 단독 순찰을 진행한다.
    /// </summary>
    private bool TryExecuteSoloPatrol(ActionResolutionContext context)
    {
        if (!enemyContext.ActionPoint.CanSpend(PatrolActionPointCost))
        {
            return false;
        }

        if (targetPoint == null && waitTurnsRemaining > 0)
        {
            waitTurnsRemaining--;
            EnemyMovementUtility.FaceDirection(enemyContext, currentPoint.LookDirection, context);
            return false;
        }

        if (targetPoint == null && !TrySelectNextPoint())
        {
            HandleBlockedTurn();
            return false;
        }

        int searchDistance = GridManager.Instance.Width * GridManager.Instance.Height;
        if (!targetPoint.TryGetGridPosition(out GridPosition targetPosition) ||
            !GridPathfinder.TryFindPath(GridManager.Instance, enemyContext.GridActor.GridPosition, targetPosition, searchDistance, pathBuffer))
        {
            HandleBlockedTurn();
            return false;
        }

        int stepCount = Mathf.Min(enemyContext.EnemyData.PatrolMoveRange, pathBuffer.Count);
        List<GridPosition> movementPath = pathBuffer.GetRange(0, stepCount);
        int movedSteps = EnemyMovementUtility.MoveAlongPath(enemyContext, movementPath, context, "적 단독 순찰 이동 연출", out bool detectedPlayer);
        if (movedSteps <= 0)
        {
            HandleBlockedTurn();
            return false;
        }

        blockedTurnCount = 0;
        if (!enemyContext.ActionPoint.TrySpend(PatrolActionPointCost))
        {
            Debug.LogError($"{nameof(EnemyRoutineController)}: {enemyContext.name} 적의 순찰 AP 소비에 실패했습니다.", this);
        }

        // 감지 칸에서 멈춘 경우 도착 방향으로 돌거나 순찰 목적지를 갱신하지 않는다.
        if (detectedPlayer) return true;

        if (enemyContext.GridActor.GridPosition == targetPosition)
        {
            previousPoint = currentPoint;
            currentPoint = targetPoint;
            targetPoint = null;
            waitTurnsRemaining = currentPoint.WaitTurns;
            EnemyMovementUtility.FaceDirection(enemyContext, currentPoint.LookDirection, context);
        }

        return true;
    }

    /// <summary>
    /// 직전 지점을 우선 제외하고, 막다른 길이면 직전 지점으로 되돌아갈 목적지를 정한다.
    /// </summary>
    private bool TrySelectNextPoint()
    {
        candidatePoints.Clear();
        IReadOnlyList<PatrolPoint> connections = currentPoint.ConnectedPoints;
        for (int i = 0; i < connections.Count; i++)
        {
            PatrolPoint candidate = connections[i];
            if (candidate != null && candidate != previousPoint && candidate.HasValidData())
            {
                candidatePoints.Add(candidate);
            }
        }

        if (candidatePoints.Count == 0 && previousPoint != null && previousPoint.HasValidData())
        {
            candidatePoints.Add(previousPoint);
        }

        if (candidatePoints.Count == 0)
        {
            return false;
        }

        targetPoint = candidatePoints[Random.Range(0, candidatePoints.Count)];
        if (logRoutine)
        {
            Debug.Log($"{nameof(EnemyRoutineController)}: {enemyContext.name} 적이 다음 순찰 지점으로 {targetPoint.name}을 선택했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 경비 위치로 복귀하되 도중·도착 회전에 감지하면 복귀 완료를 알리지 않는다.
    /// </summary>
    public bool TryReturnGuard(ActionResolutionContext context)
    {
        if (routineType != EnemyRoutineType.Guard)
        {
            return true;
        }

        if (enemyContext.GridActor.GridPosition == guardPosition)
        {
            return !EnemyMovementUtility.FaceDirection(enemyContext, guardLookDirection, context);
        }

        int searchDistance = GridManager.Instance.Width * GridManager.Instance.Height;
        if (!GridPathfinder.TryFindPath(GridManager.Instance, enemyContext.GridActor.GridPosition, guardPosition, searchDistance, pathBuffer))
        {
            return false;
        }

        int stepCount = Mathf.Min(enemyContext.EnemyData.PatrolMoveRange, pathBuffer.Count);
        List<GridPosition> movementPath = pathBuffer.GetRange(0, stepCount);
        EnemyMovementUtility.MoveAlongPath(enemyContext, movementPath, context, "적 경비 위치 복귀 연출", out bool detectedPlayer);
        // 감지를 예약한 뒤 평상 상태·원래 경비 방향으로 복귀하는 후처리를 막는다.
        if (detectedPlayer) return false;
        if (enemyContext.GridActor.GridPosition == guardPosition)
        {
            return !EnemyMovementUtility.FaceDirection(enemyContext, guardLookDirection, context);
        }

        return false;
    }

    /// <summary>
    /// 연속 막힘 횟수를 기록하고 구성 오류 가능성을 경고한다.
    /// </summary>
    private void HandleBlockedTurn()
    {
        blockedTurnCount++;
        if (blockedWarningTurns > 0 && blockedTurnCount >= blockedWarningTurns)
        {
            Debug.LogWarning($"{nameof(EnemyRoutineController)}: {enemyContext.name} 적이 {blockedTurnCount}턴 연속 순찰하지 못했습니다. 연결 지점과 점유 상태를 확인하세요.", this);
        }
    }

    /// <summary>
    /// 평상 행동에 필요한 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyContext == null || !enemyContext.HasValidReference())
        {
            Debug.LogError($"{nameof(EnemyRoutineController)} on {name}에는 유효한 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyRoutineController)} on {name}에는 씬의 {nameof(GridManager)}가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 행동 종류에 맞는 순찰 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (blockedWarningTurns < 0 || enemyContext.EnemyData.PatrolMoveRange <= 0)
        {
            Debug.LogError($"{nameof(EnemyRoutineController)} on {name}의 순찰 수치가 올바르지 않습니다.", this);
            return false;
        }

        if (routineType == EnemyRoutineType.Patrol && patrolGroup == null && (startPoint == null || !startPoint.HasValidData()))
        {
            Debug.LogError($"{nameof(EnemyRoutineController)} on {name}의 단독 순찰에는 유효한 시작 {nameof(PatrolPoint)}가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
