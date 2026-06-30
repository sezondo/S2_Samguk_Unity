using System;
using UnityEngine;

/// <summary>
/// 플레이어 이동 완료를 감시해 목표 칸 도달을 알린다.
/// 스테이지 클리어 확정은 StageStateManager가 처리한다.
/// </summary>
public class StageGoalManager : MonoBehaviour, IActionLogicEventHandler
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
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 이벤트 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        CheckCurrentPlayerPosition();
    }

    /// <summary>
    /// 논리 이벤트 핸들러 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
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

        TryCompleteStage(playerContext.GridActor.GridPosition, null);
    }

    /// <summary>
    /// 플레이어 이동 완료 후 도착 칸이 목표 칸인지 검사한다.
    /// </summary>
    private void HandleMoveCompleted(MoveCompletedLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent.Actor != playerContext.GridActor)
        {
            return;
        }

        TryCompleteStage(logicEvent.CompletedPosition, context);
    }

    /// <summary>
    /// 지정한 논리 이벤트를 이 컴포넌트가 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        GridActor playerActor = playerContext != null ? playerContext.GridActor : null;
        return playerActor != null &&
            logicEvent is MoveCompletedLogicEvent moveCompleted &&
            moveCompleted.Actor == playerActor;
    }

    /// <summary>
    /// 이동 완료 논리 이벤트를 받아 목표 도착 여부를 판정한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is MoveCompletedLogicEvent moveCompleted)
        {
            HandleMoveCompleted(moveCompleted, context);
        }
    }

    /// <summary>
    /// 지정한 칸이 목표 칸이면 스테이지 클리어 이벤트를 발생시킨다.
    /// </summary>
    private void TryCompleteStage(GridPosition playerPosition, ActionResolutionContext context)
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
        context?.Publish(new StageClearedLogicEvent(stageGoal));
        context?.EnqueuePresentation(PresentationEvent.StageCleared("스테이지 클리어 연출"));
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
