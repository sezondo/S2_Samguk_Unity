using System;
using UnityEngine;

/// <summary>
/// 플레이어 현재 칸 기준 원거리 총 공격 판정과 실행을 담당한다.
/// 총 공격은 AP와 총알을 각각 1회분 소비하고, 기존 표준 피해 이벤트 통로로 피해를 요청한다.
/// </summary>
public class PlayerGunAttackAction : MonoBehaviour
{
    private const int AmmoCost = 1;

    [Header("Gun Attack")]
    // 플레이어 공통 참조와 총알 상태를 제공하는 Context다.
    [SerializeField] private PlayerContext playerContext;
    // true면 플레이어 턴일 때만 총 공격 행동을 선택하고 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Log")]
    // 총 공격 선택, 취소, 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 총 공격 불가 사유를 출력할지 정한다.
    [SerializeField] private bool logBlockedTarget = true;

    // 플레이어가 현재 총 공격 행동을 선택한 상태인지 나타낸다.
    private bool isGunAttackSelected;

    public bool IsGunAttackSelected => isGunAttackSelected;
    public int GunAttackRange => playerContext.TurnData.GunAttackRange;

    // 총 공격 행동 선택 상태가 됐을 때 발생한다.
    public event Action GunAttackSelected;
    // 총 공격 행동 선택이 해제됐을 때 발생한다.
    public event Action GunAttackCanceled;

