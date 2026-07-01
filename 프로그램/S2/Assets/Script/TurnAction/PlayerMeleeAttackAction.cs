using System;
using UnityEngine;

/// <summary>
/// 플레이어 현재 칸 기준 8방향 근접 공격 판정과 실행을 담당한다.
/// 검 소유 여부에 따라 피해량과 연출 이벤트 타입을 다르게 만든다.
/// </summary>
public class PlayerMeleeAttackAction : MonoBehaviour
{
    [Header("Melee Attack")]
    // 플레이어 공통 참조와 검 상태를 제공하는 Context다.
    [SerializeField] private PlayerContext playerContext;
    // true면 플레이어 턴일 때만 근접 공격 행동을 선택하고 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Log")]
    // 근접 공격 선택, 취소, 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 근접 공격 불가 사유를 출력할지 정한다.
    [SerializeField] private bool logBlockedTarget = true;

    // 플레이어가 현재 근접 공격 행동을 선택한 상태인지 나타낸다.
    private bool isMeleeAttackSelected;

    public bool IsMeleeAttackSelected => isMeleeAttackSelected;

    // 근접 공격 행동 선택 상태가 됐을 때 발생한다.
    public event Action MeleeAttackSelected;
    // 근접 공격 행동 선택이 해제됐을 때 발생한다.
    public event Action MeleeAttackCanceled;

    /// <summary>
    /// 근접 공격 행동에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 근접 공격 선택 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        CancelMeleeAttackAction();
    }

    /// <summary>
    /// 근접 공격 행동을 선택한다.
    /// </summary>
    public void SelectMeleeAttackAction()
    {
        if (!CanSelectMeleeAttackAction())
        {
            return;
        }

        isMeleeAttackSelected = true;
        MeleeAttackSelected?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerMeleeAttackAction)}: 근접 공격 행동을 선택했습니다.", this);
        }
    }

    /// <summary>
    /// 현재 근접 공격 행동 선택을 취소한다.
    /// </summary>
    public void CancelMeleeAttackAction()
    {
        if (!isMeleeAttackSelected)
        {
            return;
        }

        isMeleeAttackSelected = false;
        MeleeAttackCanceled?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerMeleeAttackAction)}: 근접 공격 행동 선택을 취소했습니다.", this);
        }
    }

    /// <summary>
    /// 선택된 목표 칸에 근접 공격을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteMeleeAttack(GridPosition targetPosition, ActionResolutionContext resolutionContext)
    {
        if (!isMeleeAttackSelected)
        {
            return false;
        }

        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 근접 공격을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (!CanSelectMeleeAttackAction())
        {
            return false;
        }

        if (!TryValidateTarget(targetPosition, out GridActor targetActor))
        {
            return false;
        }

        int cost = playerContext.TurnData.MeleeAttackActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.TrySpend(cost))
        {
            LogBlockedTarget(targetPosition, "AP가 부족합니다");
            return false;
        }

        bool hasSword = playerContext.SwordState.IsRecalled;
        int damage = hasSword ? playerContext.TurnData.MeleeDamageWithSword : playerContext.TurnData.MeleeDamageWithoutSword;

        isMeleeAttackSelected = false;
        MeleeAttackCanceled?.Invoke();

        GridPosition attackerPosition = playerContext.GridActor.GridPosition;
        AttackPresentationKind attackKind = hasSword ? AttackPresentationKind.MeleeWithSword : AttackPresentationKind.MeleeUnarmed;
        resolutionContext.Publish(new ApplyDamageLogicEvent(
            playerContext.GridActor,
            targetActor,
            attackerPosition,
            targetPosition,
            damage,
            attackKind,
            "근접 공격 연출"));

        if (logActionState)
        {
            string swordStateText = hasSword ? "검 보유" : "검 없음";
            Debug.Log($"{nameof(PlayerMeleeAttackAction)}: {targetActor.name} 대상에게 근접 공격 피해 적용을 요청했습니다. 상태: {swordStateText}, 피해량: {damage}", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 턴과 AP 상태 기준으로 근접 공격 행동을 선택할 수 있는지 확인한다.
    /// </summary>
    private bool CanSelectMeleeAttackAction()
    {
        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (GridManager.Instance == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 목표 칸을 검사할 {nameof(GridManager)}가 필요합니다.", this);
            }

            return false;
        }

        ActionPresentationQueue presentationQueue = ActionPresentationQueue.Instance;
        if (presentationQueue == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 근접 공격 연출을 실행할 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            }

            return false;
        }

        if (presentationQueue.IsPlaying)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerMeleeAttackAction)}: 연출 큐가 실행 중이라 근접 공격 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerMeleeAttackAction)}: 현재 턴이 {turnManager.CurrentSide}라서 근접 공격 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        int cost = playerContext.TurnData.MeleeAttackActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.CanSpend(cost))
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerMeleeAttackAction)}: AP가 부족해서 근접 공격 행동을 선택할 수 없습니다. 필요 AP: {cost}, 현재 AP: {actionPoint.Current}", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 목표 칸이 플레이어 8방향 근접 칸이고 피해 가능 대상이 있는지 검사한다.
    /// </summary>
    private bool TryValidateTarget(GridPosition targetPosition, out GridActor targetActor)
    {
        targetActor = null;

        GridPosition attackerPosition = playerContext.GridActor.GridPosition;
        int deltaX = Mathf.Abs(targetPosition.x - attackerPosition.x);
        int deltaY = Mathf.Abs(targetPosition.y - attackerPosition.y);
        bool isAdjacentEightDirection = deltaX <= 1 && deltaY <= 1 && (deltaX + deltaY) > 0;
        if (!isAdjacentEightDirection)
        {
            LogBlockedTarget(targetPosition, "근접 공격은 플레이어 주변 8방향 1칸만 가능합니다");
            return false;
        }

        if (!GridManager.Instance.TryGetActorAt(targetPosition, out targetActor) || targetActor == playerContext.GridActor)
        {
            LogBlockedTarget(targetPosition, "피해를 줄 대상이 없습니다");
            return false;
        }

        if (targetActor.GetComponent<IDamageable>() == null)
        {
            LogBlockedTarget(targetPosition, $"{targetActor.name} 대상에는 {nameof(IDamageable)}이 없습니다");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 근접 공격이 막힌 목표 칸과 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedTarget(GridPosition targetPosition, string reason)
    {
        if (logBlockedTarget)
        {
            Debug.Log($"{nameof(PlayerMeleeAttackAction)}: {targetPosition} 칸에 근접 공격을 실행할 수 없습니다. 사유: {reason}", this);
        }
    }

    /// <summary>
    /// 근접 공격 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.TurnData == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerTurnData)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.ActionPoint == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.SwordState == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerSwordState)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 근접 공격 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        PlayerTurnData turnData = playerContext.TurnData;
        if (turnData.MeleeAttackActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}의 {nameof(PlayerTurnData)} 근접 공격 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        if (turnData.MeleeDamageWithSword <= 0)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}의 {nameof(PlayerTurnData)} 검 보유 근접 피해량은 0보다 커야 합니다.", this);
            return false;
        }

        if (turnData.MeleeDamageWithoutSword <= 0)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackAction)} on {name}의 {nameof(PlayerTurnData)} 검 없음 근접 피해량은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
