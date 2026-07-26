using UnityEngine;

/// <summary>
/// 연출 큐 동작을 확인하기 위한 임시 디버그 리시버다.
/// 실제 연출 컴포넌트가 붙기 전까지 이벤트 수신과 완료 신호 흐름을 로그로 검증한다.
/// </summary>
public class DebugPresentationEventReceiver : MonoBehaviour, IPresentationEventHandler
{
    [Header("Debug")]
    // true면 모든 연출 이벤트를 처리 대상으로 받아 즉시 완료한다.
    [SerializeField] private bool handleAllEvents = true;
    // false면 연출 이벤트를 처리하지 않는 핸들러로 동작한다.
    [SerializeField] private bool completeImmediately = true;
    // true면 수신한 연출 이벤트를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logReceivedEvents = true;

    /// <summary>
    /// 연출 큐에 핸들러 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TryRegisterQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 등록을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 큐 핸들러 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// 현재 씬의 연출 큐에 핸들러 등록을 시도한다.
    /// </summary>
    private void TryRegisterQueue(bool logMissingQueue)
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

        queue.Register(this);
    }

    /// <summary>
    /// 디버그 리시버가 현재 연출 이벤트를 처리할지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return handleAllEvents;
    }

    /// <summary>
    /// 디버그용으로 연출 이벤트를 받고 설정에 따라 즉시 완료 처리한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (logReceivedEvents)
        {
            Debug.Log($"{nameof(DebugPresentationEventReceiver)}: 연출 이벤트를 수신했습니다. 이벤트: {presentationEvent}", this);
        }

        if (completeImmediately)
        {
            handle.Complete();
        }
    }
}
