using UnityEngine;

/// <summary>
/// 스테이지 클리어와 실패 연출 이벤트를 받아 현재는 로그를 출력하고 큐 완료 신호를 보낸다.
/// 추후 결과 UI와 전환 연출을 연결할 씬 단위 Presenter다.
/// </summary>
public class StageResultPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Debug")]
    // true면 스테이지 결과 연출 이벤트 처리 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logStageResult = true;

    /// <summary>
    /// 활성화될 때 현재 씬의 연출 큐에 핸들러 등록을 시도한다.
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
    /// 비활성화될 때 연출 큐 핸들러 등록을 해제한다.
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
                Debug.LogError($"{nameof(StageResultPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 스테이지 클리어 또는 실패 연출 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.StageCleared ||
               presentationEvent.Type == PresentationEventType.StageFailed;
    }

    /// <summary>
    /// 스테이지 결과 연출 이벤트를 로그로 확인하고 즉시 완료 처리한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (logStageResult)
        {
            string resultMessage = presentationEvent.Type == PresentationEventType.StageCleared
                ? "스테이지 클리어 연출 이벤트를 처리했습니다."
                : "스테이지 실패 연출 이벤트를 처리했습니다.";
            Debug.Log($"{nameof(StageResultPresenter)}: {resultMessage}", this);
        }

        handle.Complete();
    }
}
