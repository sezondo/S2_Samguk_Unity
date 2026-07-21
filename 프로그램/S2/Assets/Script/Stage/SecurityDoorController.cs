using UnityEngine;

/// <summary>
/// 지정된 해킹 대상의 완료 이벤트를 받아 보안문의 그리드 점유를 해제한다.
/// 문 비주얼은 직접 제어하지 않고 연출 큐에 문 열림 이벤트를 추가한다.
/// </summary>
public class SecurityDoorController : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Unlock Condition")]
    // 이 대상의 해킹이 완료되면 문을 연다.
    [SerializeField] private HackableObject unlockHackable;

    [Header("Grid Blockers")]
    // 닫힌 문이 점유하는 모든 칸의 GridActor 목록이다.
    [SerializeField] private GridActor[] blockingActors;

    [Header("Debug")]
    // true면 문 개방 논리 처리 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logDoorState = true;

    // true면 이 문은 이미 열려 있어 더 이상 칸을 막지 않는다.
    private bool isOpen;

    public bool IsOpen => isOpen;

    /// <summary>
    /// 문 개방에 필요한 필수 참조를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 해킹 완료 논리 이벤트를 받을 수 있도록 이벤트 버스에 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 논리 이벤트 버스에서 이 문을 등록 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 아직 닫혀 있고 지정된 해킹 대상이 완료된 이벤트만 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return !isOpen &&
            logicEvent is HackCompletedLogicEvent hackCompleted &&
            hackCompleted.Hackable == unlockHackable;
    }

    /// <summary>
    /// 문의 모든 칸 점유를 해제하고 문 열림 연출을 큐에 추가한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (isOpen ||
            logicEvent is not HackCompletedLogicEvent hackCompleted ||
            hackCompleted.Hackable != unlockHackable)
        {
            return;
        }

        for (int i = 0; i < blockingActors.Length; i++)
        {
            blockingActors[i].ReleaseCellOccupation();
        }

        isOpen = true;
        context.EnqueuePresentation(PresentationEvent.SecurityDoorOpened(this, "보안문 열림 연출"));

        if (logDoorState)
        {
            Debug.Log($"{nameof(SecurityDoorController)}: {name} 문을 열고 {blockingActors.Length}개 칸의 점유를 해제했습니다.", this);
        }
    }

    /// <summary>
    /// 문 개방 논리에 필요한 필수 컴포넌트 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (unlockHackable == null)
        {
            Debug.LogError($"{nameof(SecurityDoorController)} on {name}에는 문을 여는 {nameof(HackableObject)} 참조가 필요합니다.", this);
            return false;
        }

        if (blockingActors == null || blockingActors.Length == 0)
        {
            Debug.LogError($"{nameof(SecurityDoorController)} on {name}에는 문이 점유하는 {nameof(GridActor)}가 하나 이상 필요합니다.", this);
            return false;
        }

        for (int i = 0; i < blockingActors.Length; i++)
        {
            GridActor blockingActor = blockingActors[i];
            if (blockingActor == null)
            {
                Debug.LogError($"{nameof(SecurityDoorController)} on {name}의 점유 액터 목록 {i}번 참조가 비어 있습니다.", this);
                return false;
            }

            if (!blockingActor.OccupyCell)
            {
                Debug.LogError($"{nameof(SecurityDoorController)} on {name}의 점유 액터 {blockingActor.name}은 시작 시 칸 점유가 활성화되어 있어야 합니다.", blockingActor);
                return false;
            }

            for (int comparisonIndex = i + 1; comparisonIndex < blockingActors.Length; comparisonIndex++)
            {
                if (blockingActor == blockingActors[comparisonIndex])
                {
                    Debug.LogError($"{nameof(SecurityDoorController)} on {name}의 점유 액터 목록에 {blockingActor.name} 참조가 중복되어 있습니다.", this);
                    return false;
                }
            }
        }

        return true;
    }
}
