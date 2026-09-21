using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 행동 하나에서 파생되는 논리 이벤트와 연출 이벤트를 처리하는 실행 문맥이다.
/// 논리 이벤트 큐가 완전히 빌 때까지 처리한 뒤 연출 큐를 재생할 수 있게 한다.
/// </summary>
public sealed class ActionResolutionContext
{
    // 처리 대기 중인 논리 이벤트 목록이다.
    private readonly Queue<IActionLogicEvent> logicEvents = new();
    // 논리 결과 연출을 저장할 연출 큐다.
    private readonly ActionPresentationQueue presentationQueue;

    // 논리 이벤트 처리에 실패하면 이 문맥의 나머지 논리를 실행하지 않는다.
    public bool HasFailed { get; private set; }

    /// <summary>불완전한 결과에서 후속 판단이 진행되지 않도록 실패를 전파한다.</summary>
    public void Fail(string reason)
    {
        if (HasFailed) return;
        HasFailed = true;
        logicEvents.Clear();
        if (presentationQueue != null) presentationQueue.ReportFailure(reason);
        else Debug.LogError(reason);
    }

    /// <summary>
    /// 지정한 연출 큐를 사용하는 행동 처리 문맥을 만든다.
    /// </summary>
    public ActionResolutionContext(ActionPresentationQueue presentationQueue)
    {
        this.presentationQueue = presentationQueue;
    }

    /// <summary>
    /// 후속 논리 처리가 필요한 이벤트를 대기열에 추가한다.
    /// </summary>
    public void Publish(IActionLogicEvent logicEvent)
    {
        if (HasFailed || logicEvent == null)
        {
            return;
        }

        logicEvents.Enqueue(logicEvent);
    }

    /// <summary>
    /// 판정 결과로 생성된 연출 이벤트를 연출 큐에 추가한다.
    /// </summary>
    public void EnqueuePresentation(PresentationEvent presentationEvent)
    {
        if (presentationQueue == null)
        {
            Debug.LogError($"{nameof(ActionResolutionContext)}에는 {nameof(ActionPresentationQueue)} 참조가 없어 연출 이벤트를 추가할 수 없습니다.");
            return;
        }

        presentationQueue.Enqueue(presentationEvent);
    }

    /// <summary>
    /// 현재 대기 중인 논리 이벤트를 모두 처리한다.
    /// 처리 중 새 이벤트가 추가되면 이어서 처리한다.
    /// </summary>
    public void Resolve()
    {
        while (!HasFailed && logicEvents.Count > 0)
        {
            IActionLogicEvent logicEvent = logicEvents.Dequeue();
            ActionLogicEventBus.Dispatch(logicEvent, this);
        }
    }
}
