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
    public GridPosition KnownPlayerPosition { get; }
    public EnemyAlertReason Reason { get; }

    /// <summary>
    /// 지정한 적의 발각 상태 전환 이벤트를 만든다.
    /// </summary>
    public EnemyAlertedLogicEvent(
        EnemyContext enemy,
        EnemyContext sourceEnemy,
        GridPosition detectedPosition,
        GridPosition knownPlayerPosition,
        EnemyAlertReason reason)
    {
        Enemy = enemy;
        SourceEnemy = sourceEnemy;
        DetectedPosition = detectedPosition;
        KnownPlayerPosition = knownPlayerPosition;
        Reason = reason;
    }
}

/// <summary>
/// 적이 경계 상태로 전환된 원인이다.
/// </summary>
public enum EnemyAlertReason
{
    SightDetected,
    Damaged,
    Spread,
    Scripted,
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

/// <summary>
/// 피해 적용과 그 결과 연출 생성을 요청하는 논리 이벤트다.
/// 공격 행동은 피해 결과를 직접 해석하지 않고 이 이벤트로 표준 피해 처리를 요청한다.
/// </summary>
public sealed class ApplyDamageLogicEvent : IActionLogicEvent
{
    public GridActor Attacker { get; }
    public GridActor Target { get; }
    public GridPosition FromPosition { get; }
    public GridPosition TargetPosition { get; }
    public int Damage { get; }
    public AttackPresentationKind PresentationKind { get; }
    public string Message { get; }

    /// <summary>
    /// 지정한 공격과 피해 적용 요청 이벤트를 만든다.
    /// </summary>
    public ApplyDamageLogicEvent(
        GridActor attacker,
        GridActor target,
        GridPosition fromPosition,
        GridPosition targetPosition,
        int damage,
        AttackPresentationKind presentationKind,
        string message)
    {
        Attacker = attacker;
        Target = target;
        FromPosition = fromPosition;
        TargetPosition = targetPosition;
        Damage = damage;
        PresentationKind = presentationKind;
        Message = message;
    }
}

/// <summary>
/// 피해 가능 대상에게 피해 적용을 시도했음을 알리는 논리 이벤트다.
/// </summary>
public sealed class DamageAppliedLogicEvent : IActionLogicEvent
{
    public GridActor Attacker { get; }
    public GridActor Target { get; }
    public GridPosition TargetPosition { get; }
    public DamageResult Result { get; }
    public int Damage => Result.Damage;
    public bool Applied => Result.Applied;

    /// <summary>
    /// 지정한 공격자와 대상 사이의 피해 적용 이벤트를 만든다.
    /// </summary>
    public DamageAppliedLogicEvent(GridActor attacker, GridActor target, GridPosition targetPosition, DamageResult result)
    {
        Attacker = attacker;
        Target = target;
        TargetPosition = targetPosition;
        Result = result;
    }
}

/// <summary>
/// 액터가 이번 피해 결과로 새로 전투불능이 됐음을 알리는 논리 이벤트다.
/// </summary>
public sealed class ActorDiedLogicEvent : IActionLogicEvent
{
    public GridActor Attacker { get; }
    public GridActor DeadActor { get; }
    public GridPosition DeadPosition { get; }
    public DamageResult Result { get; }

    /// <summary>
    /// 지정한 액터의 전투불능 이벤트를 만든다.
    /// </summary>
    public ActorDiedLogicEvent(GridActor attacker, GridActor deadActor, GridPosition deadPosition, DamageResult result)
    {
        Attacker = attacker;
        DeadActor = deadActor;
        DeadPosition = deadPosition;
        Result = result;
    }
}

/// <summary>
/// 공격과 피격을 하나의 연출 이벤트로 묶을 때 사용하는 공격 표현 종류다.
/// </summary>
public enum AttackPresentationKind
{
    None,
    SwordThrow,
    MeleeWithSword,
    MeleeUnarmed,
    PlayerGun,
    EnemyRanged,
}
