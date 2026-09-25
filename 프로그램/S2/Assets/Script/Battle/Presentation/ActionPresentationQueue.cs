using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 판정 결과로 생성된 연출 이벤트를 순서대로 실행하는 씬 단위 큐다.
/// 판정이나 실제 연출 내용은 알지 않고, 이벤트 순서와 완료 대기만 담당한다.
/// </summary>
public class ActionPresentationQueue : MonoBehaviour
{
    [Header("Log")]
    // true면 큐 추가, 실행, 완료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logQueueFlow = true;
    // true면 처리자가 없는 이벤트를 경고 로그로 출력한다.
    [SerializeField] private bool logUnhandledEvents = true;
    // 완료 신호가 오래 오지 않을 때 경고를 남길 시간이다. 0 이하이면 타임아웃 경고를 끈다.
    [SerializeField] private float completionWarningSeconds = 10f;

    // 씬에서 사용하는 단일 연출 큐 인스턴스다.
    public static ActionPresentationQueue Instance { get; private set; }

    // 실행 대기 중인 연출 이벤트 목록이다.
    private readonly Queue<PresentationEvent> eventQueue = new();
    // 현재 씬에서 활성화된 연출 이벤트 핸들러 목록이다.
    private readonly List<IPresentationEventHandler> handlers = new();

    // 현재 큐가 이벤트를 실행 중인지 나타낸다.
    public bool IsPlaying { get; private set; }
    // 현재 큐에 대기 중인 이벤트 수다.
    public int QueuedEventCount => eventQueue.Count + (terminalEvent.HasValue ? 1 : 0);

    // 적 논리가 후속 연출을 생산하는 동안 큐가 잠시 비어도 처리를 유지한다.
    public bool IsProducing { get; private set; }
    // 실패 이후 잘못된 상태에서 새 명령을 받지 않는 안전 정지 상태다.
    public bool HasFailed { get; private set; }
    // 입력·턴 전환 잠금은 재생뿐 아니라 생산·대기·오류까지 포함한다.
    public bool IsBusy => IsTurnPending || IsProducing || IsPlaying || QueuedEventCount > 0 || HasFailed;
    // 마지막 큐 완료와 조정자의 턴 종료 사이에도 입력 잠금을 유지한다.
    public bool IsTurnPending { get; private set; }
    // 결과 UI는 모든 확정 연출과 논리 생산이 끝난 뒤에 재생한다.
    private PresentationEvent? terminalEvent;

    /// <summary>적 턴의 논리 생산 구간을 시작한다. 중복 생산은 허용하지 않는다.</summary>
    public bool TryBeginProduction()
    {
        if (IsTurnPending || IsProducing || HasFailed) return false;
        IsTurnPending = true;
        IsProducing = true;
        return true;
    }

    /// <summary>새 연출 생산이 끝났음을 알린다. 이미 쌓인 연출은 계속 재생한다.</summary>
    public void EndProduction() => IsProducing = false;

    /// <summary>조정자가 마지막 연출 완료를 확인하거나 중단될 때 턴 처리 잠금을 해제한다.</summary>
    public void ReleaseTurnProcessing() => IsTurnPending = false;

    /// <summary>실행 오류를 기록하고 후속 논리와 사용자 입력을 차단한다.</summary>
    public void ReportFailure(string reason)
    {
        HasFailed = true;
        Debug.LogError($"{nameof(ActionPresentationQueue)}: 처리 오류로 새 행동을 중단합니다. {reason}", this);
    }

    // 논리 생산과 모든 연출이 끝나 큐가 완전히 비었을 때 발생한다.
    public event Action QueueEmptied;

    /// <summary>
    /// 씬의 단일 ActionPresentationQueue 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(ActionPresentationQueue)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 전역 큐에 연출 이벤트 추가를 시도한다.
    /// </summary>
    public static bool TryEnqueue(PresentationEvent presentationEvent)
    {
        if (Instance == null)
        {
            return false;
        }

        Instance.Enqueue(presentationEvent);
        return true;
    }

    /// <summary>
    /// 전역 큐 실행을 시도한다.
    /// </summary>
    public static bool TryPlayQueuedEvents()
    {
        return Instance != null && Instance.PlayQueuedEvents();
    }

    /// <summary>
    /// 연출 이벤트 핸들러를 중복 없이 등록한다.
    /// </summary>
    public void Register(IPresentationEventHandler handler)
    {
        if (handler == null || handlers.Contains(handler))
        {
            return;
        }

        handlers.Add(handler);
    }

    /// <summary>
    /// 연출 이벤트 핸들러 등록을 해제한다.
    /// </summary>
    public void Unregister(IPresentationEventHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        handlers.Remove(handler);
    }

    /// <summary>
    /// 연출 이벤트를 큐 끝에 추가한다.
    /// </summary>
    public void Enqueue(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Type == PresentationEventType.None)
        {
            Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {nameof(PresentationEventType.None)} 이벤트는 큐에 추가하지 않습니다.", this);
            return;
        }

        if (presentationEvent.Type == PresentationEventType.StageCleared ||
            presentationEvent.Type == PresentationEventType.StageFailed)
        {
            if (!terminalEvent.HasValue) terminalEvent = presentationEvent;
            return;
        }
        eventQueue.Enqueue(presentationEvent);

