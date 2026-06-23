using System.Collections;
using UnityEngine;

/// <summary>
/// 해킹 연출 이벤트를 받아 임시 대기/로그 연출을 처리하는 Presenter다.
/// 검 비행과 실제 해킹 이펙트는 후속 아트 작업에서 이 컴포넌트를 확장해 연결한다.
/// </summary>
public class HackPresenter : MonoBehaviour
{
    [Header("Target")]
    // 이 Presenter가 해킹 연출을 처리할 해킹 대상이다.
    [SerializeField] private HackableObject targetHackable;

    [Header("Presentation")]
    // true면 HackableData.HackDuration만큼 대기한 뒤 큐 완료 신호를 보낸다.
    [SerializeField] private bool waitHackDuration = true;

    [Header("Debug")]
    // true면 해킹 연출 시작과 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logHackFlow = true;

    // 현재 실행 중인 해킹 연출 코루틴이다.
    private Coroutine hackCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;

    /// <summary>
    /// 필수 참조를 검사하고 연출 큐 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

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
    /// 큐 구독을 해제하고 진행 중인 이벤트가 있으면 큐 정지를 막기 위해 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.PresentationEventStarted -= HandlePresentationEventStarted;
        }

        if (hackCoroutine != null)
        {
            StopCoroutine(hackCoroutine);
            hackCoroutine = null;
        }

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
            activeHandle = null;
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
                Debug.LogError($"{nameof(HackPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.PresentationEventStarted -= HandlePresentationEventStarted;
        queue.PresentationEventStarted += HandlePresentationEventStarted;
    }

    /// <summary>
    /// 해킹 연출 이벤트 중 자신이 담당하는 해킹 대상 이벤트만 처리한다.
    /// </summary>
    private bool HandlePresentationEventStarted(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (presentationEvent.Type != PresentationEventType.Hack || presentationEvent.Hackable != targetHackable)
        {
            return false;
        }

        if (hackCoroutine != null)
        {
            Debug.LogError($"{nameof(HackPresenter)} on {name}은 이미 해킹 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return true;
        }

        if (!HasValidReference())
        {
            handle.Complete();
            return true;
        }

        hackCoroutine = StartCoroutine(PlayHack(presentationEvent, handle));
        return true;
    }

    /// <summary>
    /// 임시 해킹 연출을 처리하고 완료 신호를 보낸다.
    /// </summary>
    private IEnumerator PlayHack(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        activeHandle = handle;

        if (logHackFlow)
        {
            Debug.Log($"{nameof(HackPresenter)}: {targetHackable.name} 해킹 연출을 시작합니다. 대상 칸: {presentationEvent.EventPosition}, 실행 칸: {presentationEvent.ExecutionPosition}", this);
        }

        if (waitHackDuration && targetHackable.HackData.HackDuration > 0f)
        {
            yield return new WaitForSeconds(targetHackable.HackData.HackDuration);
        }

        if (logHackFlow)
        {
            Debug.Log($"{nameof(HackPresenter)}: {targetHackable.name} 해킹 연출을 완료했습니다.", this);
        }

        CompleteActiveHack(handle);
    }

    /// <summary>
    /// 현재 해킹 연출 상태를 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveHack(PresentationEventHandle handle)
    {
        hackCoroutine = null;
        activeHandle = null;
        handle.Complete();
    }

    /// <summary>
    /// 해킹 연출에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetHackable == null)
        {
            Debug.LogError($"{nameof(HackPresenter)} on {name}에는 해킹 연출 대상 {nameof(HackableObject)} 참조가 필요합니다.", this);
            return false;
        }

        if (!targetHackable.HasValidReference() || !targetHackable.HasValidData())
        {
            return false;
        }

        return true;
    }
}
