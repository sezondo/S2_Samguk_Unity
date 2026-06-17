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

    // 현재 큐가 이벤트를 실행 중인지 나타낸다.
    public bool IsPlaying { get; private set; }
    // 현재 큐에 대기 중인 이벤트 수다.
    public int QueuedEventCount => eventQueue.Count;

    // 연출 이벤트가 시작될 때 구독자에게 이벤트와 완료 핸들을 전달한다. 처리할 이벤트면 true를 반환해야 한다.
    public event Func<PresentationEvent, PresentationEventHandle, bool> PresentationEventStarted;
    // 큐가 완전히 비었을 때 발생한다.
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
    /// 연출 이벤트를 큐 끝에 추가한다.
    /// </summary>
    public void Enqueue(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Type == PresentationEventType.None)
        {
            Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {nameof(PresentationEventType.None)} 이벤트는 큐에 추가하지 않습니다.", this);
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

        if (eventQueue.Count == 0)
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
    }

    /// <summary>
    /// 큐의 이벤트를 하나씩 실행하고 완료 신호를 기다린다.
    /// </summary>
    private IEnumerator ProcessQueue()
    {
        IsPlaying = true;

        while (eventQueue.Count > 0)
        {
            PresentationEvent presentationEvent = eventQueue.Dequeue();
            bool completed = false;
            PresentationEventHandle handle = new(() => completed = true);

            int handledCount = BroadcastPresentationEvent(presentationEvent, handle);
            if (handledCount <= 0)
            {
                if (logUnhandledEvents)
                {
                    Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {presentationEvent.Type} 이벤트를 처리한 구독자가 없어 자동 완료합니다. 이벤트: {presentationEvent}", this);
                }

                handle.Complete();
            }
            else if (handledCount > 1)
            {
                Debug.LogWarning($"{nameof(ActionPresentationQueue)}: {presentationEvent.Type} 이벤트를 처리하겠다고 응답한 구독자가 {handledCount}개입니다. 현재 구조에서는 이벤트별 책임 처리자를 하나로 두는 것을 권장합니다.", this);
            }

            if (logQueueFlow)
            {
                Debug.Log($"{nameof(ActionPresentationQueue)}: 연출 이벤트 실행을 시작했습니다. 이벤트: {presentationEvent}", this);
            }

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
    /// 현재 이벤트를 모든 구독자에게 전달하고 처리하겠다고 응답한 수를 반환한다.
    /// </summary>
    private int BroadcastPresentationEvent(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (PresentationEventStarted == null)
        {
            return 0;
        }

        int handledCount = 0;
        Delegate[] handlers = PresentationEventStarted.GetInvocationList();
        for (int i = 0; i < handlers.Length; i++)
        {
            if (handlers[i] is not Func<PresentationEvent, PresentationEventHandle, bool> handler)
            {
                continue;
            }

            try
            {
                if (handler.Invoke(presentationEvent, handle))
                {
                    handledCount++;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"{nameof(ActionPresentationQueue)}: {presentationEvent.Type} 이벤트 구독자 실행 중 예외가 발생했습니다.\n{exception}", this);
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
