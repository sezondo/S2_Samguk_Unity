using UnityEngine;

/// <summary>
/// 도깨비 환도를 거리 제한 없이 플레이어 현재 칸으로 회수하는 행동을 담당한다.
/// </summary>
public class PlayerSwordRecallAction : MonoBehaviour
{
    [Header("Sword Recall Action")]
    // 플레이어 공통 참조와 검 상태를 제공하는 Context다.
    [SerializeField] private TacticalUnitContext playerContext;
    // true면 플레이어 턴일 때만 검 회수 행동을 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Log")]
    // 검 회수 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 검 회수 불가 사유를 출력할지 정한다.
    [SerializeField] private bool logBlockedAction = true;

    /// <summary>
    /// 검 회수 행동에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 검 회수 행동을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteSwordRecall(ActionResolutionContext resolutionContext)
    {
        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}에는 검 회수 행동을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (!CanExecuteSwordRecall())
        {
            return false;
        }

        GridPosition fromPosition = playerContext.SwordState.CurrentPosition;
        GridPosition toPosition = playerContext.GridActor.GridPosition;

        int cost = playerContext.UnitData.SwordRecallActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.TrySpend(cost))
        {
            LogBlockedAction("AP가 부족합니다");
            return false;
        }

        playerContext.SwordState.RecallToPlayer();

        resolutionContext.Publish(new SwordRecalledLogicEvent(playerContext.GridActor, fromPosition, toPosition));
        resolutionContext.EnqueuePresentation(PresentationEvent.SwordMove(
            playerContext.GridActor,
            fromPosition,
            toPosition,
            SwordMoveKind.Recall,
            "검 회수 이동 연출"));

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerSwordRecallAction)}: 검을 {fromPosition} 칸에서 플레이어 위치 {toPosition} 칸으로 회수했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 턴과 AP 상태 기준으로 검 회수 행동을 실행할 수 있는지 확인한다.
    /// </summary>
    private bool CanExecuteSwordRecall()
    {
        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (playerContext.SwordState.IsRecalled)
        {
            LogBlockedAction("이미 검을 소유 중입니다");
            return false;
        }

        ActionPresentationQueue presentationQueue = ActionPresentationQueue.Instance;
        if (presentationQueue == null)
        {
            LogBlockedAction($"{nameof(ActionPresentationQueue)}가 없습니다");
            return false;
        }

        if (presentationQueue.IsPlaying)
        {
            LogBlockedAction("연출 큐가 실행 중입니다");
            return false;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            LogBlockedAction($"현재 턴이 {turnManager.CurrentSide}입니다");
            return false;
        }

        int cost = playerContext.UnitData.SwordRecallActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.CanSpend(cost))
        {
            LogBlockedAction($"AP가 부족합니다. 필요 AP: {cost}, 현재 AP: {actionPoint.Current}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 검 회수가 막힌 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedAction(string reason)
    {
        if (logBlockedAction)
        {
            Debug.Log($"{nameof(PlayerSwordRecallAction)}: 검을 회수할 수 없습니다. 사유: {reason}", this);
        }
    }

    /// <summary>
    /// 검 회수 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.UnitData == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.ActionPoint == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.SwordState == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(PlayerSwordState)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 검 회수 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        ControllableUnitData unitData = playerContext.UnitData;
        if (unitData.SwordRecallActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallAction)} on {name}의 {nameof(ControllableUnitData)} 검 회수 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
