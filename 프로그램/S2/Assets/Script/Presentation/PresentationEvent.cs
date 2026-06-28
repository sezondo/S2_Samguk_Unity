/// <summary>
/// 연출 큐에 들어가는 단일 연출 이벤트 데이터다.
/// 판정 결과를 화면에 어떤 순서로 보여줄지 설명한다.
/// </summary>
public readonly struct PresentationEvent
{
    public PresentationEventType Type { get; }
    public GridActor Actor { get; }
    public EnemyContext Enemy { get; }
    public HackableObject Hackable { get; }
    public GridPosition FromPosition { get; }
    public GridPosition ToPosition { get; }
    public GridPosition EventPosition { get; }
    public GridPosition ExecutionPosition { get; }
    public string Message { get; }

    /// <summary>
    /// 지정한 값으로 연출 이벤트를 만든다.
    /// 직접 생성보다 정적 생성 함수를 우선 사용한다.
    /// </summary>
    public PresentationEvent(
        PresentationEventType type,
        GridActor actor,
        EnemyContext enemy,
        HackableObject hackable,
        GridPosition fromPosition,
        GridPosition toPosition,
        GridPosition eventPosition,
        GridPosition executionPosition,
        string message)
    {
        Type = type;
        Actor = actor;
        Enemy = enemy;
        Hackable = hackable;
        FromPosition = fromPosition;
        ToPosition = toPosition;
        EventPosition = eventPosition;
        ExecutionPosition = executionPosition;
        Message = message;
    }

    /// <summary>
    /// 액터 이동 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent MoveActor(GridActor actor, GridPosition fromPosition, GridPosition toPosition, string message = null)
    {
        return new PresentationEvent(PresentationEventType.MoveActor, actor, null, null, fromPosition, toPosition, toPosition, toPosition, message);
    }

    /// <summary>
    /// 발각 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent AlertDetected(GridPosition eventPosition, EnemyContext enemy, string message = null)
    {
        return new PresentationEvent(PresentationEventType.AlertDetected, null, enemy, null, default, default, eventPosition, eventPosition, message);
    }

    /// <summary>
    /// 적 반응 이동 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent EnemyReactionMove(EnemyContext enemy, GridPosition fromPosition, GridPosition toPosition, string message = null)
    {
        GridActor actor = enemy != null ? enemy.GridActor : null;
        return new PresentationEvent(PresentationEventType.EnemyReactionMove, actor, enemy, null, fromPosition, toPosition, toPosition, toPosition, message);
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
        return new PresentationEvent(PresentationEventType.Hack, actor, null, hackable, default, executionPosition, targetPosition, executionPosition, message);
    }

    /// <summary>
    /// 검 투척 연출 이벤트를 만든다.
    /// FromPosition은 투척 전 검 위치, ToPosition은 투척 뒤 검 위치다.
    /// </summary>
    public static PresentationEvent SwordThrow(GridActor actor, GridPosition fromPosition, GridPosition toPosition, string message = null)
    {
        return new PresentationEvent(PresentationEventType.SwordThrow, actor, null, null, fromPosition, toPosition, toPosition, toPosition, message);
    }

    /// <summary>
    /// 검 회수 연출 이벤트를 만든다.
    /// FromPosition은 회수 전 검 위치, ToPosition은 회수 뒤 플레이어 위치다.
    /// </summary>
    public static PresentationEvent SwordRecall(GridActor actor, GridPosition fromPosition, GridPosition toPosition, string message = null)
    {
        return new PresentationEvent(PresentationEventType.SwordRecall, actor, null, null, fromPosition, toPosition, toPosition, toPosition, message);
    }

    /// <summary>
    /// 근접 공격 연출 이벤트를 만든다.
    /// hasSword가 true면 검 보유 근접 공격, false면 검 없음 근접 공격 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent MeleeAttack(GridActor actor, GridPosition fromPosition, GridPosition targetPosition, bool hasSword, string message = null)
    {
        PresentationEventType type = hasSword ? PresentationEventType.MeleeAttackWithSword : PresentationEventType.MeleeAttackUnarmed;
        return new PresentationEvent(type, actor, null, null, fromPosition, targetPosition, targetPosition, targetPosition, message);
    }

    /// <summary>
    /// 스테이지 클리어 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent StageCleared(string message = null)
    {
        return new PresentationEvent(PresentationEventType.StageCleared, null, null, null, default, default, default, default, message);
    }

    /// <summary>
    /// 스테이지 실패 연출 이벤트를 만든다.
    /// </summary>
    public static PresentationEvent StageFailed(string message = null)
    {
        return new PresentationEvent(PresentationEventType.StageFailed, null, null, null, default, default, default, default, message);
    }

    /// <summary>
    /// 로그에서 읽기 쉬운 이벤트 문자열을 반환한다.
    /// </summary>
    public override string ToString()
    {
        string actorName = Actor != null ? Actor.name : "없음";
        string enemyName = Enemy != null ? Enemy.name : "없음";
        string hackableName = Hackable != null ? Hackable.name : "없음";
        return $"{Type} Actor:{actorName} Enemy:{enemyName} Hackable:{hackableName} From:{FromPosition} To:{ToPosition} Event:{EventPosition} Execution:{ExecutionPosition}";
    }
}
