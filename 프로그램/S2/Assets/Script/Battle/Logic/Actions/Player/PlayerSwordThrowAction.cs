using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도깨비 환도를 현재 검 기준 칸에서 목표 칸으로 투척하는 행동을 담당한다.
/// 검 투척은 GridManager.CanEnter()로 막지 않고 보드 안 칸 여부와 사거리만 검사한다.
/// </summary>
public class PlayerSwordThrowAction : MonoBehaviour
{
    [Header("Sword Throw Action")]
    // 플레이어 공통 참조와 검 상태를 제공하는 Context다.
    [SerializeField] private TacticalUnitContext playerContext;
    // true면 플레이어 턴일 때만 검 투척 행동을 선택하고 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Log")]
    // 검 투척 선택, 취소, 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 검 투척 불가 사유를 출력할지 정한다.
    [SerializeField] private bool logBlockedTarget = true;

    // 플레이어가 현재 검 투척 행동을 선택한 상태다.
    private bool isSwordThrowSelected;

    public bool IsSwordThrowSelected => isSwordThrowSelected;
    public int SwordThrowRange => playerContext.UnitData.SwordThrowRange;

    // 검 투척 행동 선택 상태가 됐을 때 발생한다.
    public event Action SwordThrowSelected;
    // 검 투척 행동 선택이 해제됐을 때 발생한다.
    public event Action SwordThrowCanceled;