    /// <summary>
    /// 총 공격 행동에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 총 공격 선택 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        CancelGunAttackAction();
    }

    /// <summary>
    /// 총 공격 행동을 선택한다.
    /// </summary>
    public void SelectGunAttackAction()
    {
        if (!CanSelectGunAttackAction())
        {
            return;
        }

        isGunAttackSelected = true;
        GunAttackSelected?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGunAttackAction)}: 총 공격 행동을 선택했습니다. 사거리: {GunAttackRange}, 총알: {playerContext.GunAmmo.CurrentAmmo}/{playerContext.GunAmmo.MaxAmmo}", this);
        }
    }

    /// <summary>
    /// 현재 총 공격 행동 선택을 취소한다.
    /// </summary>
    public void CancelGunAttackAction()
    {
        if (!isGunAttackSelected)
        {
            return;
        }

        isGunAttackSelected = false;
        GunAttackCanceled?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGunAttackAction)}: 총 공격 행동 선택을 취소했습니다.", this);
        }
    }

    /// <summary>
    /// 선택된 목표 칸에 총 공격을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteGunAttack(GridPosition targetPosition, ActionResolutionContext resolutionContext)
    {
        if (!isGunAttackSelected)
        {
            return false;
        }

        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 총 공격을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (!CanSelectGunAttackAction())
        {
            return false;
        }

        if (!TryValidateTarget(targetPosition, out GridActor targetActor))
        {
            return false;
        }

        int actionPointCost = playerContext.TurnData.GunAttackActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        PlayerGunAmmo gunAmmo = playerContext.GunAmmo;
        if (!actionPoint.CanSpend(actionPointCost) || !gunAmmo.CanSpend(AmmoCost))
        {
            LogBlockedTarget(targetPosition, $"자원이 부족합니다. 필요 AP: {actionPointCost}, 현재 AP: {actionPoint.Current}, 필요 총알: {AmmoCost}, 현재 총알: {gunAmmo.CurrentAmmo}");
            return false;
        }

        if (!actionPoint.TrySpend(actionPointCost))
        {
            LogBlockedTarget(targetPosition, "AP 소비에 실패했습니다");
            return false;
        }

        if (!gunAmmo.TrySpend(AmmoCost))
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에서 AP 소비 후 총알 소비에 실패했습니다. 총 공격 자원 상태를 확인하세요.", this);
            return false;
        }

        isGunAttackSelected = false;
        GunAttackCanceled?.Invoke();

        GridPosition attackerPosition = playerContext.GridActor.GridPosition;
        int damage = playerContext.TurnData.GunAttackDamage;
        resolutionContext.Publish(new ApplyDamageLogicEvent(
            playerContext.GridActor,
            targetActor,
            attackerPosition,
            targetPosition,
            damage,
            AttackPresentationKind.PlayerGun,
            "총 공격 연출"));

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerGunAttackAction)}: {targetActor.name} 대상에게 총 공격 피해 적용을 요청했습니다. 피해량: {damage}, 남은 총알: {gunAmmo.CurrentAmmo}/{gunAmmo.MaxAmmo}", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 턴, AP, 총알 상태 기준으로 총 공격 행동을 선택할 수 있는지 확인한다.
    /// </summary>
    private bool CanSelectGunAttackAction()
    {
        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (GridManager.Instance == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 목표 칸을 검사할 {nameof(GridManager)}가 필요합니다.", this);
            }

            return false;
        }

        ActionPresentationQueue presentationQueue = ActionPresentationQueue.Instance;
        if (presentationQueue == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 총 공격 연출을 실행할 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            }

            return false;
        }

        if (presentationQueue.IsPlaying)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGunAttackAction)}: 연출 큐가 실행 중이라 총 공격 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGunAttackAction)}: 현재 턴이 {turnManager.CurrentSide}라서 총 공격 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        int actionPointCost = playerContext.TurnData.GunAttackActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (!actionPoint.CanSpend(actionPointCost))
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGunAttackAction)}: AP가 부족해서 총 공격 행동을 선택할 수 없습니다. 필요 AP: {actionPointCost}, 현재 AP: {actionPoint.Current}", this);
            }

            return false;
        }

        PlayerGunAmmo gunAmmo = playerContext.GunAmmo;
        if (!gunAmmo.CanSpend(AmmoCost))
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerGunAttackAction)}: 총알이 부족해서 총 공격 행동을 선택할 수 없습니다. 필요 총알: {AmmoCost}, 현재 총알: {gunAmmo.CurrentAmmo}", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 목표 칸이 플레이어 총 공격 사거리 안이고 피해 가능 대상이 있는지 검사한다.
    /// </summary>
    private bool TryValidateTarget(GridPosition targetPosition, out GridActor targetActor)
    {
        targetActor = null;

        if (!GridManager.Instance.IsInside(targetPosition))
        {
            LogBlockedTarget(targetPosition, "보드 범위 밖입니다");
            return false;
        }

        GridPosition attackerPosition = playerContext.GridActor.GridPosition;
        int distance = attackerPosition.ManhattanDistanceTo(targetPosition);
        if (distance > GunAttackRange)
        {
            LogBlockedTarget(targetPosition, $"총 공격 사거리 밖입니다. 거리: {distance}, 최대 거리: {GunAttackRange}");
            return false;
        }

        if (!GridManager.Instance.TryGetActorAt(targetPosition, out targetActor) || targetActor == playerContext.GridActor)
        {
            LogBlockedTarget(targetPosition, "피해를 줄 대상이 없습니다");
            return false;
        }

        ActorHealth targetHealth = targetActor.GetComponent<ActorHealth>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            LogBlockedTarget(targetPosition, $"{targetActor.name} 대상은 이미 전투불능입니다");
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
    /// 총 공격이 막힌 목표 칸과 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedTarget(GridPosition targetPosition, string reason)
    {
        if (logBlockedTarget)
        {
            Debug.Log($"{nameof(PlayerGunAttackAction)}: {targetPosition} 칸에 총 공격을 실행할 수 없습니다. 사유: {reason}", this);
        }
    }

    /// <summary>
    /// 총 공격 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.TurnData == null)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerTurnData)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.ActionPoint == null)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.GunAmmo == null || !playerContext.GunAmmo.enabled)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}에는 {nameof(PlayerContext)}에 연결된 활성 {nameof(PlayerGunAmmo)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 총 공격 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        PlayerTurnData turnData = playerContext.TurnData;
        if (turnData.GunAttackActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}의 {nameof(PlayerTurnData)} 총 공격 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        if (turnData.GunAttackRange < 0)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}의 {nameof(PlayerTurnData)} 총 공격 사거리는 0 이상이어야 합니다.", this);
            return false;
        }

        if (turnData.GunAttackDamage <= 0)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}의 {nameof(PlayerTurnData)} 총 공격 피해량은 0보다 커야 합니다.", this);
            return false;
        }

        if (turnData.MaxGunAmmo <= 0)
        {
            Debug.LogError($"{nameof(PlayerGunAttackAction)} on {name}의 {nameof(PlayerTurnData)} 최대 총알 수는 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
