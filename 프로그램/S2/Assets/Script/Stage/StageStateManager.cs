using System;
using UnityEngine;

/// <summary>
/// 스테이지의 현재 진행 상태를 보관하고 클리어/실패 전환을 알린다.
/// 개별 목표 판정은 StageGoalManager가 맡고, 이 컴포넌트는 전체 스테이지 상태만 관리한다.
/// </summary>
public class StageStateManager : MonoBehaviour
{
    [Header("Reference")]
    // 목표 달성 이벤트를 제공하는 스테이지 목표 매니저다.
    [SerializeField] private StageGoalManager stageGoalManager;

    [Header("State")]
    // 현재 스테이지 진행 상태다.
    [SerializeField] private StageState currentState = StageState.Playing;

    [Header("Log")]
    // true면 스테이지 상태 변경 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logStateChanges = true;

    // 목표 달성 이벤트를 현재 구독 중인지 나타낸다.
    private bool subscribedGoalManager;

    public StageState CurrentState => currentState;
    public bool IsPlaying => currentState == StageState.Playing;
    public bool IsCleared => currentState == StageState.Cleared;
    public bool IsFailed => currentState == StageState.Failed;

    // 스테이지 상태가 바뀔 때 이전 상태와 새 상태를 전달한다.
    public event Action<StageState, StageState> StageStateChanged;

    /// <summary>
    /// 스테이지 상태 관리에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 목표 달성 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeGoalManager();
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 이벤트 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeGoalManager();
    }

    /// <summary>
    /// 목표 달성 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (stageGoalManager != null)
        {
            stageGoalManager.StageCleared -= HandleStageCleared;
        }

        subscribedGoalManager = false;
    }

    /// <summary>
    /// 외부 시스템에서 스테이지 실패 처리를 요청한다.
    /// </summary>
    public bool RequestFail()
    {
        return TrySetState(StageState.Failed);
    }

    /// <summary>
    /// 외부 시스템에서 스테이지 클리어 처리를 요청한다.
    /// </summary>
    public bool RequestClear()
    {
        return TrySetState(StageState.Cleared);
    }

    /// <summary>
    /// 목표 달성 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeGoalManager()
    {
        if (subscribedGoalManager)
        {
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        stageGoalManager.StageCleared += HandleStageCleared;
        subscribedGoalManager = true;
    }

    /// <summary>
    /// 목표 달성 이벤트를 받아 스테이지를 클리어 상태로 전환한다.
    /// </summary>
    private void HandleStageCleared(StageGoal _)
    {
        RequestClear();
    }

    /// <summary>
    /// 현재 스테이지 상태를 새 상태로 바꾸고 변경 이벤트를 알린다.
    /// </summary>
    private bool TrySetState(StageState nextState)
    {
        if (currentState == nextState)
        {
            return false;
        }

        if (currentState != StageState.Playing)
        {
            if (logStateChanges)
            {
                Debug.LogWarning($"{nameof(StageStateManager)}: 이미 {currentState} 상태라서 {nextState} 상태로 바꿀 수 없습니다.", this);
            }

            return false;
        }

        StageState previousState = currentState;
        currentState = nextState;

        if (logStateChanges)
        {
            Debug.Log($"{nameof(StageStateManager)}: 스테이지 상태가 {previousState}에서 {currentState}로 변경됐습니다.", this);
        }

        StageStateChanged?.Invoke(previousState, currentState);
        return true;
    }

    /// <summary>
    /// 스테이지 상태 관리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (stageGoalManager == null)
        {
            Debug.LogError($"{nameof(StageStateManager)} on {name}에는 {nameof(StageGoalManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}

/// <summary>
/// 스테이지의 현재 진행 상태다.
/// </summary>
public enum StageState
{
    Playing,
    Cleared,
    Failed,
}
