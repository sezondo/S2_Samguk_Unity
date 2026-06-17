using System;

/// <summary>
/// 연출 이벤트 처리 완료를 큐 매니저에 알리는 손잡이다.
/// 이벤트를 처리하겠다고 true를 반환한 구독자는 반드시 Complete를 호출해야 한다.
/// </summary>
public sealed class PresentationEventHandle
{
    private readonly Action completed;
    private bool isCompleted;

    public bool IsCompleted => isCompleted;

    /// <summary>
    /// 완료 콜백을 가진 핸들을 만든다.
    /// </summary>
    public PresentationEventHandle(Action completed)
    {
        this.completed = completed;
    }

    /// <summary>
    /// 현재 이벤트 처리가 끝났음을 알린다.
    /// 중복 호출은 무시한다.
    /// </summary>
    public void Complete()
    {
        if (isCompleted)
        {
            return;
        }

        isCompleted = true;
        completed?.Invoke();
    }
}
