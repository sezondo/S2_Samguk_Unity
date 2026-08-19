using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 하나의 턴 행동을 결정하고 실행한다.
/// 적 인식 상태에 따라 평상 루틴, 의심 조사 또는 기존 전투 행동을 실행한다.
/// </summary>
public class EnemyTurnAgent : MonoBehaviour
{
    // 적 턴 이동 행동 1회가 소비하는 AP 비용이다.
    private const int MoveActionPointCost = 1;
    // 적 원거리 공격 행동 1회가 소비하는 AP 비용이다.
    private const int AttackActionPointCost = 1;

    [Header("Reference")]
    // 이 턴 행동을 수행할 적 Context다.
    [SerializeField] private EnemyContext enemyContext;

    [Header("Score")]
    // 적 턴 중 이동 후보를 평가할 때 사용하는 엄폐 점수 가중치다.
    [SerializeField] private EnemyTacticalPositionScoreSettings scoreSettings = EnemyTacticalPositionScoreSettings.CreateDefaultCoverReactionSettings();

    [Header("Log")]
    // true면 적 턴 행동 선택과 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logTurnAction = true;
    // true면 적 턴 이동 후보별 점수 내역을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logCandidateScores;

    // 적 턴 이동 경로 계산기다.
    private readonly EnemyTacticalMovePlanner movePlanner = new();
    // 이동 경로를 담는 재사용 버퍼다.
    private readonly List<GridPosition> pathBuffer = new();
    // 후보 점수 로그를 담는 재사용 버퍼다.
    private readonly List<EnemyTacticalPositionScoreResult> scoreResults = new();
    // 플레이어 진영에서 가장 가까운 살아 있는 표적을 선택한다.
    private readonly EnemyTargetSelector targetSelector = new();

    /// <summary>
    /// 적 턴 행동에 필요한 참조와 데이터를 확인한다.
    /// </summary>
    private void Awake()
    {
        EnsureScoreSettingsInitialized();

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 이 적의 턴 행동을 실행하고 논리/연출 이벤트를 지정한 문맥에 기록한다.
    /// </summary>
    public bool TryExecuteTurn(ActionResolutionContext resolutionContext)
    {
        if (!CanAct(resolutionContext))
        {
            return false;
        }

        if (enemyContext.AlertState.IsUnaware)
        {
            return enemyContext.RoutineController != null &&
                enemyContext.RoutineController.TryExecuteRoutineTurn(resolutionContext);
        }

        if (enemyContext.AlertState.IsSuspicious)
        {
            return enemyContext.InvestigationAgent != null &&
                enemyContext.InvestigationAgent.TryExecuteSuspiciousTurn(resolutionContext);
        }

        return TryExecuteAlertedTurn(resolutionContext);
    }

    /// <summary>
    /// 발각 상태의 기존 엄폐 이동과 공격 행동을 실행한다.
    /// </summary>
    private bool TryExecuteAlertedTurn(ActionResolutionContext resolutionContext)
    {

        if (!targetSelector.TrySelectNearestPlayerUnit(enemyContext.GridActor.GridPosition, out TacticalUnitContext targetUnit))
        {
            if (logTurnAction)
            {
                Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적이 공격할 살아 있는 플레이어 진영 유닛을 찾지 못했습니다.", this);
            }

            return false;
        }

        GridActor playerActor = targetUnit.GridActor;
        bool acted = false;

        if (enemyContext.AttackAction.CanAttack(playerActor))
        {
            acted |= TryAttack(playerActor, resolutionContext);
            acted |= TryMoveToBestCover(playerActor.GridPosition, resolutionContext);
            return acted;
        }

        if (TryMoveToAttackCover(playerActor.GridPosition, resolutionContext))
        {
            acted = true;
            acted |= TryAttack(playerActor, resolutionContext);
            return acted;
        }

        acted |= TryMoveToBestCover(playerActor.GridPosition, resolutionContext);
        if (!acted && logTurnAction)
        {
            Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적은 공격 가능한 엄폐 위치와 이동할 엄폐 위치를 찾지 못해 대기합니다.", this);
        }

        return acted;
    }

    /// <summary>
    /// 공격 가능한 엄폐 위치로 이동한다.
    /// </summary>
    private bool TryMoveToAttackCover(GridPosition playerPosition, ActionResolutionContext resolutionContext)
    {
        if (!enemyContext.ActionPoint.CanSpend(MoveActionPointCost))
        {
            return false;
        }

        GridPosition startPosition = enemyContext.GridActor.GridPosition;
        EnemyAttackAction attackAction = enemyContext.AttackAction;
        bool found = movePlanner.TryFindBestCoverPath(
            GridManager.Instance,
            startPosition,
            playerPosition,
            enemyContext.EnemyData.TurnMoveRange,
            scoreSettings,
            pathBuffer,
            logCandidateScores ? scoreResults : null,
            out GridPosition targetPosition,
            candidate => attackAction.CanAttackFrom(candidate, playerPosition),
            false);

        if (!found)
        {
            if (logTurnAction)
            {
                Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적이 공격 가능한 엄폐 위치를 찾지 못했습니다.", this);
            }

            return false;
        }

        LogCandidateScores();
        return TryMoveAlongPath(pathBuffer, targetPosition, "공격 가능한 엄폐 위치 이동", resolutionContext);
    }

    /// <summary>
    /// 이동 가능 범위 안의 최고 엄폐 위치로 이동한다. 현재 위치가 최고면 이동하지 않는다.
    /// </summary>
    private bool TryMoveToBestCover(GridPosition playerPosition, ActionResolutionContext resolutionContext)
    {
        if (!enemyContext.ActionPoint.CanSpend(MoveActionPointCost))
        {
            return false;
        }

        GridPosition startPosition = enemyContext.GridActor.GridPosition;
        bool found = movePlanner.TryFindBestCoverPath(
            GridManager.Instance,
            startPosition,
            playerPosition,
            enemyContext.EnemyData.TurnMoveRange,
            scoreSettings,
            pathBuffer,
            logCandidateScores ? scoreResults : null,
            out GridPosition targetPosition,
            null,
            true);

        if (!found)
        {
            if (logTurnAction)
            {
                Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적이 이동할 엄폐 위치를 찾지 못했습니다.", this);
            }

            return false;
        }

        LogCandidateScores();
        if (targetPosition == startPosition)
        {
            if (logTurnAction)
            {
                Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적은 현재 위치가 가장 좋은 엄폐 위치라 이동하지 않습니다.", this);
            }

            return false;
        }

        return TryMoveAlongPath(pathBuffer, targetPosition, "최고 엄폐 위치 이동", resolutionContext);
    }

    /// <summary>
    /// 계산된 경로를 따라 적 논리 위치를 이동시키고 1칸 단위 연출 이벤트를 추가한다.
    /// </summary>
    private bool TryMoveAlongPath(
        IReadOnlyList<GridPosition> path,
        GridPosition targetPosition,
        string reason,
        ActionResolutionContext resolutionContext)
    {
        if (path.Count <= 0)
        {
            return false;
        }

        int movedSteps = EnemyMovementUtility.MoveAlongPath(enemyContext, path, resolutionContext, "적 턴 이동 연출");
        if (movedSteps != path.Count)
        {
            return false;
        }

        if (!enemyContext.ActionPoint.TrySpend(MoveActionPointCost))
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적의 이동 AP 소비에 실패했습니다.", this);
            return false;
        }

        if (logTurnAction)
        {
            Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적이 {reason}으로 {targetPosition} 칸까지 이동했습니다. 남은 AP: {enemyContext.ActionPoint.Current}", this);
        }

        return true;
    }

