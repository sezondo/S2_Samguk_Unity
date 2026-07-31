using UnityEngine;

/// <summary>
/// 플레이어 유닛 사망을 감시해 중요 유닛 사망 또는 전원 사망 시 스테이지 실패를 확정한다.
/// </summary>
public class StageFailureCoordinator : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Reference")]
    // 실패 상태 전환을 요청할 스테이지 상태 매니저다.
    [SerializeField] private StageStateManager stageStateManager;

    /// <summary>
    /// 필수 스테이지 상태 참조를 즉시 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 활성화될 때 사망 논리 이벤트를 받도록 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 비활성화될 때 논리 이벤트 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 플레이어 사망 여부를 확인할 액터 사망 이벤트만 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is ActorDiedLogicEvent;
    }

    /// <summary>
    /// 중요 플레이어 유닛 또는 마지막 플레이어 유닛이 사망하면 실패 연출을 큐에 추가한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not ActorDiedLogicEvent actorDied ||
            stageStateManager == null ||
            !stageStateManager.IsPlaying ||
            TacticalUnitRegistry.Instance == null ||
            !TacticalUnitRegistry.Instance.TryGetPlayerControllableUnit(
                actorDied.DeadActor,
                out TacticalUnitContext deadUnit))
        {
            return;
        }

        bool importantUnitDied = deadUnit.UnitData != null && deadUnit.UnitData.DefeatOnDeath;
        bool allPlayerUnitsDied = !HasAlivePlayerControllableUnit();
        if (!importantUnitDied && !allPlayerUnitsDied)
        {
            return;
        }

        if (!stageStateManager.RequestFail())
        {
            return;
        }

        string reason = importantUnitDied
            ? $"중요 유닛 '{deadUnit.UnitData.DisplayName}' 전투불능"
            : "조작 가능한 플레이어 유닛 전원 전투불능";
        context.EnqueuePresentation(PresentationEvent.StageFailed(reason));
        Debug.Log($"{nameof(StageFailureCoordinator)}: {reason}으로 스테이지 실패를 확정했습니다.", this);
    }

    /// <summary>
    /// 등록된 플레이어 조작 유닛 중 살아 있는 유닛이 하나라도 있는지 확인한다.
    /// </summary>
    private static bool HasAlivePlayerControllableUnit()
    {
        var units = TacticalUnitRegistry.Instance.PlayerControllableUnits;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null && units[i].IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 실패 판정에 필요한 스테이지 상태 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (stageStateManager == null)
        {
            Debug.LogError($"{nameof(StageFailureCoordinator)} on {name}에는 {nameof(StageStateManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
