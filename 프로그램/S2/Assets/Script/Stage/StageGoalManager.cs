using System;
using UnityEngine;

/// <summary>
/// 플레이어 이동 완료를 감시해 목표 칸 도달을 알린다.
/// 스테이지 클리어 확정은 StageStateManager가 처리한다.
/// </summary>
public class StageGoalManager : MonoBehaviour
{
    [Header("Reference")]
    // 플레이어 이동 완료 이벤트를 제공하는 플레이어 Context다.
    [SerializeField] private PlayerContext playerContext;
    // 이번 스테이지의 목표 칸 정보다.
    [SerializeField] private StageGoal stageGoal;

    [Header("Log")]
    // true면 목표 도달 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logStageClear = true;

    // 이미 클리어 처리를 완료했는지 나타낸다.
    private bool isCleared;
    // 이동 완료 이벤트를 현재 구독 중인지 나타낸다.
    private bool subscribedMoveCompleted;

    public bool IsCleared => isCleared;

    // 스테이지 목표가 달성됐을 때 목표 컴포넌트를 전달한다.
    public event Action<StageGoal> StageCleared;

    /// <summary>
    /// 목표 판정에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 플레이어 이동 완료 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeMoveCompleted();
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 이벤트 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeMoveCompleted();
        CheckCurrentPlayerPosition();
    }

    /// <summary>
    /// 플레이어 이동 완료 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (playerContext != null && playerContext.GridMoveAction != null)
        {
            playerContext.GridMoveAction.MoveCompleted -= HandleMoveCompleted;
        }

        subscribedMoveCompleted = false;
    }

    /// <summary>
    /// 현재 플레이어가 목표 칸에 있는지 즉시 확인한다.
    /// </summary>
    public void CheckCurrentPlayerPosition()
    {
        if (!HasValidReference() || isCleared)
        {
            return;
        }

        TryCompleteStage(playerContext.GridActor.GridPosition);
    }

    /// <summary>
    /// 플레이어 이동 완료 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeMoveCompleted()
    {
        if (subscribedMoveCompleted)
        {
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        playerContext.GridMoveAction.MoveCompleted += HandleMoveCompleted;
        subscribedMoveCompleted = true;
    }

    /// <summary>
    /// 플레이어 이동 완료 후 도착 칸이 목표 칸인지 검사한다.
    /// </summary>
    private void HandleMoveCompleted(GridPosition completedPosition)
    {
        TryCompleteStage(completedPosition);
    }

    /// <summary>
    /// 지정한 칸이 목표 칸이면 스테이지 클리어 이벤트를 발생시킨다.
    /// </summary>
    private void TryCompleteStage(GridPosition playerPosition)
    {
        if (isCleared || !stageGoal.IsGoalPosition(playerPosition))
        {
            return;
        }

        isCleared = true;

        if (logStageClear)
        {
            Debug.Log($"{nameof(StageGoalManager)}: 플레이어가 {playerPosition} 목표 칸에 도착했습니다.", this);
        }

        StageCleared?.Invoke(stageGoal);
    }

    /// <summary>
    /// 목표 판정에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(StageGoalManager)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (stageGoal == null)
        {
            Debug.LogError($"{nameof(StageGoalManager)} on {name}에는 {nameof(StageGoal)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