    /// <summary>
    /// 적 원거리 공격을 실행하고 AP를 소비한다.
    /// </summary>
    private bool TryAttack(GridActor playerActor, ActionResolutionContext resolutionContext)
    {
        if (!enemyContext.ActionPoint.CanSpend(AttackActionPointCost))
        {
            return false;
        }

        if (!enemyContext.AttackAction.TryExecuteAttack(playerActor, resolutionContext))
        {
            return false;
        }

        if (!enemyContext.ActionPoint.TrySpend(AttackActionPointCost))
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적의 공격 AP 소비에 실패했습니다.", this);
            return false;
        }

        if (logTurnAction)
        {
            Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적이 원거리 공격을 실행했습니다. 남은 AP: {enemyContext.ActionPoint.Current}", this);
        }

        return true;
    }

    /// <summary>
    /// 이 적이 현재 턴 행동을 실행할 수 있는지 확인한다.
    /// </summary>
    private bool CanAct(ActionResolutionContext resolutionContext)
    {
        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (resolutionContext == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 적 턴 행동을 처리할 {nameof(ActionResolutionContext)}가 필요합니다.", this);
            return false;
        }

        if (GridManager.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 적 턴 이동 계산에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.IsAlive)
        {
            if (logTurnAction)
            {
                Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적은 전투불능이라 행동하지 않습니다.", this);
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 후보 칸별 엄폐 점수 내역을 로그로 출력한다.
    /// </summary>
    private void LogCandidateScores()
    {
        if (!logCandidateScores)
        {
            return;
        }

        for (int i = 0; i < scoreResults.Count; i++)
        {
            Debug.Log($"{nameof(EnemyTurnAgent)}: {enemyContext.name} 적 턴 후보 {scoreResults[i]}", this);
        }
    }

    /// <summary>
    /// 적 턴 행동에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyContext == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!enemyContext.enabled || !enemyContext.HasValidReference())
        {
            return false;
        }

        if (enemyContext.AlertState != null && enemyContext.AlertState.IsAlerted &&
            (enemyContext.AttackAction == null || !enemyContext.AttackAction.enabled))
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 {nameof(EnemyContext)}에 연결된 활성 {nameof(EnemyAttackAction)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyContext.ActionPoint == null || !enemyContext.ActionPoint.enabled)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}에는 {nameof(EnemyContext)}에 연결된 활성 {nameof(EnemyActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyContext.AlertState != null && enemyContext.AlertState.IsUnaware && enemyContext.RoutineController == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 평상 행동에는 {nameof(EnemyRoutineController)} 참조가 필요합니다.", this);
            return false;
        }

        if (enemyContext.AlertState != null && enemyContext.AlertState.IsSuspicious && enemyContext.InvestigationAgent == null)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 의심 행동에는 {nameof(EnemyInvestigationAgent)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 적 턴 행동에 필요한 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        EnemyData enemyData = enemyContext.EnemyData;
        if (enemyData.TurnActionPoint < 0)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 적 턴 AP는 0 이상이어야 합니다.", this);
            return false;
        }

        if (enemyData.TurnMoveRange < 0)
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 적 턴 이동 거리는 0 이상이어야 합니다.", this);
            return false;
        }

        if (!scoreSettings.HasValidData())
        {
            Debug.LogError($"{nameof(EnemyTurnAgent)} on {name}의 엄폐 점수 또는 각도 설정이 올바르지 않습니다.", this);
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
