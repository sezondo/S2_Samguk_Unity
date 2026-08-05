using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 해킹 행동 판정과 실행을 담당한다.
/// 검 능력 유닛은 대상 주변 실행 칸으로 검을 이동시킨 뒤 해킹 연출을 재생한다.
/// </summary>
public class PlayerHackAction : MonoBehaviour
{
    [Header("Hack Action")]
    // 플레이어 공통 참조와 턴 데이터를 제공하는 필수 Context다.
    [SerializeField] private TacticalUnitContext playerContext;
    // true면 플레이어 턴일 때만 해킹 행동을 선택하고 실행할 수 있다.
    [SerializeField] private bool requirePlayerTurn = true;

    [Header("Log")]
    // 해킹 행동 선택, 취소, 완료 같은 상태 로그를 출력할지 정한다.
    [SerializeField] private bool logActionState = true;
    // 해킹 불가 대상이나 조건 실패 사유를 출력할지 정한다.
    [SerializeField] private bool logBlockedTarget = true;

    // 대상 주변 8칸 후보를 계산할 때 사용하는 방향 목록이다.
    private static readonly GridPosition[] AdjacentEightDirections =
    {
        new(-1, 1),
        new(0, 1),
        new(1, 1),
        new(-1, 0),
        new(1, 0),
        new(-1, -1),
        new(0, -1),
        new(1, -1),
    };

    // 해킹 실행 칸 후보를 거리순으로 정렬할 때 재사용하는 버퍼다.
    private readonly List<GridPosition> executionPositionCandidates = new();

    // 플레이어가 현재 해킹 행동을 선택한 상태인지 나타낸다.
    private bool isHackSelected;

    public bool IsHackSelected => isHackSelected;
    public int HackRange => playerContext.UnitData.HackRange;

    // 해킹 행동 선택 상태가 됐을 때 발생한다.
    public event Action HackSelected;
    // 해킹 행동 선택이 해제됐을 때 발생한다.
    public event Action HackCanceled;

