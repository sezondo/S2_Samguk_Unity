/// <summary>
/// 연출 큐에서 처리할 이벤트 종류다.
/// 판정 시스템은 이 타입으로 어떤 연출 결과인지 알린다.
/// </summary>
public enum PresentationEventType
{
    None,
    MoveActor,
    AlertDetected,
    EnemyReactionMove,
    Attack,
    Hack,
    SwordThrow,
    SwordRecall,
    MeleeAttackWithSword,
    MeleeAttackUnarmed,
    Interact,
    StageCleared,
    StageFailed,
}
