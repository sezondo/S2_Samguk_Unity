/// <summary>
/// 의심 상태인 적이 현재 수행 중인 조사 단계다.
/// </summary>
public enum SuspiciousBehaviorPhase
{
    MovingToInvestigationPosition,
    Observing,
    Searching,
    ReturningToRoutine,
}
