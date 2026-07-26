/// <summary>
/// 행동 논리 이벤트를 처리하는 시스템의 공통 규약이다.
/// </summary>
public interface IActionLogicEventHandler
{
    /// <summary>
    /// 지정한 논리 이벤트를 이 핸들러가 처리할 수 있는지 확인한다.
    /// </summary>
    bool CanHandle(IActionLogicEvent logicEvent);

    /// <summary>
    /// 논리 이벤트를 처리하고 필요하면 후속 논리 이벤트나 연출 이벤트를 문맥에 추가한다.
    /// </summary>
    void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context);
}
