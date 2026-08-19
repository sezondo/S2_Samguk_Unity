using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경계 상태로 전환된 적의 1차 반응 행동을 처리한다.
/// 현재는 이동 가능 범위 안의 벽 인접 칸으로 이동하는 엄폐 반응만 담당한다.
/// </summary>
public class EnemyAlertReactionCoordinator : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Reference")]
    // 이 컴포넌트가 경계 반응 이동을 처리할 적 Context다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("Score")]
    // 경계 반응 엄폐 후보를 평가할 때 사용하는 점수 가중치다.
    [SerializeField] private EnemyTacticalPositionScoreSettings scoreSettings = EnemyTacticalPositionScoreSettings.CreateDefaultCoverReactionSettings();

    [Header("Log")]
    // true면 엄폐 후보 선택과 이동 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logReaction = true;
    // true면 엄폐 후보별 세부 점수 내역을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logCandidateScores = true;

    // 경계 반응 이동 경로 계산기다.
    private readonly EnemyTacticalMovePlanner movePlanner = new();
    // 경계 반응 시 가장 가까운 살아 있는 플레이어 진영 유닛을 찾는다.
    private readonly EnemyTargetSelector targetSelector = new();
    // 최종 목표까지의 이동 경로 버퍼다.
    private readonly List<GridPosition> pathBuffer = new();
    // 후보별 점수 로그 출력용 버퍼다.
    private readonly List<EnemyTacticalPositionScoreResult> scoreResults = new();
    // 이 적이 현재 Alerted 진입에 대한 수동 반응 이동을 이미 처리했는지 나타낸다.
    private bool hasReactedToAlert;

    /// <summary>
    /// 경계 반응에 필요한 참조와 점수 설정을 확인한다.
    /// </summary>
    private void Awake()
    {
        EnsureScoreSettingsInitialized();

        if (!HasValidReference() || !HasValidScoreSettings())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 활성화될 때 논리 이벤트 핸들러로 등록한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 논리 이벤트 핸들러 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
        hasReactedToAlert = false;
    }

    /// <summary>
    /// 지정한 논리 이벤트를 이 컴포넌트가 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return enemyContext != null &&
            logicEvent is EnemyAlertedLogicEvent alerted &&
            alerted.Enemy == enemyContext;
    }

    /// <summary>
    /// 경계 전환 이벤트를 받아 엄폐 이동 반응을 처리한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not EnemyAlertedLogicEvent alerted)
        {
            return;
        }

        HandleEnemyAlerted(alerted, context);
    }

    /// <summary>
    /// 경계 상태가 된 적의 이동 가능 엄폐 후보를 계산하고 논리 이동과 연출 이벤트를 만든다.
    /// </summary>
    private void HandleEnemyAlerted(EnemyAlertedLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent.Enemy != enemyContext)
        {
            return;
        }

        if (!CanReact() || !HasValidData())
        {
            return;
        }

        if (hasReactedToAlert)
        {
            if (logReaction)
            {
                Debug.Log($"{nameof(EnemyAlertReactionCoordinator)}: {enemyContext.name} 적은 이미 현재 경계 상태 진입에 반응했으므로 추가 엄폐 이동을 생략합니다.", this);
            }

            return;
        }

        hasReactedToAlert = true;
        int moveRange = enemyContext.EnemyData.AlertReactionMoveRange;
        if (moveRange <= 0)
        {
            return;
        }

        GridPosition startPosition = enemyContext.GridActor.GridPosition;
        GridPosition reactionTargetPosition = logicEvent.KnownPlayerPosition;
        if (targetSelector.TrySelectNearestPlayerUnit(startPosition, out TacticalUnitContext nearestPlayerUnit))
        {
            reactionTargetPosition = nearestPlayerUnit.GridActor.GridPosition;
        }

        if (!movePlanner.TryFindBestCoverReactionPath(
                GridManager.Instance,
                startPosition,
                reactionTargetPosition,
                moveRange,
                scoreSettings,
                pathBuffer,
                logCandidateScores ? scoreResults : null,
                out GridPosition targetPosition))
        {
            if (logReaction)
            {
                Debug.Log($"{nameof(EnemyAlertReactionCoordinator)}: {enemyContext.name} 적이 이동 가능한 엄폐 칸을 찾지 못했습니다.", this);
            }

            return;
        }

        if (logCandidateScores)
        {
            LogCandidateScores();
        }

        MoveEnemyAlongPath(pathBuffer, context);

        if (logReaction)
        {
            Debug.Log($"{nameof(EnemyAlertReactionCoordinator)}: {enemyContext.name} 적이 경계 반응으로 {startPosition} 칸에서 {targetPosition} 칸까지 엄폐 이동합니다. 원인: {logicEvent.Reason}", this);
        }
    }

    /// <summary>
    /// 계산된 경로를 따라 적 논리 위치를 이동시키고 1칸 단위 연출 이벤트를 추가한다.
    /// </summary>
    private void MoveEnemyAlongPath(IReadOnlyList<GridPosition> path, ActionResolutionContext context)
    {
        EnemyMovementUtility.MoveAlongPath(enemyContext, path, context, "적 경계 엄폐 이동 연출");
    }

    /// <summary>
    /// 후보 칸별 엄폐 점수 내역을 로그로 출력한다.
    /// </summary>
    private void LogCandidateScores()
    {
        for (int i = 0; i < scoreResults.Count; i++)
        {
            Debug.Log($"{nameof(EnemyAlertReactionCoordinator)}: {enemyContext.name} 엄폐 후보 {scoreResults[i]}", this);
        }
    }

    /// <summary>
    /// 이 적이 경계 반응 이동을 처리할 수 있는지 확인한다.
    /// </summary>
    private bool CanReact()
    {
        if (!HasValidReference())
        {
            return false;
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyAlertReactionCoordinator)} on {name}에는 엄폐 이동 계산에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 경계 반응 이동에 필요한 적 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData()
    {
        if (enemyContext.EnemyData.AlertReactionMoveRange < 0)
        {
            Debug.LogError($"{nameof(EnemyAlertReactionCoordinator)}: {enemyContext.name}의 경계 반응 이동 거리는 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 경계 반응의 엄폐 점수와 각도 설정이 올바른지 확인한다.
    /// </summary>
    private bool HasValidScoreSettings()
    {
        if (scoreSettings.HasValidData())
        {
            return true;
        }

        Debug.LogError($"{nameof(EnemyAlertReactionCoordinator)} on {name}의 엄폐 점수 또는 각도 설정이 올바르지 않습니다.", this);
        return false;
    }

    /// <summary>
    /// 경계 반응에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyAlertReactionCoordinator)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.enabled || !enemyContext.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 기존 씬 컴포넌트에 새 점수 설정이 비어 있으면 기본값으로 보정한다.
    /// </summary>
    private void EnsureScoreSettingsInitialized()
    {
        if (scoreSettings.IsInitialized)
        {
            return;
        }

        scoreSettings = EnemyTacticalPositionScoreSettings.CreateDefaultCoverReactionSettings();
    }
}
