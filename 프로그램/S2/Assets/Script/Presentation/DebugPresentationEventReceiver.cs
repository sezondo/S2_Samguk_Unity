using UnityEngine;

/// <summary>
/// 연출 큐 동작을 확인하기 위한 임시 디버그 리시버다.
/// 실제 연출 컴포넌트가 붙기 전까지 이벤트 수신과 완료 신호 흐름을 로그로 검증한다.
/// </summary>
public class DebugPresentationEventReceiver : MonoBehaviour
{
    [Header("Debug")]
    // true면 모든 연출 이벤트를 처리 대상으로 받아 즉시 완료한다.
    [SerializeField] private bool handleAllEvents = true;
    // false면 이벤트를 받더라도 처리하지 않는 구독자로 동작한다.
    [SerializeField] private bool completeImmediately = true;
    // true면 수신한 연출 이벤트를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logReceivedEvents = true;

    /// <summary>
    /// 큐 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeQueue(true);
    }

    /// <summary>
    /// 큐 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.PresentationEventStarted -= HandlePresentationEventStarted;
        }
    }

    /// <summary>
    /// 현재 씬의 연출 큐 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeQueue(bool logMissingQueue)
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            if (logMissingQueue)
            {
                Debug.LogError($"{nameof(DebugPresentationEventReceiver)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.PresentationEventStarted -= HandlePresentationEventStarted;
        queue.PresentationEventStarted += HandlePresentationEventStarted;
    }

    /// <summary>
    /// 디버그용으로 연출 이벤트를 받고 즉시 완료 처리한다.
    /// </summary>
    private bool HandlePresentationEventStarted(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (!handleAllEvents)
        {
            return false;
        }

        if (logReceivedEvents)
        {
            Debug.Log($"{nameof(DebugPresentationEventReceiver)}: 연출 이벤트를 수신했습니다. 이벤트: {presentationEvent}", this);
        }

        if (completeImmediately)
        {
            handle.Complete();
        }

        return true;
    }
}
