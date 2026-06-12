using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 발각 이벤트를 받아 최초 감지 적 기준으로 주변 적에게 애드를 전파하는 조정자다.
/// 현재 단계에서는 전파 대상 계산과 로그 출력만 담당한다.
/// </summary>
public class EnemyAlertCoordinator : MonoBehaviour
{
    [Header("Source")]
    // 플레이어의 이동 위험 평가 이벤트를 제공하는 필수 Context다.
    [SerializeField] private PlayerContext playerContext;

    [Header("Log")]
    // true면 애드 전파 대상 계산 결과를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAlertSpread = true;

    // 발각 이벤트를 발행하는 플레이어 이동 위험 평가 컴포넌트다.
    private GridMoveRiskEvaluator riskEvaluator;
    // 발각 이벤트를 현재 구독 중인지 나타낸다.
    private bool subscribedAlertEvent;

    /// <summary>
    /// 애드 전파에 필요한 플레이어 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 컴포넌트가 활성화될 때 발각 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeAlertEvent(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 이벤트 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeAlertEvent(true);
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 발각 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (riskEvaluator != null)
        {
            riskEvaluator.AlertTriggered -= HandleAlertTriggered;
        }

        subscribedAlertEvent = false;
    }

    /// <summary>
    /// 플레이어 이동 위험 평가 컴포넌트의 발각 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeAlertEvent(bool logMissingRegistry)
    {
        if (subscribedAlertEvent)
        {
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        if (EnemyRegistry.Instance == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(EnemyAlertCoordinator)} on {name}에는 애드 전파 대상 조회에 사용할 씬의 {nameof(EnemyRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        riskEvaluator = playerContext.GridMoveRiskEvaluator;
        riskEvaluator.AlertTriggered += HandleAlertTriggered;
        subscribedAlertEvent = true;
    }

    /// <summary>
    /// 발각 이벤트를 받아 최초 감지 적 기준 전파 대상 적을 계산한다.
    /// </summary>
    private void HandleAlertTriggered(GridPosition detectedPosition, EnemyGridSight detectingSight)
    {
        if (!TryFindEnemyContext(detectingSight, out EnemyContext detectingEnemy))
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)}: 최초 감지 적 Context를 찾지 못해 애드 전파를 중단합니다.", this);
            return;
        }

        if (!HasValidData(detectingEnemy))
        {
            return;
        }

        int spreadRange = detectingEnemy.EnemyData.AlertSpreadRange;
        GridPosition spreadOrigin = detectingEnemy.GridActor.GridPosition;
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

            if (logAlertSpread)
            {
                Debug.Log($"{nameof(EnemyAlertCoordinator)}: {detectedPosition} 칸 발각이 {detectingEnemy.name} 기준 {distance}칸 거리의 {enemy.name} 적에게 전파됐습니다.", this);
            }
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
    /// 애드 전파를 받을 수 있는 적 Context인지 확인한다.
    /// </summary>
    private static bool CanReceiveAlert(EnemyContext enemy)
    {
        return enemy != null && enemy.enabled && enemy.HasValidReference();
    }

    /// <summary>
    /// 애드 전파에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (playerContext.GridMoveRiskEvaluator == null)
        {
            Debug.LogError($"{nameof(EnemyAlertCoordinator)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(GridMoveRiskEvaluator)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
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
