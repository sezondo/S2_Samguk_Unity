using System;
using UnityEngine;

/// <summary>
/// 턴마다 지급되는 행동 포인트를 관리한다.
/// AP 수치 정책만 담당하고, 어떤 행동이 AP를 쓰는지는 호출자가 결정한다.
/// </summary>
public class ActionPoint : MonoBehaviour
{
    [Header("AP")]
    // 이 오브젝트가 가질 수 있는 최대 AP다.
    [SerializeField] private int maxActionPoint = 3;
    // 턴 시작 시 보충할 AP 양이다. 최대 AP를 넘으면 최대 AP로 제한된다.
    [SerializeField] private int startTurnActionPoint = 3;
    // true면 플레이어 턴이 시작될 때 AP를 자동 보충한다.
    [SerializeField] private bool refillOnPlayerTurnStart = true;
    // AP가 바뀔 때 Unity 콘솔에 로그를 남길지 정한다.
    [SerializeField] private bool logActionPointChanges = true;

    // 현재 남아 있는 AP다.
    public int Current { get; private set; }
    // 음수 설정을 막은 실제 최대 AP 값이다.
    public int Max => Mathf.Max(0, maxActionPoint);

    // AP가 바뀔 때 현재 AP와 최대 AP를 알려준다.
    public event Action<int, int> ActionPointChanged;

    // 이벤트 중복 구독을 막기 위해 현재 구독 중인 턴 매니저를 보관한다.
    private TurnManager subscribedTurnManager;

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
        int clampedMax = Mathf.Max(0, maxActionPoint);
        int refillAmount = Mathf.Clamp(startTurnActionPoint, 0, clampedMax);
        SetCurrent(refillAmount);
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
        int clampedMax = Mathf.Max(0, maxActionPoint);
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
}
