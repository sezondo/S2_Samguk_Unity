/// <summary>
/// 연출 이벤트를 처리하는 객체의 공통 규약이다.
/// </summary>
public interface IPresentationEventHandler
{
    /// <summary>
    /// 지정한 연출 이벤트를 이 핸들러가 처리할 수 있는지 확인한다.
    /// </summary>
    bool CanHandle(PresentationEvent presentationEvent);

    /// <summary>
    /// 연출 이벤트 처리를 시작하고 연출 종료 시 완료 핸들에 신호를 보낸다.
    /// </summary>
    void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle);
}