    /// <summary>
    /// 해킹 행동에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 해킹 선택 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        CancelHackAction();
    }

    /// <summary>
    /// 해킹 행동을 선택한다.
    /// </summary>
    public void SelectHackAction()
    {
        if (!CanSelectHackAction())
        {
            return;
        }

        isHackSelected = true;
        HackSelected?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerHackAction)}: 해킹 행동을 선택했습니다. 해킹 가능 거리: {HackRange}", this);
        }
    }

    /// <summary>
    /// 현재 해킹 행동 선택을 취소한다.
    /// </summary>
    public void CancelHackAction()
    {
        if (!isHackSelected)
        {
            return;
        }

        isHackSelected = false;
        HackCanceled?.Invoke();

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerHackAction)}: 해킹 행동 선택을 취소했습니다.", this);
        }
    }

    /// <summary>
    /// 선택된 해킹 대상에 해킹 행동을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteHack(HackableObject target, ActionResolutionContext resolutionContext)
    {
        if (!isHackSelected)
        {
            return false;
        }

        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 해킹 행동을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (!CanSelectHackAction())
        {
            return false;
        }

        if (!TryValidateTarget(target, out GridPosition targetPosition))
        {
            return false;
        }

        if (!TryFindExecutionPosition(targetPosition, out GridPosition executionPosition))
        {
            LogBlockedTarget(target, "해킹 연출을 실행할 대상 주변 8칸을 찾지 못했습니다");
            return false;
        }

        int cost = playerContext.UnitData.HackActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.TrySpend(cost))
        {
            LogBlockedTarget(target, "AP가 부족합니다");
            return false;
        }

        bool usesSword = playerContext.HasAbility(UnitAbilityType.Sword) && playerContext.SwordState != null;
        GridPosition swordFromPosition = usesSword
            ? playerContext.SwordState.CurrentPosition
            : playerContext.GridActor.GridPosition;

        if (usesSword)
        {
            // 시야 갱신 핸들러가 해킹 완료 이벤트를 받을 때 새 검 위치를 읽을 수 있도록 논리 상태를 먼저 확정한다.
            playerContext.SwordState.SetDeployedPosition(executionPosition);
        }

        target.OnHackStarted();
        target.OnHackCompleted();

        isHackSelected = false;
        HackCanceled?.Invoke();

        resolutionContext.Publish(new HackCompletedLogicEvent(playerContext.GridActor, target, targetPosition, executionPosition));
        if (usesSword)
        {
            // 검 능력 유닛은 해킹 연출 전에 검 Visual을 실제 실행 칸으로 이동시킨다.
            resolutionContext.EnqueuePresentation(PresentationEvent.SwordMove(
                playerContext.GridActor,
                swordFromPosition,
                executionPosition,
                SwordMoveKind.Hack,
                "해킹 검 이동 연출"));
        }

        resolutionContext.EnqueuePresentation(PresentationEvent.Hack(playerContext.GridActor, target, targetPosition, executionPosition, "해킹 연출"));

        if (logActionState)
        {
            Debug.Log($"{nameof(PlayerHackAction)}: {target.name} 대상을 해킹했습니다. 대상 칸: {targetPosition}, 실행 칸: {executionPosition}", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 턴과 AP 상태 기준으로 해킹 행동을 선택할 수 있는지 확인한다.
    /// </summary>
    private bool CanSelectHackAction()
    {
        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (GridManager.Instance == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 해킹 거리를 계산할 {nameof(GridManager)}가 필요합니다.", this);
            }

            return false;
        }

        if (HackableRegistry.Instance == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 해킹 대상을 찾을 {nameof(HackableRegistry)}가 필요합니다.", this);
            }

            return false;
        }

        ActionPresentationQueue presentationQueue = ActionPresentationQueue.Instance;
        if (presentationQueue == null)
        {
            if (logBlockedTarget)
            {
                Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 해킹 연출을 실행할 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            }

            return false;
        }

        if (presentationQueue.IsPlaying)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerHackAction)}: 연출 큐가 실행 중이라 해킹 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (requirePlayerTurn && turnManager != null && !turnManager.IsPlayerTurn)
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerHackAction)}: 현재 턴이 {turnManager.CurrentSide}라서 해킹 행동을 선택할 수 없습니다.", this);
            }

            return false;
        }

        int cost = playerContext.UnitData.HackActionPointCost;
        ActionPoint actionPoint = playerContext.ActionPoint;
        if (cost > 0 && !actionPoint.CanSpend(cost))
        {
            if (logBlockedTarget)
            {
                Debug.Log($"{nameof(PlayerHackAction)}: AP가 부족해서 해킹 행동을 선택할 수 없습니다. 필요 AP: {cost}, 현재 AP: {actionPoint.Current}", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 해킹 대상이 실행 가능한 대상인지 검사하고 대상 위치를 반환한다.
    /// </summary>
    private bool TryValidateTarget(HackableObject target, out GridPosition targetPosition)
    {
        targetPosition = GridPosition.Zero;

        if (target == null)
        {
            LogBlockedTarget(null, "해킹 대상이 없습니다");
            return false;
        }

        if (!target.enabled || !target.HasValidReference() || !target.HasValidData())
        {
            LogBlockedTarget(target, "해킹 대상 참조나 데이터가 유효하지 않습니다");
            return false;
        }

        if (target.IsHacked)
        {
            LogBlockedTarget(target, "이미 해킹된 대상입니다");
            return false;
        }

        targetPosition = target.GridPosition;
        if (PlayerVisionManager.Instance != null && !PlayerVisionManager.Instance.IsVisible(targetPosition))
        {
            LogBlockedTarget(target, "현재 플레이어 시야 밖입니다");
            return false;
        }

        GridPosition hackOriginPosition = playerContext.HasAbility(UnitAbilityType.Sword) && playerContext.SwordState != null
            ? playerContext.SwordState.CurrentPosition
            : playerContext.GridActor.GridPosition;
        int distance = hackOriginPosition.ManhattanDistanceTo(targetPosition);
        if (distance > HackRange)
        {
            LogBlockedTarget(target, $"해킹 가능 거리 밖입니다. 거리: {distance}, 최대 거리: {HackRange}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 해킹 대상 주변 8칸 중 나중에 검이 도착할 실행 칸을 찾는다.
    /// </summary>
    private bool TryFindExecutionPosition(GridPosition targetPosition, out GridPosition executionPosition)
    {
        executionPositionCandidates.Clear();

        GridManager gridManager = GridManager.Instance;
        for (int i = 0; i < AdjacentEightDirections.Length; i++)
        {
            GridPosition candidate = targetPosition + AdjacentEightDirections[i];
            if (gridManager.IsInside(candidate) && gridManager.CanEnter(candidate))
            {
                executionPositionCandidates.Add(candidate);
            }
        }

        if (executionPositionCandidates.Count <= 0)
        {
            executionPosition = GridPosition.Zero;
            return false;
        }

        GridPosition playerPosition = playerContext.GridActor.GridPosition;
        executionPositionCandidates.Sort((left, right) =>
            left.ManhattanDistanceTo(playerPosition).CompareTo(right.ManhattanDistanceTo(playerPosition)));

        executionPosition = executionPositionCandidates[0];
        return true;
    }

    /// <summary>
    /// 해킹이 막힌 대상과 사유를 로그로 남긴다.
    /// </summary>
    private void LogBlockedTarget(HackableObject target, string reason)
    {
        if (!logBlockedTarget)
        {
            return;
        }

        string targetName = target != null ? target.name : "없음";
        Debug.Log($"{nameof(PlayerHackAction)}: {targetName} 대상을 해킹할 수 없습니다. 사유: {reason}", this);
    }

    /// <summary>
    /// 해킹 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (playerContext.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.ActionPoint == null)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.HasAbility(UnitAbilityType.Sword) && playerContext.SwordState == null)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}의 검 능력 기반 해킹에는 {nameof(PlayerSwordState)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 해킹 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        ControllableUnitData unitData = playerContext.UnitData;
        if (unitData == null)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}에는 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        if (unitData.HackRange < 0)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}의 {nameof(ControllableUnitData)} 해킹 가능 거리는 0 이상이어야 합니다.", this);
            return false;
        }

        if (unitData.HackActionPointCost <= 0)
        {
            Debug.LogError($"{nameof(PlayerHackAction)} on {name}의 {nameof(ControllableUnitData)} 해킹 AP 비용은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
