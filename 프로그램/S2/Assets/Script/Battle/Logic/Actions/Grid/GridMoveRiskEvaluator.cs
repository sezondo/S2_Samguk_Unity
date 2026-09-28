using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 이동 경로의 새 발각 위험을 평가하고 실제 칸 진입 감지를 전달한다.
/// 그래픽은 이동 표시기가 담당하며 실제 감지와 예측의 공개 범위를 구분한다.
/// </summary>
[RequireComponent(typeof(TacticalUnitContext))]
[RequireComponent(typeof(PlayerGridMoveAction))]
public class GridMoveRiskEvaluator : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Source")]
    // 플레이어 공통 참조와 이동 행동 컴포넌트를 제공하는 필수 Context다.
    [SerializeField] private TacticalUnitContext playerContext;

    [Header("Log")]
    // 실제 이동 중 감지 칸에 진입했을 때 1차 애드 로그를 출력할지 정한다.
    [SerializeField] private bool logAddTriggered = true;

    // 현재 이동 실행 중 애드 로그를 이미 남겼는지 나타낸다.
    private bool didLogAddInCurrentMove;

    /// <summary>
    /// 이동 위험 평가에 필요한 유닛 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

    }

    /// <summary>
    /// 실제 이동 감지에 필요한 논리 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        ActionLogicEventBus.Register(this);
    }

    /// <summary>씬 초기화 이후 필수 적 등록소 연결을 확인한다.</summary>
    private void Start()
    {
        if (EnemyRegistry.Instance != null) return;
        Debug.LogError($"{name}: 이동 위험 평가에는 씬의 EnemyRegistry가 필요합니다.", this);
        enabled = false;
    }

    /// <summary>비활성화되면 논리 이벤트 수신을 해제한다.</summary>
    private void OnDisable() => ActionLogicEventBus.Unregister(this);

    /// <summary>현재 보이는 미발각 적에게 새로 들키는 첫 경로 인덱스를 반환한다.</summary>
    public int FindFirstPreviewRiskIndex(IReadOnlyList<GridPosition> path)
    {
        if (path == null || !isActiveAndEnabled) return -1;
        for (int i = 0; i < path.Count; i++)
            if (TryFindDetectingEnemy(path[i], true, out _)) return i;
        return -1;
    }

    /// <summary>
    /// 실제 이동 중 감지 칸에 들어가면 1차 발각 이벤트를 알린다.
    /// </summary>
    private void HandleMoveStepEntered(MoveStepEnteredLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent.Actor != playerContext.GridActor ||
            didLogAddInCurrentMove ||
            !TryFindDetectingEnemy(logicEvent.StepPosition, false, out EnemyGridSight detectingSight))
        {
            return;
        }

        didLogAddInCurrentMove = true;
        TryFindEnemyContext(detectingSight, out EnemyContext detectingEnemy);
        if (logAddTriggered)
        {
            Debug.Log($"{nameof(GridMoveRiskEvaluator)}: {logicEvent.StepPosition} 칸에서 {detectingSight.name} 시야에 들어와 애드가 발생했습니다.", this);
        }

        context.Publish(new AlertTriggeredLogicEvent(logicEvent.StepPosition, detectingEnemy, detectingSight));
    }

    /// <summary>
    /// 이동 완료 후 이동 실행 중 애드 로그 상태를 초기화한다.
    /// </summary>
    private void HandleMoveCompleted(MoveCompletedLogicEvent logicEvent)
    {
        if (logicEvent.Actor != playerContext.GridActor)
        {
            return;
        }

        didLogAddInCurrentMove = false;
    }

    /// <summary>
    /// 지정한 논리 이벤트를 이 컴포넌트가 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        GridActor playerActor = playerContext != null ? playerContext.GridActor : null;
        return playerActor != null &&
            (logicEvent is MoveStepEnteredLogicEvent moveStepEntered && moveStepEntered.Actor == playerActor ||
             logicEvent is MoveCompletedLogicEvent moveCompleted && moveCompleted.Actor == playerActor);
    }

    /// <summary>
    /// 이동 칸 진입과 이동 완료 논리 이벤트를 처리한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is MoveStepEnteredLogicEvent moveStepEntered)
        {
            HandleMoveStepEntered(moveStepEntered, context);
            return;
        }

        if (logicEvent is MoveCompletedLogicEvent moveCompleted)
        {
            HandleMoveCompleted(moveCompleted);
        }
    }

    /// <summary>
    /// 지정한 칸을 감지할 수 있는 적 시야 컴포넌트를 찾고 필요하면 현재 보이는 적으로 제한한다.
    /// </summary>
    private bool TryFindDetectingEnemy(
        GridPosition position,
        bool requireVisibleEnemy,
        out EnemyGridSight detectingEnemy)
    {
        if (EnemyRegistry.Instance == null)
        {
            detectingEnemy = null;
            return false;
        }

        IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            EnemyGridSight enemySight = enemy != null ? enemy.GridSight : null;
            // 예측은 살아 있고 보이는 미발각 적만 사용한다. 실제 감지는 안개와 무관하다.
            if (enemy != null && enemy.isActiveAndEnabled && enemy.IsAlive && enemySight != null &&
                enemySight.isActiveAndEnabled &&
                (!requireVisibleEnemy || (enemy.AlertState != null && !enemy.AlertState.IsAlerted && IsEnemyVisibleToPlayer(enemy))) &&
                enemySight.CanDetect(position))
            {
                detectingEnemy = enemySight;
                return true;
            }
        }

        detectingEnemy = null;
        return false;
    }

    /// <summary>
    /// 지정한 적의 현재 칸이 플레이어 합산 시야 안에 있는지 확인한다.
    /// </summary>
    private bool IsEnemyVisibleToPlayer(EnemyContext enemy)
    {
        if (enemy == null || enemy.GridActor == null)
        {
            return false;
        }

        PlayerVisionManager visionManager = PlayerVisionManager.Instance;
        return visionManager != null && visionManager.IsVisible(enemy.GridActor.GridPosition);
    }

    /// <summary>
    /// 지정한 적 시야 컴포넌트를 가진 EnemyContext를 현재 등록소에서 찾는다.
    /// </summary>
    private bool TryFindEnemyContext(EnemyGridSight gridSight, out EnemyContext enemyContext)
    {
        if (gridSight != null && EnemyRegistry.Instance != null)
        {
            IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyContext enemy = enemies[i];
                if (enemy != null && enemy.GridSight == gridSight)
                {
                    enemyContext = enemy;
                    return true;
                }
            }
        }

        enemyContext = null;
        return false;
    }

    /// <summary>
    /// 이동 위험 평가에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(GridMoveRiskEvaluator)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (playerContext.GridMoveAction == null)
        {
            Debug.LogError($"{nameof(GridMoveRiskEvaluator)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(PlayerGridMoveAction)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
