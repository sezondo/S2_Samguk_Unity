using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 적 하나의 평상·의심·발각 상태와 현재 의심 정보를 보관한다.
/// </summary>
public class EnemyAlertState : MonoBehaviour
{
    [Header("Reference")]
    // 이 상태 컴포넌트가 속한 적의 핵심 참조 주머니다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("State")]
    // 현재 적의 전장 인식 상태다.
    [FormerlySerializedAs("currentLevel")]
    [SerializeField] private EnemyAwarenessState currentState = EnemyAwarenessState.Unaware;

    // 현재 의심 행동 단계다.
    private SuspiciousBehaviorPhase suspiciousPhase = SuspiciousBehaviorPhase.MovingToInvestigationPosition;
    // 현재 조사 중인 이상 현상 정보다.
    private EnemySuspicionInfo suspicionInfo;
    // 새 이상 현상 없이 조사를 계속할 남은 적 턴 수다.
    private int remainingSuspicionTurns;

    [Header("Log")]
    // true면 상태 변경 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logStateChanges = true;

    public EnemyAwarenessState CurrentState => currentState;
    public bool IsUnaware => currentState == EnemyAwarenessState.Unaware;
    public bool IsSuspicious => currentState == EnemyAwarenessState.Suspicious;
    public bool IsAlerted => currentState == EnemyAwarenessState.Alerted;
    public SuspiciousBehaviorPhase SuspiciousPhase => suspiciousPhase;
    public EnemySuspicionInfo SuspicionInfo => suspicionInfo;
    public int RemainingSuspicionTurns => remainingSuspicionTurns;

    // 경계 상태가 바뀔 때 이전 상태와 새 상태를 전달한다.
    public event Action<EnemyAwarenessState, EnemyAwarenessState> AwarenessStateChanged;
    // 조사 단계 변경을 논리와 별도로 순차 표시할 수 있도록 알린다.
    public event Action SuspiciousPhaseChanged;

    /// <summary>
    /// 상태 관리에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 외부 시스템에서 이 적에게 발각 상태 전환을 요청한다.
    /// </summary>
    public bool RequestAlert(GridPosition detectedPosition, EnemyContext sourceEnemy)
    {
        return TrySetState(EnemyAwarenessState.Alerted, detectedPosition, sourceEnemy);
    }

    /// <summary>
    /// 지정한 이상 현상을 현재 조사 대상으로 기록하고 의심 유지 시간을 초기화한다.
    /// </summary>
    public bool RequestSuspicion(EnemySuspicionInfo info)
    {
        if (IsAlerted || !HasValidData())
        {
            return false;
        }

        if (IsSuspicious)
        {
            // 즉시 반응은 반복하지 않지만 이후 조사와 복귀 로그는 가장 최근 이상 현상을 기준으로 삼는다.
            suspicionInfo = info;
            remainingSuspicionTurns = enemyContext.EnemyData.SuspicionDurationTurns;

            // 복귀 도중 새 이상 현상을 감지하면 복귀만 중단하고 즉시 반응 연출은 다시 실행하지 않는다.
            if (suspiciousPhase == SuspiciousBehaviorPhase.ReturningToRoutine)
            {
                SetSuspiciousPhase(SuspiciousBehaviorPhase.Searching);
            }

            return true;
        }

        EnemyAwarenessState previousState = currentState;
        currentState = EnemyAwarenessState.Suspicious;
        suspicionInfo = info;
        suspiciousPhase = SuspiciousBehaviorPhase.MovingToInvestigationPosition;
        remainingSuspicionTurns = enemyContext.EnemyData.SuspicionDurationTurns;

        AwarenessStateChanged?.Invoke(previousState, currentState);

        return true;
    }

    /// <summary>
    /// 의심 행동의 현재 내부 단계를 변경한다.
    /// </summary>
    public void SetSuspiciousPhase(SuspiciousBehaviorPhase nextPhase)
    {
        if (IsSuspicious && suspiciousPhase != nextPhase)
        {
            suspiciousPhase = nextPhase;
            SuspiciousPhaseChanged?.Invoke();
        }
    }

    /// <summary>
    /// 조사 적 턴을 하나 소비하고 남은 턴이 있는지 반환한다.
    /// </summary>
    public bool ConsumeSuspicionTurn()
    {
        if (!IsSuspicious || remainingSuspicionTurns <= 0)
        {
            return false;
        }

        remainingSuspicionTurns--;
        return remainingSuspicionTurns > 0;
    }

    /// <summary>
    /// 조사와 복귀가 끝난 적을 평상 상태로 되돌린다.
    /// </summary>
    public bool RequestUnaware()
    {
        if (!IsSuspicious)
        {
            return false;
        }

        return TrySetState(EnemyAwarenessState.Unaware, suspicionInfo.Position, suspicionInfo.Detector);
    }

    /// <summary>
    /// 현재 경계 상태를 새 상태로 바꾸고 변경 이벤트를 알린다.
    /// </summary>
    private bool TrySetState(EnemyAwarenessState nextState, GridPosition detectedPosition, EnemyContext sourceEnemy)
    {
        if (currentState == nextState)
        {
            return false;
        }

        EnemyAwarenessState previousState = currentState;
        currentState = nextState;
        if (nextState != EnemyAwarenessState.Suspicious)
        {
            remainingSuspicionTurns = 0;
        }

        if (logStateChanges)
        {
            string sourceName = sourceEnemy != null ? sourceEnemy.name : "알 수 없는 적";
            Debug.Log($"{nameof(EnemyAlertState)}: {enemyContext.name} 적 상태가 {previousState}에서 {currentState}로 변경됐습니다. 사건 칸: {detectedPosition}, 전파 출처: {sourceName}", this);
        }

        AwarenessStateChanged?.Invoke(previousState, currentState);
        return true;
    }

    /// <summary>
    /// 의심 유지 시간에 필요한 적 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (enemyContext == null || enemyContext.EnemyData == null)
        {
            Debug.LogError($"{nameof(EnemyAlertState)} on {name}에는 의심 데이터를 제공할 {nameof(EnemyData)}가 필요합니다.", this);
            return false;
        }

        if (enemyContext.EnemyData.SuspicionDurationTurns <= 0)
        {
            Debug.LogError($"{nameof(EnemyAlertState)} on {name}의 의심 유지 턴은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 상태 관리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyAlertState)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.HasValidReference())
        {
            return false;
        }

        return true;
    }
}
