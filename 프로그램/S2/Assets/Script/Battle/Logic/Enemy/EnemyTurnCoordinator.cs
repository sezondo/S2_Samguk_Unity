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
    // 적 턴을 계속 실행할 수 있는 스테이지 진행 상태를 제공한다.
    [SerializeField] private StageStateManager stageStateManager;

    [Header("Log")]
    // true면 적 턴 시작, 적별 실행, 적 턴 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logEnemyTurn = true;
    // EnemyTurnAgent가 연결되지 않은 적을 만났을 때 로그를 남길지 정한다.
    [SerializeField] private bool logMissingAgent = true;

    // 현재 적 턴 실행 코루틴이다.
    private Coroutine enemyTurnCoroutine;
    // 논리 생산과 마지막 연출 완료를 포함한 적 턴 처리 상태다.
    public bool IsRunning { get; private set; }
    // 한 프레임에 누적할 판단 시간의 목표다. 단일 Plan 호출을 강제 중단하지는 않는다.
    private const double LogicSliceMilliseconds = 4;
    // 최근 적 턴의 판단·논리 확정 시간과 단일 행동 최대 시간, 실행 시도 수다.
    public double LastLogicMilliseconds { get; private set; }
    public double LastMaxActionMilliseconds { get; private set; }
    public int LastActionCount { get; private set; }

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
        if (IsRunning && presentationQueue != null)
        {
            presentationQueue.EndProduction();
            presentationQueue.ReleaseTurnProcessing();
        }
        IsRunning = false;
    }

    /// <summary>
    /// 턴 시작 이벤트를 받아 적 턴이면 적 행동 실행을 시작한다.
    /// </summary>
    private void HandleTurnStarted(TurnSide side)
    {
        if (side != TurnSide.Enemy ||
            IsRunning ||
            stageStateManager == null ||
            !stageStateManager.IsPlaying)
        {
            return;
        }

        IsRunning = true;
        enemyTurnCoroutine = StartCoroutine(RunEnemyTurn());
        if (!IsRunning) enemyTurnCoroutine = null;
    }

    /// <summary>
    /// 현재 씬의 적들을 순서대로 실행하고 모든 연출이 끝난 뒤 플레이어 턴으로 넘긴다.
    /// </summary>
    private IEnumerator RunEnemyTurn()
    {
        bool completedNormally = false;
        bool ownsProduction = false;
        try
        {
            if (!HasValidReference() || !stageStateManager.IsPlaying) yield break;
            // 빈 큐에 대한 불필요한 한 프레임 대기로 적 턴 시작 직후 잠금이 비지 않게 한다.
            if (presentationQueue.IsPlaying || presentationQueue.QueuedEventCount > 0)
                yield return WaitForPresentationQueue();
            if (!stageStateManager.IsPlaying || !presentationQueue.TryBeginProduction()) yield break;

            ownsProduction = true;
            LastLogicMilliseconds = LastMaxActionMilliseconds = 0;
            LastActionCount = 0;
            var slice = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 등록 변경과 새 발각을 구분하기 위해 적 목록과 턴 시작의 전투 상태를 저장한다.
                var enemies = new List<EnemyContext>(EnemyRegistry.Instance.Enemies);
                var combatAtStart = new HashSet<EnemyContext>();
                foreach (var enemy in enemies)
                {
                    if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive) continue;
                    if (enemy.AlertState.IsAlerted) combatAtStart.Add(enemy);
                    if (enemy.ActionPoint != null && enemy.ActionPoint.enabled)
                    {
                        enemy.ActionPoint.RefillForTurn();
                        enemy.TurnAgent?.BeginTurn();
                    }
                }

                foreach (var enemy in enemies)
                {
                    if (!stageStateManager.IsPlaying || presentationQueue.HasFailed) break;
                    if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive) continue;
                    var agent = enemy.TurnAgent;
                    if (agent == null || !agent.enabled || enemy.ActionPoint == null || !enemy.ActionPoint.enabled)
                    {
                        if (logMissingAgent) Debug.Log($"{name}: {enemy.name}의 턴 행동 참조가 없어 건너뜁니다.", this);
                        continue;
                    }
                    // 이 턴 도중 새로 발각된 적은 즉시 반응만 수행하고 다음 턴부터 공격한다.
                    bool combatTurn = combatAtStart.Contains(enemy);
                    if (!combatTurn && enemy.AlertState.IsAlerted) continue;
                    do
                    {
                        int beforeAP = enemy.ActionPoint.Current;
                        var context = new ActionResolutionContext(presentationQueue);
                        bool acted = ExecuteLogicalAction(agent, context);
                        presentationQueue.PlayQueuedEvents();
                        if (context.HasFailed || presentationQueue.HasFailed) break;

                        // 연출 완료와 무관하게 시간 예산만 양보한다. 다음 프레임에도 논리는 계속 앞서간다.
                        if (slice.Elapsed.TotalMilliseconds >= LogicSliceMilliseconds)
                        {
                            yield return null;
                            slice.Restart();
                        }
                        if (!combatTurn || !acted || enemy.ActionPoint.Current >= beforeAP) break;
                    }
                    while (stageStateManager.IsPlaying && enemy.IsAlive && agent.enabled && enemy.ActionPoint.Current > 0);
                }
            }
            finally
            {
                presentationQueue.EndProduction();
            }

            // 실패·클리어 때도 확정된 연출은 끝까지 처리하고 결과 UI가 앞서지 않게 한다.
            yield return WaitForPresentationQueue();
            completedNormally = !presentationQueue.HasFailed;
            if (logEnemyTurn)
                Debug.Log($"적 턴 논리 {LastLogicMilliseconds:F2}ms, 단일 최대 {LastMaxActionMilliseconds:F2}ms, 판단 {LastActionCount}회", this);
        }
        finally
        {
            if (ownsProduction) presentationQueue.ReleaseTurnProcessing();
            IsRunning = false;
            enemyTurnCoroutine = null;
        }
        if (completedNormally && stageStateManager.IsPlaying && turnManager.IsEnemyTurn)
            turnManager.EndCurrentTurn();
    }

    /// <summary>한 행동의 파생 논리까지 확정하고 예외 시 이후 생산을 정지한다.</summary>
    private bool ExecuteLogicalAction(EnemyTurnAgent agent, ActionResolutionContext context)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            bool acted = agent.TryExecuteTurn(context);
            context.Resolve();
            if (!agent.enabled) context.Fail("적 행동 실행이 중단되어 후속 판단을 정지합니다.");
            return acted;
        }
        catch (System.Exception exception)
        {
            context.Fail($"적 행동 실행 예외: {exception}");
            return false;
        }
        finally
        {
            timer.Stop();
            double elapsed = timer.Elapsed.TotalMilliseconds;
            LastLogicMilliseconds += elapsed;
            LastMaxActionMilliseconds = System.Math.Max(LastMaxActionMilliseconds, elapsed);
            LastActionCount++;
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

        if (stageStateManager == null)
        {
            Debug.LogError($"{nameof(EnemyTurnCoordinator)} on {name}에는 진행 상태를 제공할 {nameof(StageStateManager)} 참조가 필요합니다.", this);
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
