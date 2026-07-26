using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 발각이나 피해 이벤트를 받아 기준 적 주변으로 애드를 전파하는 조정자다.
/// 현재 단계에서는 전파 대상 계산과 적 상태 전환 요청을 담당한다.
/// </summary>
public class EnemyAlertCoordinator : MonoBehaviour, IActionLogicEventHandler
{
    [Header("Log")]
    // true면 애드 전파 대상 계산과 상태 전환 요청 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAlertSpread = true;

    /// <summary>
    /// 애드 전파에 필요한 플레이어 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        // 논리 이벤트와 전술 유닛 등록소는 실행 시점에 검사한다.
    }

    /// <summary>
    /// 컴포넌트가 활성화될 때 발각 이벤트 구독을 시도한다.
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
        if (EnemyRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)} on {name}에는 애드 전파 대상 조회에 사용할 씬의 {nameof(EnemyRegistry)}가 필요합니다.", this);
            enabled = false;
        }

        if (TacticalUnitRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)} on {name}에는 플레이어 진영 인식에 사용할 씬의 {nameof(TacticalUnitRegistry)}가 필요합니다.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 논리 이벤트 핸들러 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        ActionLogicEventBus.Unregister(this);
    }

    /// <summary>
    /// 발각 이벤트를 받아 최초 감지 적 기준 전파 대상 적을 계산한다.
    /// </summary>
    private void HandleAlertTriggered(AlertTriggeredLogicEvent logicEvent, ActionResolutionContext context)
    {
        EnemyContext detectingEnemy = logicEvent.DetectingEnemy;
        if (detectingEnemy == null && !TryFindEnemyContext(logicEvent.DetectingSight, out detectingEnemy))
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)}: 최초 감지 적 Context를 찾지 못해 애드 전파를 중단합니다.", this);
            return;
        }

        SpreadAlertFromSource(detectingEnemy, logicEvent.DetectedPosition, logicEvent.DetectedPosition, EnemyAlertReason.SightDetected, context);
    }

    /// <summary>
    /// 피해 적용 이벤트를 받아 피해 대상 적 기준 전파 대상 적을 계산한다.
    /// </summary>
    private void HandleDamageApplied(DamageAppliedLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (!logicEvent.Applied || logicEvent.Target == null)
        {
            return;
        }

        if (!TryFindEnemyContext(logicEvent.Target, out EnemyContext damagedEnemy))
        {
            return;
        }

        GridPosition knownPlayerPosition = logicEvent.Attacker != null ? logicEvent.Attacker.GridPosition : logicEvent.TargetPosition;
        SpreadAlertFromSource(damagedEnemy, logicEvent.TargetPosition, knownPlayerPosition, EnemyAlertReason.Damaged, context);
    }

    /// <summary>
    /// 기준 적의 애드 전파 범위 안 적들을 경계 상태로 전환하고 후속 논리 이벤트를 발행한다.
    /// </summary>
    private void SpreadAlertFromSource(
        EnemyContext sourceEnemy,
        GridPosition detectedPosition,
        GridPosition knownPlayerPosition,
        EnemyAlertReason sourceReason,
        ActionResolutionContext context)
    {
        if (!HasValidData(sourceEnemy))
        {
            return;
        }

        int spreadRange = sourceEnemy.EnemyData.AlertSpreadRange;
        GridPosition spreadOrigin = sourceEnemy.GridActor.GridPosition;
        IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (!CanReceiveAlert(enemy))
            {
                continue;
            }

            int distance = spreadOrigin.ManhattanDistanceTo(enemy.GridActor.GridPosition);
            if (distance > spreadRange)
            {
                continue;
            }

            bool changed = enemy.AlertState.RequestAlert(detectedPosition, sourceEnemy);
            if (!changed)
            {
                continue;
            }

            EnemyAlertReason reason = enemy == sourceEnemy ? sourceReason : EnemyAlertReason.Spread;
            if (logAlertSpread)
            {
                Debug.Log($"{nameof(EnemyAlertCoordinator)}: {detectedPosition} 칸 사건을 {sourceEnemy.name} 기준 {distance}칸 거리의 {enemy.name} 적에게 전파했습니다. 원인: {reason}", this);
            }

            context.EnqueuePresentation(PresentationEvent.AlertDetected(
                detectedPosition,
                enemy,
                "적 경계 상태 전환 연출"));
            context.Publish(new EnemyAlertedLogicEvent(enemy, sourceEnemy, detectedPosition, knownPlayerPosition, reason));
        }
    }

    /// <summary>
    /// 지정한 논리 이벤트를 이 컴포넌트가 처리할 수 있는지 확인한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is AlertTriggeredLogicEvent || logicEvent is DamageAppliedLogicEvent;
    }

    /// <summary>
    /// 발각 논리 이벤트를 처리해 주변 적에게 애드를 전파한다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is AlertTriggeredLogicEvent alertTriggered)
        {
            HandleAlertTriggered(alertTriggered, context);
            return;
        }

        if (logicEvent is DamageAppliedLogicEvent damageApplied)
        {
            HandleDamageApplied(damageApplied, context);
        }
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
    /// 지정한 GridActor를 가진 EnemyContext를 현재 등록소에서 찾는다.
    /// </summary>
    private bool TryFindEnemyContext(GridActor gridActor, out EnemyContext enemyContext)
    {
        if (gridActor != null && EnemyRegistry.Instance != null)
        {
            IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyContext enemy = enemies[i];
                if (enemy != null && enemy.GridActor == gridActor)
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
    /// 애드 전파를 받을 수 있는 적 Context인지 확인한다.
    /// </summary>
    private static bool CanReceiveAlert(EnemyContext enemy)
    {
        if (enemy == null || !enemy.enabled || !enemy.HasValidReference() || enemy.AlertState == null || !enemy.AlertState.enabled)
        {
            return false;
        }

        return enemy.IsAlive;
    }

    /// <summary>
    /// 애드 전파에 필요한 적 데이터가 유효한지 확인한다.
    /// </summary>
    private bool HasValidData(EnemyContext detectingEnemy)
    {
        if (detectingEnemy == null || detectingEnemy.EnemyData == null)
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)}: 최초 감지 적 데이터가 없어 애드 전파를 중단합니다.", this);
            return false;
        }

        if (detectingEnemy.EnemyData.AlertSpreadRange < 0)
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)}: {detectingEnemy.name}의 애드 전파 범위는 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
