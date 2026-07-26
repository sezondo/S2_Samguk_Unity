/// <summary>
/// 해킹 가능한 대상이 제공해야 하는 공통 규약이다.
/// 해킹 가능 데이터와 해킹 생명주기 알림을 제공한다.
/// </summary>
public interface IHackable
{
    HackableData HackData { get; }

    /// <summary>
    /// 해킹 대상이 선택 가능 상태로 표시될 때 호출된다.
    /// </summary>
    void OnHackReady();

    /// <summary>
    /// 해킹 행동이 실제로 시작될 때 호출된다.
    /// </summary>
    void OnHackStarted();

    /// <summary>
    /// 해킹 행동이 완료됐을 때 호출된다.
    /// </summary>
    void OnHackCompleted();

    /// <summary>
    /// 해킹 선택이나 진행이 취소됐을 때 호출된다.
    /// </summary>
    void OnHackCanceled();
}
