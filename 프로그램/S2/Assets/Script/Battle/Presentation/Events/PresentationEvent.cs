/// <summary>
/// 연출 큐에 들어가는 단일 연출 이벤트 데이터다.
/// 판정 결과를 화면에 어떤 순서로 보여줄지 설명한다.
/// </summary>
public readonly struct PresentationEvent
{
    public PresentationEventType Type { get; }
    public GridActor Actor { get; }
    public GridActor TargetActor { get; }
    public EnemyContext Enemy { get; }
    public HackableObject Hackable { get; }
    public SecurityDoorController SecurityDoor { get; }
    public PlayerVisionSnapshot VisionSnapshot { get; }
    public bool ActorVisibility { get; }
    public GridPosition FromPosition { get; }
    public GridPosition ToPosition { get; }
    public GridPosition EventPosition { get; }
    public GridPosition ExecutionPosition { get; }
    public MovePresentationPhase MovePhase { get; }
    public SwordMoveKind SwordMoveKind { get; }
    public AttackPresentationKind AttackKind { get; }
    public DamageResult DamageResult { get; }
    public bool HasDamageResult { get; }
    public string Message { get; }

    /// <summary>
    /// 지정한 값으로 연출 이벤트를 만든다.
    /// 직접 생성보다 정적 생성 함수를 우선 사용한다.
    /// </summary>
    public PresentationEvent(
        PresentationEventType type,
        GridActor actor,
        GridActor targetActor,
        EnemyContext enemy,
        HackableObject hackable,
        GridPosition fromPosition,
        GridPosition toPosition,
        GridPosition eventPosition,
        GridPosition executionPosition,
        MovePresentationPhase movePhase,
        AttackPresentationKind attackKind,
        DamageResult damageResult,
        bool hasDamageResult,
        string message,
        SwordMoveKind swordMoveKind = SwordMoveKind.None,
        SecurityDoorController securityDoor = null,
        PlayerVisionSnapshot visionSnapshot = null,
        bool actorVisibility = false)
    {
        Type = type;
        Actor = actor;
        TargetActor = targetActor;
        Enemy = enemy;
        Hackable = hackable;
        SecurityDoor = securityDoor;
        VisionSnapshot = visionSnapshot;
        ActorVisibility = actorVisibility;
        FromPosition = fromPosition;
        ToPosition = toPosition;
        EventPosition = eventPosition;
        ExecutionPosition = executionPosition;
        MovePhase = movePhase;
        SwordMoveKind = swordMoveKind;
        AttackKind = attackKind;
        DamageResult = damageResult;
        HasDamageResult = hasDamageResult;
        Message = message;
    }

    /// <summary>
    /// 액터 이동 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent MoveActor(
        GridActor actor,
        GridPosition fromPosition,
        GridPosition toPosition,
        MovePresentationPhase movePhase = MovePresentationPhase.Single,
        string message = null)
    {
        return new PresentationEvent(PresentationEventType.MoveActor, actor, null, null, null, fromPosition, toPosition, toPosition, toPosition, movePhase, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 발각 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent AlertDetected(GridPosition eventPosition, EnemyContext enemy, string message = null)
    {
        return new PresentationEvent(PresentationEventType.AlertDetected, null, null, enemy, null, default, default, eventPosition, eventPosition, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 적 반응 이동 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent EnemyReactionMove(
        EnemyContext enemy,
        GridPosition fromPosition,
        GridPosition toPosition,
        MovePresentationPhase movePhase = MovePresentationPhase.Single,
        string message = null)
    {
        GridActor actor = enemy != null ? enemy.GridActor : null;
        return new PresentationEvent(PresentationEventType.EnemyReactionMove, actor, null, enemy, null, fromPosition, toPosition, toPosition, toPosition, movePhase, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 해킹 연출 이벤트를 만든다.
    /// EventPosition은 해킹 대상 칸, ExecutionPosition은 나중에 검이 도착할 대상 주변 칸이다.
    /// </summary>
    public static PresentationEvent Hack(
        GridActor actor,
        HackableObject hackable,
        GridPosition targetPosition,
        GridPosition executionPosition,
        string message = null)
    {
        return new PresentationEvent(PresentationEventType.Hack, actor, null, null, hackable, default, executionPosition, targetPosition, executionPosition, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 전투 조작을 열기 전에 실행할 입장 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent BattleIntro(string message = null)
    {
        return new PresentationEvent(PresentationEventType.BattleIntro, null, null, null, null, default, default, default, default, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 지정한 보안문의 열림 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent SecurityDoorOpened(SecurityDoorController securityDoor, string message = null)
    {
        return new PresentationEvent(PresentationEventType.SecurityDoorOpen, null, null, null, null, default, default, default, default, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message, SwordMoveKind.None, securityDoor);
    }

    /// <summary>
    /// 특정 한 칸 이동 시점의 플레이어 시야와 누적 탐색 상태를 화면에 적용하는 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent PlayerVisionChanged(PlayerVisionSnapshot snapshot, string message = null)
    {
        return new PresentationEvent(
            PresentationEventType.PlayerVisionChanged,
            null,
            null,
            null,
            null,
            default,
            default,
            default,
            default,
            MovePresentationPhase.None,
            AttackPresentationKind.None,
            default,
            false,
            message,
            SwordMoveKind.None,
            null,
            snapshot);
    }

    /// <summary>
    /// 공격 연출처럼 현재 시야 밖 Actor를 잠시 표시하거나 임시 표시를 해제하는 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent ActorVisibilityOverride(GridActor actor, bool isVisible, string message = null)
    {
        return new PresentationEvent(
            PresentationEventType.ActorVisibilityOverride,
            actor,
            null,
            null,
            null,
            default,
            default,
            default,
            default,
            MovePresentationPhase.None,
            AttackPresentationKind.None,
            default,
            false,
            message,
            SwordMoveKind.None,
            null,
            null,
            isVisible);
    }

    /// <summary>
    /// 검 Visual 이동 연출 이벤트를 만든다.
    /// FromPosition은 이동 전 검 위치, ToPosition은 이동 뒤 검 위치다.
    /// </summary>
    public static PresentationEvent SwordMove(
        GridActor actor,
        GridPosition fromPosition,
        GridPosition toPosition,
        SwordMoveKind swordMoveKind,
        string message = null)
    {
        return new PresentationEvent(PresentationEventType.SwordMove, actor, null, null, null, fromPosition, toPosition, toPosition, toPosition, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message, swordMoveKind);
    }

    /// <summary>
    /// 근접 공격 연출 이벤트를 만든다.
    /// hasSword가 true면 검 보유 근접 공격, false면 검 없음 근접 공격 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent MeleeAttack(GridActor actor, GridPosition fromPosition, GridPosition targetPosition, bool hasSword, string message = null)
    {
        PresentationEventType type = hasSword ? PresentationEventType.MeleeAttackWithSword : PresentationEventType.MeleeAttackUnarmed;
        AttackPresentationKind attackKind = hasSword ? AttackPresentationKind.MeleeWithSword : AttackPresentationKind.MeleeUnarmed;
        return new PresentationEvent(type, actor, null, null, null, fromPosition, targetPosition, targetPosition, targetPosition, MovePresentationPhase.None, attackKind, default, false, message);
    }

    /// <summary>
    /// 공격, 피격, 사망 여부를 한 번에 처리할 통합 전투 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent CombatAction(
        GridActor attacker,
        GridActor targetActor,
        GridPosition fromPosition,
        GridPosition targetPosition,
        AttackPresentationKind attackKind,
        DamageResult damageResult,
        string message = null)
    {
        return new PresentationEvent(PresentationEventType.CombatAction, attacker, targetActor, null, null, fromPosition, targetPosition, targetPosition, targetPosition, MovePresentationPhase.None, attackKind, damageResult, true, message);
    }

    /// <summary>
    /// 스테이지 클리어 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent StageCleared(string message = null)
    {
        return new PresentationEvent(PresentationEventType.StageCleared, null, null, null, null, default, default, default, default, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 스테이지 실패 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent StageFailed(string message = null)
    {
        return new PresentationEvent(PresentationEventType.StageFailed, null, null, null, null, default, default, default, default, MovePresentationPhase.None, AttackPresentationKind.None, default, false, message);
    }

    /// <summary>
    /// 로그에서 읽기 쉬운 이벤트 문자열을 반환한다.
    /// </summary>
    public override string ToString()
    {
        string actorName = Actor != null ? Actor.name : "없음";
        string targetActorName = TargetActor != null ? TargetActor.name : "없음";
        string enemyName = Enemy != null ? Enemy.name : "없음";
        string hackableName = Hackable != null ? Hackable.name : "없음";
        string securityDoorName = SecurityDoor != null ? SecurityDoor.name : "없음";
        return $"{Type} Actor:{actorName} Target:{targetActorName} Enemy:{enemyName} Hackable:{hackableName} SecurityDoor:{securityDoorName} From:{FromPosition} To:{ToPosition} MovePhase:{MovePhase} SwordMove:{SwordMoveKind} ActorVisibility:{ActorVisibility} Event:{EventPosition} Execution:{ExecutionPosition}";
    }
}
