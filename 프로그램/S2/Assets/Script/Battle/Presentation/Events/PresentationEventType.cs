/// <summary>
/// 연출 큐에서 처리할 이벤트 종류다.
/// 판정 시스템은 이 타입으로 어떤 연출 결과인지 알린다.
/// </summary>
public enum PresentationEventType
{
    None,
    BattleIntro,
    MoveActor,
    AlertDetected,
    SuspicionDetected,
    EnemyReactionMove,
    Attack,
    Hack,
    SecurityDoorOpen,
    PlayerVisionChanged,
    ActorVisibilityOverride,
    SwordMove,
    MeleeAttackWithSword,
    MeleeAttackUnarmed,
    CombatAction,
    Interact,
    StageCleared,
    StageFailed,
    GroupMove,
    EnemyFacingTurn,
    GroupFacingTurn,
    CombatCameraFocus,
    CombatCameraRestore,
    EnemyAwarenessChanged,
    // 발각 적을 비춘 뒤 그 위치에 머무는 카메라 이동이다.
    AlertCameraFocus,
}

/// <summary>
/// 검 Visual이 이동하는 행동의 연출 목적이다.
/// 논리 행동 종류와 분리해 투척, 해킹 이동, 회수의 화면 처리를 구분한다.
/// </summary>
public enum SwordMoveKind
{
    None,
    Throw,
    Hack,
    Recall,
}
