using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 턴 시작 시 현재 씬의 적들을 순서대로 실행하는 조정자다.
/// 각 적의 실제 행동 판단은 EnemyTurnAgent가 담당한다.
/// </summary>
public class EnemyTurnCoordinator : MonoBehaviour
{
    [Header("Reference")]
    // 적 턴이 끝난 뒤 플레이어 턴으로 넘길 턴 매니저다.
    [SerializeField] private TurnManager turnManager;
    // 적 행동 연출 이벤트를 재생할 연출 큐다.
    [SerializeField] private ActionPresentationQueue presentationQueue;

    [Header("Log")]
    // true면 적 턴 시작, 적별 실행, 적 턴 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logEnemyTurn = true;
    // EnemyTurnAgent가 연결되지 않은 적을 만났을 때 로그를 남길지 정한다.
    [SerializeField] private bool logMissingAgent = true;

    // 현재 적 턴 실행 코루틴이다.
    private Coroutine enemyTurnCoroutine;

    /// <summary>
    /// 적 턴 조정에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        // 씬 단위 참조는 초기화 순서가 끝난 Start와 실행 시점에 검사한다.
    }

    /// <summary>
    /// 턴 시작 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        TrySubscribeTurnManager(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeTurnManager(true);
    }

    /// <summary>
    /// 턴 시작 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.TurnStarted -= HandleTurnStarted;
        }

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }
    }

    /// <summary>
    /// 턴 시작 이벤트를 받아 적 턴이면 적 행동 실행을 시작한다.
    /// </summary>
    private void HandleTurnStarted(TurnSide side)
    {
        if (side != TurnSide.Enemy || enemyTurnCoroutine != null)
        {
            return;
        }

        enemyTurnCoroutine = StartCoroutine(RunEnemyTurn());
    }

    /// <summary>
    /// 현재 씬의 적들을 순서대로 실행하고 모든 연출이 끝난 뒤 플레이어 턴으로 넘긴다.
    /// </summary>
    private IEnumerator RunEnemyTurn()
    {
        if (!HasValidReference())
        {
            enemyTurnCoroutine = null;
            yield break;
        }

        if (logEnemyTurn)
        {
            Debug.Log($"{nameof(EnemyTurnCoordinator)}: 적 턴 행동을 시작합니다.", this);
        }

        yield return WaitForPresentationQueue();

        IReadOnlyList<EnemyContext> enemies = EnemyRegistry.Instance.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy == null || !enemy.enabled)
            {
                continue;
            }

            EnemyTurnAgent turnAgent = enemy.TurnAgent;
            if (turnAgent == null || !turnAgent.enabled)
            {
                if (logMissingAgent)
                {
                    Debug.Log($"{nameof(EnemyTurnCoordinator)}: {enemy.name} 적에는 활성 {nameof(EnemyTurnAgent)}가 없어 적 턴 행동을 생략합니다.", this);
                }

                continue;
            }

            if (enemy.ActionPoint == null || !enemy.ActionPoint.enabled)
            {
                if (logMissingAgent)
                {
                    Debug.Log($"{nameof(EnemyTurnCoordinator)}: {enemy.name} 적에는 활성 {nameof(EnemyActionPoint)}가 없어 적 턴 행동을 생략합니다.", this);
                }

                continue;
            }

            enemy.ActionPoint.RefillForTurn();

            ActionResolutionContext resolutionContext = new(presentationQueue);
            bool acted = turnAgent.TryExecuteTurn(resolutionContext);
            resolutionContext.Resolve();

            if (acted || presentationQueue.QueuedEventCount > 0)
            {
                presentationQueue.PlayQueuedEvents();
                yield return WaitForPresentationQueue();
            }
        }

        if (logEnemyTurn)
        {
            Debug.Log($"{nameof(EnemyTurnCoordinator)}: 적 턴 행동을 끝냅니다.", this);
        }

        enemyTurnCoroutine = null;
        if (turnManager.IsEnemyTurn)
        {
            turnManager.EndCurrentTurn();
        }
    }

    /// <summary>
    /// 현재 연출 큐가 비고 재생이 끝날 때까지 기다린다.
    /// </summary>
    private IEnumerator WaitForPresentationQueue()
    {
        if (presentationQueue.QueuedEventCount > 0 && !presentationQueue.IsPlaying)
        {
            presentationQueue.PlayQueuedEvents();
        }

        while (presentationQueue.IsPlaying || presentationQueue.QueuedEventCount > 0)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 현재 씬의 턴 매니저 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeTurnManager(bool logMissingTurnManager)
    {
        if (turnManager == null && TurnManager.Instance != null)
        {
            turnManager = TurnManager.Instance;
        }

        if (turnManager == null)
        {
            if (logMissingTurnManager)
            {
                Debug.LogError($"{nameof(EnemyTurnCoordinator)} on {name}에는 {nameof(TurnManager)} 참조가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        turnManager.TurnStarted -= HandleTurnStarted;
        turnManager.TurnStarted += HandleTurnStarted;
    }

    /// <summary>
    /// 적 턴 조정에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (turnManager == null && TurnManager.Instance != null)
        {
            turnManager = TurnManager.Instance;
        }

        if (turnManager == null)
        {
            Debug.LogError($"{nameof(EnemyTurnCoordinator)} on {name}에는 {nameof(TurnManager)} 참조가 필요합니다.", this);
            return false;
        }

        if (presentationQueue == null && ActionPresentationQueue.Instance != null)
        {
            presentationQueue = ActionPresentationQueue.Instance;
        }

        if (presentationQueue == null)
        {
            Debug.LogError($"{nameof(EnemyTurnCoordinator)} on {name}에는 {nameof(ActionPresentationQueue)} 참조가 필요합니다.", this);
            return false;
        }

        if (EnemyRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyTurnCoordinator)} on {name}에는 {nameof(EnemyRegistry)}가 필요합니다.", this);
            return false;
        }

        if (TacticalUnitRegistry.Instance == null)
        {
            Debug.LogError($"{nameof(EnemyTurnCoordinator)} on {name}에는 {nameof(TacticalUnitRegistry)}가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
