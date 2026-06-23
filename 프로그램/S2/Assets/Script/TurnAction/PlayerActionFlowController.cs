using UnityEngine;

/// <summary>
/// 플레이어 행동 하나의 논리 처리와 연출 큐 실행 시점을 조정한다.
/// 논리 이벤트가 모두 처리된 뒤 연출 큐를 재생한다.
/// </summary>
public class PlayerActionFlowController : MonoBehaviour
{
    [Header("Reference")]
    // 플레이어 행동 실행에 필요한 핵심 참조 주머니다.
    [SerializeField] private PlayerContext playerContext;
    // 논리 결과 연출을 재생할 씬 단위 연출 큐다.
    [SerializeField] private ActionPresentationQueue presentationQueue;

    [Header("Log")]
    // true면 행동 실행 차단 사유를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logBlockedAction = true;

    // 씬에서 사용하는 플레이어 행동 흐름 조정자 인스턴스다.
    public static PlayerActionFlowController Instance { get; private set; }

    /// <summary>
    /// 씬의 단일 PlayerActionFlowController 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerActionFlowController)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (!HasValidReference())
        {
            enabled = false;
        }
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
    /// 플레이어 이동 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteMove(GridPosition targetPosition)
    {
        if (!CanStartAction())
        {
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!playerContext.GridMoveAction.TryExecuteMoveTo(targetPosition, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 플레이어 해킹 행동을 실행하고 논리 처리가 끝나면 연출 큐를 재생한다.
    /// </summary>
    public bool TryExecuteHack(HackableObject target)
    {
        if (!CanStartAction())
        {
            return false;
        }

        if (playerContext.HackAction == null)
        {
            Debug.LogError($"{nameof(PlayerActionFlowController)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerHackAction)} 참조가 필요합니다.", this);
            return false;
        }

        ActionResolutionContext resolutionContext = new(presentationQueue);
        if (!playerContext.HackAction.TryExecuteHack(target, resolutionContext))
        {
            return false;
        }

        resolutionContext.Resolve();
        presentationQueue.PlayQueuedEvents();
        return true;
    }

    /// <summary>
    /// 현재 플레이어 행동을 시작할 수 있는지 확인한다.
    /// </summary>
    private bool CanStartAction()
    {
        if (!HasValidReference())
        {
            return false;
        }

        if (presentationQueue.IsPlaying)
        {
            if (logBlockedAction)
            {
                Debug.Log($"{nameof(PlayerActionFlowController)}: 연출 큐가 실행 중이라 새 행동을 시작할 수 없습니다.", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 행동 흐름 조정에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerActionFlowController)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (presentationQueue == null)
        {
            Debug.LogError($"{nameof(PlayerActionFlowController)} on {name}에는 {nameof(ActionPresentationQueue)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
