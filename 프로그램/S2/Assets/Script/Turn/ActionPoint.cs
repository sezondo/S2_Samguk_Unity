using System;
using UnityEngine;

/// <summary>
/// 턴마다 지급되는 행동 포인트를 관리한다.
/// AP 수치 정책만 담당하고, 어떤 행동이 AP를 쓰는지는 호출자가 결정한다.
/// </summary>
public class ActionPoint : MonoBehaviour
{
    [Header("AP")]
    // 플레이어 공통 참조와 턴 데이터를 제공하는 필수 Context다.
    [SerializeField] private TacticalUnitContext playerContext;
    // true면 플레이어 턴이 시작될 때 AP를 자동 보충한다.
    [SerializeField] private bool refillOnPlayerTurnStart = true;
    // AP가 바뀔 때 Unity 콘솔에 로그를 남길지 정한다.
    [SerializeField] private bool logActionPointChanges = true;

    // 현재 남아 있는 AP다.
    public int Current { get; private set; }
    // 음수 설정을 막은 실제 최대 AP 값이다.
    public int Max => playerContext.UnitData.MaxActionPoint;

    // AP가 바뀔 때 현재 AP와 최대 AP를 알려준다.
    public event Action<int, int> ActionPointChanged;

    // 이벤트 중복 구독을 막기 위해 현재 구독 중인 턴 매니저를 보관한다.
    private TurnManager subscribedTurnManager;

    /// <summary>
    /// 전술 유닛 Context와 AP 데이터를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 오브젝트가 활성화될 때 턴 시작 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeTurnManager();
    }

    /// <summary>
    /// 시작 시점에 턴 매니저 구독을 보정하고 필요하면 초기 AP를 보충한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeTurnManager();

        if (refillOnPlayerTurnStart && (TurnManager.Instance == null || TurnManager.Instance.IsPlayerTurn))
        {
            RefillForTurn();
        }
    }

    /// <summary>
    /// 오브젝트가 비활성화될 때 턴 시작 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (subscribedTurnManager != null)
        {
            subscribedTurnManager.TurnStarted -= HandleTurnStarted;
            subscribedTurnManager = null;
        }
    }

    /// <summary>
    /// 지정한 비용만큼 AP를 소비할 수 있는지 확인한다.
    /// </summary>
    public bool CanSpend(int cost)
    {
        return cost >= 0 && Current >= cost;
    }

    /// <summary>
    /// 지정한 비용만큼 AP 소비를 시도한다.
    /// </summary>
    public bool TrySpend(int cost)
    {
        if (!CanSpend(cost))
        {
            return false;
        }

        SetCurrent(Current - cost);
        return true;
    }

    /// <summary>
    /// 턴 시작 기준 AP 양으로 현재 AP를 보충한다.
    /// </summary>
    public void RefillForTurn()
    {
        SetCurrent(playerContext.UnitData.StartTurnActionPoint);
    }

    /// <summary>
    /// 턴 시작 이벤트를 받아 플레이어 턴이면 AP를 보충한다.
    /// </summary>
    private void HandleTurnStarted(TurnSide side)
    {
        if (refillOnPlayerTurnStart && side == TurnSide.Player)
        {
            RefillForTurn();
        }
    }

    /// <summary>
    /// 현재 씬의 TurnManager에 턴 시작 이벤트를 한 번만 구독한다.
    /// </summary>
    private void TrySubscribeTurnManager()
    {
        if (subscribedTurnManager != null)
        {
            return;
        }

        TurnManager turnManager = TurnManager.Instance;
        if (turnManager == null)
        {
            return;
        }

        subscribedTurnManager = turnManager;
        subscribedTurnManager.TurnStarted += HandleTurnStarted;
    }

    /// <summary>
    /// 현재 AP를 유효 범위로 보정해 저장하고 변경 이벤트를 발생시킨다.
    /// </summary>
    private void SetCurrent(int value)
    {
        int clampedMax = playerContext.UnitData.MaxActionPoint;
        int nextValue = Mathf.Clamp(value, 0, clampedMax);
        if (Current == nextValue)
        {
            return;
        }

        Current = nextValue;
        if (logActionPointChanges)
        {
            Debug.Log($"{nameof(ActionPoint)}: {name} 오브젝트 AP {Current}/{Max}", this);
        }

        ActionPointChanged?.Invoke(Current, Max);
    }
    /// <summary>
    /// AP 관리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(ActionPoint)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// AP 관리에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        ControllableUnitData unitData = playerContext.UnitData;
        if (unitData == null)
        {
            Debug.LogError($"{nameof(ActionPoint)} on {name}에는 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        if (unitData.MaxActionPoint <= 0)
        {
            Debug.LogError($"{nameof(ActionPoint)} on {name}의 {nameof(ControllableUnitData)} 최대 AP는 0보다 커야 합니다.", this);
            return false;
        }

        if (unitData.StartTurnActionPoint < 0)
        {
            Debug.LogError($"{nameof(ActionPoint)} on {name}의 {nameof(ControllableUnitData)} 턴 시작 AP는 0 이상이어야 합니다.", this);
            return false;
        }

        if (unitData.StartTurnActionPoint > unitData.MaxActionPoint)
        {
            Debug.LogError($"{nameof(ActionPoint)} on {name}의 {nameof(ControllableUnitData)} 턴 시작 AP는 최대 AP보다 클 수 없습니다.", this);
            return false;
        }

        return true;
    }
}
