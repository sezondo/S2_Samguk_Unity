using UnityEngine;

/// <summary>
/// 닫힌 보안문을 동적 시야 차단물로 등록하고 지정된 해킹 완료 시 그리드 점유와 시야 차단을 해제한다.
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
    // 닫힌 문의 GridActor를 동적 시야 차단물로 등록한 GridManager다.
    private GridManager sightGridManager;
    // 문의 모든 Blocker가 동적 시야 차단물로 등록되어 있는지 나타낸다.
    private bool sightBlockersRegistered;

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
        TryRegisterSightBlockers(false);
    }

    /// <summary>
    /// 초기화 순서 때문에 OnEnable에서 놓친 닫힌 문 시야 차단 등록을 다시 시도한다.
    /// </summary>
    private void Start()
    {
        if (!TryRegisterSightBlockers(true))
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 논리 이벤트 버스에서 이 문을 등록 해제하고, 오브젝트 자체가 비활성화될 때만 시야 차단 등록도 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);

        // Controller 컴포넌트만 꺼진 경우에도 화면과 점유가 남은 닫힌 문은 계속 시야를 막아야 한다.
        if (!gameObject.activeInHierarchy)
        {
            UnregisterSightBlockers();
        }
    }

    /// <summary>
    /// Controller가 제거될 때 남아 있는 동적 시야 차단 등록을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        UnregisterSightBlockers();
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

        UnregisterSightBlockers();

        for (int i = 0; i < blockingActors.Length; i++)
        {
            blockingActors[i].ReleaseCellOccupation();
        }

        isOpen = true;
        context.EnqueuePresentation(PresentationEvent.SecurityDoorOpened(this, "보안문 열림 연출"));
        if (PlayerVisionManager.Instance != null)
        {
            // 문 열림 비주얼 뒤에 새로 확보된 시야가 적용되도록 같은 연출 문맥에 추가한다.
            PlayerVisionManager.Instance.RefreshVision(context);
        }

        if (logDoorState)
        {
            Debug.Log($"{nameof(SecurityDoorController)}: {name} 문을 열고 {blockingActors.Length}개 칸의 점유를 해제했습니다.", this);
        }
    }

    /// <summary>
    /// 기존 문 Blocker 참조를 사용해 닫힌 문 칸을 동적 시야 차단물로 등록한다.
    /// </summary>
    private bool TryRegisterSightBlockers(bool logMissingGridManager)
    {
        if (isOpen || sightBlockersRegistered)
        {
            return true;
        }

        sightGridManager = GridManager.Instance;
        if (sightGridManager == null)
        {
            if (logMissingGridManager)
            {
                Debug.LogError($"{nameof(SecurityDoorController)} on {name}에는 동적 시야 차단을 등록할 씬의 {nameof(GridManager)}가 필요합니다.", this);
            }

            return false;
        }

        for (int i = 0; i < blockingActors.Length; i++)
        {
            if (sightGridManager.RegisterSightBlocker(blockingActors[i]))
            {
                continue;
            }

            for (int rollbackIndex = 0; rollbackIndex < i; rollbackIndex++)
            {
                sightGridManager.UnregisterSightBlocker(blockingActors[rollbackIndex]);
            }

            return false;
        }

        sightBlockersRegistered = true;
        return true;
    }

    /// <summary>
    /// 현재 문 Blocker의 동적 시야 차단 등록을 모두 해제한다.
    /// </summary>
    private void UnregisterSightBlockers()
    {
        if (!sightBlockersRegistered || sightGridManager == null)
        {
            return;
        }

        for (int i = 0; i < blockingActors.Length; i++)
        {
            sightGridManager.UnregisterSightBlocker(blockingActors[i]);
        }

        sightBlockersRegistered = false;
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
