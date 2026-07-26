using System;
using UnityEngine;

/// <summary>
/// 적 하나가 자기 턴 행동에 사용할 AP를 관리한다.
/// 적 턴에서 어떤 행동이 AP를 쓰는지는 호출자가 결정하고, 이 컴포넌트는 보유 AP와 소비 처리만 담당한다.
/// </summary>
public class EnemyActionPoint : MonoBehaviour
{
    [Header("Reference")]
    // 적 공통 참조와 턴 데이터를 제공하는 필수 Context다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("Log")]
    // AP가 바뀔 때 Unity 콘솔에 로그를 남길지 정한다.
    [SerializeField] private bool logActionPointChanges = true;

    // 현재 남아 있는 적 AP다.
    public int Current { get; private set; }
    // 적 데이터에 설정된 턴 시작 AP다.
    public int Max => enemyContext.EnemyData.TurnActionPoint;

    // AP가 바뀔 때 현재 AP와 최대 AP를 알려준다.
    public event Action<int, int> ActionPointChanged;

    /// <summary>
    /// 적 AP 관리에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
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
    /// 적 턴 시작 기준 AP 양으로 현재 AP를 보충한다.
    /// </summary>
    public void RefillForTurn()
    {
        if (!HasValidReference() || !HasValidData())
        {
            return;
        }

        SetCurrent(enemyContext.EnemyData.TurnActionPoint);
    }

    /// <summary>
    /// 현재 AP를 유효 범위로 보정해 저장하고 변경 이벤트를 발생시킨다.
    /// </summary>
    private void SetCurrent(int value)
    {
        int clampedMax = enemyContext.EnemyData.TurnActionPoint;
        int nextValue = Mathf.Clamp(value, 0, clampedMax);
        if (Current == nextValue)
        {
            return;
        }

        Current = nextValue;
        if (logActionPointChanges)
        {
            Debug.Log($"{nameof(EnemyActionPoint)}: {name} 오브젝트 AP {Current}/{Max}", this);
        }

        ActionPointChanged?.Invoke(Current, Max);
    }

    /// <summary>
    /// 적 AP 관리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyActionPoint)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.enabled || !enemyContext.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 적 AP 관리에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        EnemyData enemyData = enemyContext.EnemyData;
        if (enemyData.TurnActionPoint < 0)
        {
            Debug.LogError($"{nameof(EnemyActionPoint)} on {name}의 적 턴 AP는 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
