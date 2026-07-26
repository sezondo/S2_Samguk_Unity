using UnityEngine;

/// <summary>
/// 담당 보안문의 열림 연출 이벤트를 받아 문 비주얼을 숨기고 큐 완료 신호를 보낸다.
/// 문의 그리드 점유와 개방 상태에는 관여하지 않는다.
/// </summary>
public class SecurityDoorPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Target")]
    // 이 Presenter가 열림 연출을 처리할 보안문 논리 컴포넌트다.
    [SerializeField] private SecurityDoorController targetDoor;
    // 닫힌 문 스프라이트를 포함하며 개방 시 숨길 비주얼 오브젝트다.
    [SerializeField] private GameObject doorVisual;

    [Header("Debug")]
    // true면 문 열림 연출 처리 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logDoorPresentation = true;

    /// <summary>
    /// 필수 참조를 검사하고 현재 문 상태를 비주얼에 반영한 뒤 연출 큐 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        ApplyCurrentDoorState();
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
    /// 연출 큐 핸들러 등록을 해제한다.
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
                Debug.LogError($"{nameof(SecurityDoorPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 문 열림 연출 이벤트 중 자신이 담당하는 보안문 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.SecurityDoorOpen &&
               presentationEvent.SecurityDoor == targetDoor;
    }

    /// <summary>
    /// 담당 보안문의 현재 논리 상태를 비주얼에 반영한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (!HasValidReference())
        {
            handle.Complete();
            return;
        }

        ApplyCurrentDoorState();

        if (logDoorPresentation)
        {
            Debug.Log($"{nameof(SecurityDoorPresenter)}: {targetDoor.name} 문의 열림 비주얼을 적용했습니다.", this);
        }

        handle.Complete();
    }

    /// <summary>
    /// 문 연출에 필요한 필수 컴포넌트 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetDoor == null)
        {
            Debug.LogError($"{nameof(SecurityDoorPresenter)} on {name}에는 연출 대상 {nameof(SecurityDoorController)} 참조가 필요합니다.", this);
            return false;
        }

        if (doorVisual == null)
        {
            Debug.LogError($"{nameof(SecurityDoorPresenter)} on {name}에는 문 비주얼 오브젝트 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 현재 문의 논리 상태에 맞춰 닫힌 문 비주얼 활성 상태를 적용한다.
    /// </summary>
    private void ApplyCurrentDoorState()
    {
        doorVisual.SetActive(!targetDoor.IsOpen);
    }
}
