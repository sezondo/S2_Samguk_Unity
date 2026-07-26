using UnityEngine;

/// <summary>
/// 액터 사망 논리 이벤트를 받아 오브젝트는 유지하되 전술 점유만 해제하는 기본 처리자다.
/// 실제 사망 애니메이션과 시체 정렬은 연출 Presenter에서 별도로 처리한다.
/// </summary>
public sealed class ActorDeathCoordinator : IActionLogicEventHandler
{
    public static ActorDeathCoordinator Instance { get; } = new();

    /// <summary>
    /// 외부 생성을 막고 기본 인스턴스만 사용한다.
    /// </summary>
    private ActorDeathCoordinator()
    {
    }

    /// <summary>
    /// 액터 사망 이벤트만 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is ActorDiedLogicEvent;
    }

    /// <summary>
    /// 사망한 액터의 그리드 칸 점유를 해제한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not ActorDiedLogicEvent actorDied)
        {
            return;
        }

        if (actorDied.DeadActor == null)
        {
            Debug.LogError($"{nameof(ActorDeathCoordinator)}: 사망 처리할 액터가 없어 점유 해제를 건너뜁니다.");
            return;
        }

        actorDied.DeadActor.ReleaseCellOccupation();
        Debug.Log($"{nameof(ActorDeathCoordinator)}: {actorDied.DeadActor.name} 액터가 전투불능이 되어 {actorDied.DeadPosition} 칸 점유를 해제했습니다.", actorDied.DeadActor);
    }
}