        if (logQueueFlow)
        {
            Debug.Log($"{nameof(ActionPresentationQueue)}: 연출 이벤트를 큐에 추가했습니다. 이벤트: {presentationEvent}, 대기 수: {eventQueue.Count}", this);
        }
    }

    /// <summary>
    /// 대기 중인 연출 이벤트를 순서대로 실행한다.
    /// </summary>
    public bool PlayQueuedEvents()
    {
        if (IsPlaying)
        {
            return false;
        }

        if (QueuedEventCount == 0 && !IsProducing)
        {
            QueueEmptied?.Invoke();
            return false;
        }

        StartCoroutine(ProcessQueue());
        return true;
    }

    /// <summary>
    /// 현재 대기 중인 이벤트를 모두 제거한다.
    /// 현재 실행 중인 이벤트는 강제로 완료하지 않는다.
    /// </summary>
    public void ClearQueuedEvents()
    {
        eventQueue.Clear();
        terminalEvent = null;
    }

    /// <summary>
    /// 큐의 이벤트를 하나씩 실행하고 완료 신호를 기다린다.
    /// </summary>
    private IEnumerator ProcessQueue()
    {
        IsPlaying = true;

        while (QueuedEventCount > 0 || IsProducing)
        {
            if (eventQueue.Count == 0)
            {
                if (IsProducing) { yield return null; continue; }
                if (terminalEvent.HasValue)
                {
                    eventQueue.Enqueue(terminalEvent.Value);
                    terminalEvent = null;
                }
            }
            PresentationEvent presentationEvent = eventQueue.Dequeue();
            bool completed = false;
            PresentationEventHandle handle = new(() => completed = true);

            int handledCount = BroadcastPresentationEvent(presentationEvent, handle);
            if (handledCount <= 0)
            {
                if (logUnhandledEvents)
                {
                    Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {presentationEvent.Type} 이벤트를 처리한 핸들러가 없어 자동 완료합니다. 이벤트: {presentationEvent}", this);
                }

                handle.Complete();
            }
            else if (handledCount > 1)
            {
                Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {presentationEvent.Type} 이벤트를 처리한 핸들러가 {handledCount}개입니다. 현재 구조에서는 이벤트별 책임 처리자를 하나로 두는 것을 권장합니다.", this);
            }

            if (logQueueFlow)
            {
                Debug.Log($"{nameof(ActionPresentationQueue)}: 연출 이벤트 실행을 시작했습니다. 이벤트: {presentationEvent}", this);
            }

            // 즉시 완료한 투척 배치와 다음 타격 사이에는 프레임을 끼우지 않는다.
            if (!completed)
                yield return WaitUntilCompleted(presentationEvent, handle, () => completed);

            if (logQueueFlow)
            {
                Debug.Log($"{nameof(ActionPresentationQueue)}: 연출 이벤트 실행이 끝났습니다. 이벤트: {presentationEvent}", this);
            }
        }

        IsPlaying = false;
        QueueEmptied?.Invoke();

        if (logQueueFlow)
        {
            Debug.Log($"{nameof(ActionPresentationQueue)}: 모든 연출 이벤트 실행이 끝났습니다.", this);
        }
    }

    /// <summary>
    /// 현재 이벤트를 처리 가능한 모든 핸들러에게 전달하고 실제 처리를 시작한 수를 반환한다.
    /// </summary>
    private int BroadcastPresentationEvent(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        int handledCount = 0;
        IPresentationEventHandler[] handlerSnapshot = handlers.ToArray();
        for (int i = 0; i < handlerSnapshot.Length; i++)
        {
            IPresentationEventHandler handler = handlerSnapshot[i];
            if (handler == null)
            {
                continue;
            }

            try
            {
                if (!handler.CanHandle(presentationEvent))
                {
                    continue;
                }

                handler.Handle(presentationEvent, handle);
                handledCount++;
            }
            catch (Exception exception)
            {
                ReportFailure($"{presentationEvent.Type} 이벤트 핸들러 예외: {exception}");
                handle.Complete();
            }
        }

        return handledCount;
    }

    /// <summary>
    /// 이벤트 처리자가 완료 신호를 보낼 때까지 기다리고, 오래 걸리면 경고를 남긴다.
    /// </summary>
    private IEnumerator WaitUntilCompleted(PresentationEvent presentationEvent, PresentationEventHandle handle, Func<bool> completed)
    {
        float elapsed = 0f;
        bool warned = false;

        while (!completed())
        {
            if (completionWarningSeconds > 0f && !warned)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= completionWarningSeconds)
                {
                    warned = true;
                    Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {presentationEvent.Type} 이벤트 완료 신호를 {completionWarningSeconds:0.##}초 동안 받지 못했습니다. 처리자가 {nameof(PresentationEventHandle)}.{nameof(PresentationEventHandle.Complete)}()를 호출했는지 확인하세요.", this);
                }
            }

            yield return null;
        }

        if (!handle.IsCompleted)
        {
            Debug.LogWarning($"{nameof(ActionPresentationQueue)}: 내부 완료 상태와 핸들 완료 상태가 일치하지 않습니다. 이벤트: {presentationEvent}", this);
        }
    }
}