    /// <summary>
    /// 검 투척 행동에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 검 투척 선택 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        CancelSwordThrowAction();
    }

    /// <summary>
    /// 검 투척 행동을 선택한다.
    /// </summary>
    public void SelectSwordThrowAction()
    {
        if (!CanSelectSwordThrowAction())
        {
            return;
        }

        isSwordThrowSelected = true;
        SwordThrowSelected?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerSwordThrowAction)}: 검 투척 행동을 선택했습니다. 기준 칸: {playerContext.SwordState.CurrentPosition}, 사거리: {SwordThrowRange}", this);
        }
    }

    /// <summary>
    /// 현재 검 투척 행동 선택을 취소한다.
    /// </summary>
    public void CancelSwordThrowAction()
    {
        bool wasSelected = isSwordThrowSelected;
        isSwordThrowSelected = false;
        if (wasSelected)
        {
            SwordThrowCanceled?.Invoke();
        }

        if (wasSelected && logActionState)
        {
            Debug.Log($"{nameof(PlayerSwordThrowAction)}: 검 투척 행동 선택을 취소했습니다.", this);
        }
    }

    /// <summary>
    /// 선택된 목표 칸으로 검 투척 행동을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteSwordThrow(GridPosition targetPosition, ActionResolutionContext resolutionContext)
    {
        if (!isSwordThrowSelected)
        {
            return false;
        }

        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 검 투척 행동을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (!CanSelectSwordThrowAction())
        {
            return false;
        }

        if (!TryValidateTarget(targetPosition, out GridPosition fromPosition))
        {
            return false;
        }

        int cost = playerContext.UnitData.SwordThrowActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.TrySpend(cost))
        {
            LogBlockedTarget(targetPosition, "AP가 부족합니다");
            return false;
        }

        playerContext.SwordState.SetDeployedPosition(targetPosition);
        isSwordThrowSelected = false;
        SwordThrowCanceled?.Invoke();

        // 검 위치 변경을 피해 요청보다 먼저 처리해 검 이동 뒤 새 시야가 열린 다음 타격 연출이 이어지게 한다.
        resolutionContext.Publish(new SwordThrownLogicEvent(playerContext.GridActor, fromPosition, targetPosition));
        bool requestedDamage = TryRequestDamageAtTarget(fromPosition, targetPosition, resolutionContext);
        // 빈 칸 투척도 같은 집중 연출을 사용한다. 대상 참조는 논리 점유 해제 전에 기록한다.
        GridManager.Instance.TryGetActorAt(targetPosition, out GridActor focusTarget);
        if (!requestedDamage || focusTarget == playerContext.GridActor) focusTarget = null;
        resolutionContext.EnqueuePresentation(PresentationEvent.CombatCameraFocus(
            fromPosition, targetPosition, "검 투척 카메라 포커스",
            playerContext.GridActor, focusTarget, AttackPresentationKind.SwordThrow));

        // 피해 여부와 관계없이 검 이동을 먼저 보여주고, 피해가 있으면 CombatAction이 뒤이어 재생된다.
        resolutionContext.EnqueuePresentation(PresentationEvent.SwordMove(
            playerContext.GridActor,
            fromPosition,
            targetPosition,
            SwordMoveKind.Throw,
            "검 투척 이동 연출"));
        if (!requestedDamage)
            resolutionContext.EnqueuePresentation(PresentationEvent.CombatCameraRestore("빈 칸 투척 복귀"));

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerSwordThrowAction)}: 검을 {fromPosition} 칸에서 {targetPosition} 칸으로 투척했습니다.", this);
        }

        return true;
    }

    /// <summary>이전 입력 호출과 호환한다. 미리보기는 공통 HUD가 현재 칸에서 계산한다.</summary>
    public void RefreshSuspicionPreview(GridPosition targetPosition) { }

    /// <summary>포인터 이탈 정리는 공통 HUD가 담당하므로 임시 격자를 만들지 않는다.</summary>
    public void ClearSuspicionTargetWarning() { }

    /// <summary>실제로 보이는 미발각 적에게 새 의심을 유발할 칸인지 검사한다.</summary>
    public bool WillCauseSuspicion(GridPosition targetPosition)
    {
        if (EnemyRegistry.Instance == null || PlayerVisionManager.Instance == null ||
            PlayerVisionPresenter.Instance == null || ActorPresentationRegistry.Instance == null) return false;
        foreach (var enemy in EnemyRegistry.Instance.Enemies)
        {
            if (enemy == null || !enemy.IsAlive || enemy.AlertState == null || enemy.AlertState.IsAlerted ||
                enemy.GridSight == null || !PlayerVisionManager.Instance.IsVisible(enemy.GridActor.GridPosition) ||
                !PlayerVisionPresenter.Instance.ShouldShowEnemyActorInSnapshot(enemy.GridActor) ||
                !ActorPresentationRegistry.Instance.TryGetVisual(enemy.GridActor, out var visual) || visual.VisionAlpha <= .01f) continue;
            foreach (var p in enemy.GridSight.SwordDetectionPositions)
                if (p == targetPosition) return true;
        }
        return false;
    }

    /// <summary>
    /// 검 투척 목표 칸에 피해 가능 대상이 있으면 표준 피해 적용을 요청한다.
    /// </summary>
    private bool TryRequestDamageAtTarget(GridPosition fromPosition, GridPosition targetPosition, ActionResolutionContext resolutionContext)
    {
        if (!GridManager.Instance.TryGetActorAt(targetPosition, out GridActor targetActor) || targetActor == playerContext.GridActor)
        {
            return false;
        }

        if (targetActor.GetComponent<IDamageable>() == null)
        {
            if (logActionState)
            {
                Debug.Log($"{nameof(PlayerSwordThrowAction)}: {targetPosition} 칸의 {targetActor.name} 대상에는 {nameof(IDamageable)}이 없어 검만 이동합니다.", this);
            }

            return false;
        }

        int damage = playerContext.UnitData.SwordThrowDamage;
        resolutionContext.Publish(new ApplyDamageLogicEvent(
            playerContext.GridActor,
            targetActor,
            fromPosition,
            targetPosition,
            damage,
            AttackPresentationKind.SwordThrow,
            "검 투척 연출"));

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerSwordThrowAction)}: 검 투척으로 {targetActor.name} 대상에게 {damage} 피해 적용을 요청했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 턴과 AP 상태 기준으로 검 투척 행동을 선택할 수 있는지 확인한다.
    /// </summary>
    private bool CanSelectSwordThrowAction()
    {
        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (GridManager.Instance == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 목표 칸을 검사할 {nameof(GridManager)}가 필요합니다.", this);
            }

            return false;
        }

        ActionPresentationQueue presentationQueue = ActionPresentationQueue.Instance;
        if (presentationQueue == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 검 투척 연출을 실행할 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            }

            return false;
        }

        if (presentationQueue.IsBusy)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerSwordThrowAction)}: 연출 큐가 실행 중이라 검 투척 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerSwordThrowAction)}: 현재 턴이 {turnManager.CurrentSide}라서 검 투척 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        int cost = playerContext.UnitData.SwordThrowActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.CanSpend(cost))
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerSwordThrowAction)}: AP가 부족해서 검 투척 행동을 선택할 수 없습니다. 필요 AP: {cost}, 현재 AP: {actionPoint.Current}", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 검 투척 목표 칸이 보드 안이고 현재 검 위치 기준 사거리 안인지 검사한다.
    /// </summary>
    private bool TryValidateTarget(GridPosition targetPosition, out GridPosition fromPosition)
    {
        fromPosition = playerContext.SwordState.CurrentPosition;
        bool valid = TryPreviewTarget(targetPosition, out string reason);
        if (!valid) LogBlockedTarget(targetPosition, reason);
        return valid;
    }

    /// <summary>검 현재 위치 기준 거리와 AP를 검사한다. 빈 칸·미탐색 칸 투척 규칙은 유지한다.</summary>
    public bool TryPreviewTarget(GridPosition targetPosition, out string reason)
    {
        reason = "보드 밖";
        if (!isActiveAndEnabled || playerContext == null || playerContext.SwordState == null ||
            GridManager.Instance == null || !GridManager.Instance.IsInside(targetPosition)) return false;
        if (playerContext.SwordState.CurrentPosition.ManhattanDistanceTo(targetPosition) > SwordThrowRange)
        { reason = "사거리 밖"; return false; }
        if (!playerContext.ActionPoint.CanSpend(playerContext.UnitData.SwordThrowActionPointCost))
        { reason = "AP 부족"; return false; }
        reason = "투척 가능";
        return true;
    }

    /// <summary>
    /// 검 투척이 막힌 목표 칸과 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedTarget(GridPosition targetPosition, string reason)
    {
        if (logBlockedTarget)
        {
            Debug.Log($"{nameof(PlayerSwordThrowAction)}: {targetPosition} 칸으로 검을 투척할 수 없습니다. 사유: {reason}", this);
        }
    }

    /// <summary>
    /// 검 투척 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.UnitData == null)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.ActionPoint == null)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.SwordState == null)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(PlayerSwordState)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 검 투척 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        ControllableUnitData unitData = playerContext.UnitData;
        if (unitData.SwordThrowRange < 0)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}의 {nameof(ControllableUnitData)} 검 투척 사거리는 0 이상이어야 합니다.", this);
            return false;
        }

        if (unitData.SwordThrowActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}의 {nameof(ControllableUnitData)} 검 투척 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        if (unitData.SwordThrowDamage <= 0)
        {
            Debug.LogError($"{nameof(PlayerSwordThrowAction)} on {name}의 {nameof(ControllableUnitData)} 검 투척 피해량은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
