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
/// 적의 위치 또는 시야 방향이 바뀌어 플레이어 감지를 다시 검사해야 함을 알린다.
/// </summary>
public sealed class EnemyPerceptionChangedLogicEvent : IActionLogicEvent
{
    public EnemyContext Enemy { get; }
    // 이동·회전 직후 그 위치의 시야로 확정한 감지 결과다.
    public bool HasDetectedPlayer { get; }
    // 감지 당시 플레이어 칸이며 나중에 바뀐 적 시야로 다시 판정하지 않는다.
    public GridPosition DetectedPlayerPosition { get; }

    /// <summary>한 칸 이동 또는 회전 시점에 확정한 플레이어 감지 결과를 보관한다.</summary>
    public EnemyPerceptionChangedLogicEvent(EnemyContext enemy, bool hasDetectedPlayer, GridPosition detectedPlayerPosition)
    {
        Enemy = enemy;
        HasDetectedPlayer = hasDetectedPlayer;
        DetectedPlayerPosition = detectedPlayerPosition;
    }
}

/// <summary>
/// 적 하나가 이상 현상을 직접 감지하거나 전파받아 의심 행동을 시작했음을 알린다.
/// </summary>
public sealed class EnemySuspicionTriggeredLogicEvent : IActionLogicEvent
{
    public EnemyContext Enemy { get; }
    public EnemySuspicionInfo SuspicionInfo { get; }

    /// <summary>
    /// 의심 상태로 전환되거나 조사 정보가 갱신된 적 이벤트를 만든다.
    /// </summary>
    public EnemySuspicionTriggeredLogicEvent(EnemyContext enemy, EnemySuspicionInfo suspicionInfo)
    {
        Enemy = enemy;
        SuspicionInfo = suspicionInfo;
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
/// 대상 인접 엄폐 방향과 각도를 반영한 원거리 공격 명중 판정을 요청하는 논리 이벤트다.
/// </summary>
public sealed class ResolveAttackLogicEvent : IActionLogicEvent
{
    public GridActor Attacker { get; }
    public GridActor Target { get; }
    public GridPosition FromPosition { get; }
    public GridPosition TargetPosition { get; }
    public int Damage { get; }
    public int BaseHitChance { get; }
    public int MinimumHitChance { get; }
    public int MaximumHitChance { get; }
    public int LowCoverHitPenalty { get; }
    public AttackPresentationKind PresentationKind { get; }
    public string Message { get; }

    /// <summary>
    /// 지정한 원거리 공격의 엄폐·명중 판정 요청을 만든다.
    /// </summary>
    public ResolveAttackLogicEvent(
        GridActor attacker,
        GridActor target,
        GridPosition fromPosition,
        GridPosition targetPosition,
        int damage,
        RangedAttackAccuracyData accuracyData,
        AttackPresentationKind presentationKind,
        string message)
    {
        Attacker = attacker;
        Target = target;
        FromPosition = fromPosition;
        TargetPosition = targetPosition;
        Damage = damage;
        BaseHitChance = accuracyData.BaseHitChance;
        MinimumHitChance = accuracyData.MinimumHitChance;
        MaximumHitChance = accuracyData.MaximumHitChance;
        LowCoverHitPenalty = accuracyData.LowCoverHitPenalty;
        PresentationKind = presentationKind;
        Message = message;
    }
}

/// <summary>
/// 엄폐와 난수 계산을 마치고 명중·빗나감이 확정된 공격 논리 이벤트다.
/// </summary>
public sealed class AttackResolvedLogicEvent : IActionLogicEvent
{
    public ResolveAttackLogicEvent Attack { get; }
    public AttackResult Result { get; }

    /// <summary>
    /// 원본 공격 요청과 확정된 명중 판정 결과를 묶는다.
    /// </summary>
    public AttackResolvedLogicEvent(ResolveAttackLogicEvent attack, AttackResult result)
    {
        Attack = attack;
        Result = result;
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
    // 기존 직렬화 번호를 유지하며 적 근접 연출을 추가한다.
    EnemyMelee,
}
