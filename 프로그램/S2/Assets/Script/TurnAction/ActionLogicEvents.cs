/// <summary>
/// 액터가 이동 경로의 한 칸에 진입했음을 알리는 논리 이벤트다.
/// </summary>
public sealed class MoveStepEnteredLogicEvent : IActionLogicEvent
{
    public GridActor Actor { get; }
    public GridPosition StepPosition { get; }

    /// <summary>
    /// 지정한 액터가 한 칸에 진입한 이벤트를 만든다.
    /// </summary>
    public MoveStepEnteredLogicEvent(GridActor actor, GridPosition stepPosition)
    {
        Actor = actor;
        StepPosition = stepPosition;
    }
}

/// <summary>
/// 액터의 이동 행동이 최종 칸에서 끝났음을 알리는 논리 이벤트다.
/// </summary>
public sealed class MoveCompletedLogicEvent : IActionLogicEvent
{
    public GridActor Actor { get; }
    public GridPosition CompletedPosition { get; }

    /// <summary>
    /// 지정한 액터가 이동을 완료한 이벤트를 만든다.
    /// </summary>
    public MoveCompletedLogicEvent(GridActor actor, GridPosition completedPosition)
    {
        Actor = actor;
        CompletedPosition = completedPosition;
    }
}

/// <summary>
/// 플레이어가 적 시야에 들어와 발각됐음을 알리는 논리 이벤트다.
/// </summary>
public sealed class AlertTriggeredLogicEvent : IActionLogicEvent
{
    public GridPosition DetectedPosition { get; }
    public EnemyContext DetectingEnemy { get; }
    public EnemyGridSight DetectingSight { get; }

    /// <summary>
    /// 지정한 칸에서 발생한 발각 이벤트를 만든다.
    /// </summary>
    public AlertTriggeredLogicEvent(GridPosition detectedPosition, EnemyContext detectingEnemy, EnemyGridSight detectingSight)
    {
        DetectedPosition = detectedPosition;
        DetectingEnemy = detectingEnemy;
        DetectingSight = detectingSight;
    }
}

/// <summary>
/// 적 하나가 발각 상태로 전환됐음을 알리는 논리 이벤트다.
/// </summary>
public sealed class EnemyAlertedLogicEvent : IActionLogicEvent
{
    public EnemyContext Enemy { get; }
    public EnemyContext SourceEnemy { get; }
    public GridPosition DetectedPosition { get; }

    /// <summary>
    /// 지정한 적의 발각 상태 전환 이벤트를 만든다.
    /// </summary>
    public EnemyAlertedLogicEvent(EnemyContext enemy, EnemyContext sourceEnemy, GridPosition detectedPosition)
    {
        Enemy = enemy;
        SourceEnemy = sourceEnemy;
        DetectedPosition = detectedPosition;
    }
}

/// <summary>
/// 스테이지 목표가 달성됐음을 알리는 논리 이벤트다.
/// </summary>
public sealed class StageClearedLogicEvent : IActionLogicEvent
{
    public StageGoal StageGoal { get; }

    /// <summary>
    /// 지정한 목표 달성 이벤트를 만든다.
    /// </summary>
    public StageClearedLogicEvent(StageGoal stageGoal)
    {
        StageGoal = stageGoal;
    }
}

/// <summary>
/// 해킹 대상 하나의 해킹 처리가 완료됐음을 알리는 논리 이벤트다.
/// </summary>
public sealed class HackCompletedLogicEvent : IActionLogicEvent
{
    public GridActor Actor { get; }
    public HackableObject Hackable { get; }
    public GridPosition TargetPosition { get; }
    public GridPosition ExecutionPosition { get; }

    /// <summary>
    /// 지정한 대상의 해킹 완료 이벤트를 만든다.
    /// </summary>
    public HackCompletedLogicEvent(
        GridActor actor,
        HackableObject hackable,
        GridPosition targetPosition,
        GridPosition executionPosition)
    {
        Actor = actor;
        Hackable = hackable;
        TargetPosition = targetPosition;
        ExecutionPosition = executionPosition;
    }
}

/// <summary>
/// 검 투척 행동으로 검의 기준 칸이 바뀌었음을 알리는 논리 이벤트다.
/// </summary>
public sealed class SwordThrownLogicEvent : IActionLogicEvent
{
    public GridActor Actor { get; }
    public GridPosition FromPosition { get; }
    public GridPosition ToPosition { get; }

    /// <summary>
    /// 지정한 검 투척 완료 이벤트를 만든다.
    /// </summary>
    public SwordThrownLogicEvent(GridActor actor, GridPosition fromPosition, GridPosition toPosition)
    {
        Actor = actor;
        FromPosition = fromPosition;
        ToPosition = toPosition;
    }
}

/// <summary>
/// 검 회수 행동으로 검이 플레이어 위치로 돌아왔음을 알리는 논리 이벤트다.
/// </summary>
public sealed class SwordRecalledLogicEvent : IActionLogicEvent
{
    public GridActor Actor { get; }
    public GridPosition FromPosition { get; }
    public GridPosition ToPosition { get; }

    /// <summary>
    /// 지정한 검 회수 완료 이벤트를 만든다.
    /// </summary>
    public SwordRecalledLogicEvent(GridActor actor, GridPosition fromPosition, GridPosition toPosition)
    {
        Actor = actor;
        FromPosition = fromPosition;
        ToPosition = toPosition;
    }
}
