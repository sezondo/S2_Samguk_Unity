using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장된 전투 씬에서 논리 선행·순차 재생·HP·입력·결과 순서를 검증한다. 플레이 변경은 저장하지 않는다.</summary>
[InitializeOnLoad]
public static class EnemyQueueFlowVerification
{
    // 도메인 리로드 이후 실행 요청과 검증 결과를 보관한다.
    private const string Pending = "S2.QueueFlow.Verify";
    private const string Output = "Temp/QueueFlow/verification.txt";
    private static readonly List<string> results = new();
    private static IEnumerator routine;
    private static double nextStep;
    // 이벤트 콜백 안에서 예외를 던져 실제 연출을 중단하지 않고 결과만 수집한다.
    private static string callbackFailure;

    /// <summary>플레이 진입 후 실제 씬 검증을 시작한다.</summary>
    static EnemyQueueFlowVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>편집 씬을 보존한 채 검증용 플레이를 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Enemy Queue Flow")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "BattleTest01" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태에서 실행해야 합니다.");
        Directory.CreateDirectory("Temp/QueueFlow");
        File.WriteAllText(Output, "RUNNING");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>플레이 초기화 뒤 검사 루틴을 연결한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        results.Clear(); callbackFailure = null;
        routine = Run(); nextStep = EditorApplication.timeSinceStartup + 1;
        EditorApplication.update += Tick;
    }

    /// <summary>실제 프레임을 진행하며 검사 종료·예외를 기록한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            if (!EditorApplication.isPlaying) throw new Exception("검증 중 플레이 종료");
            if (!routine.MoveNext()) { Finish(null); return; }
            nextStep = EditorApplication.timeSinceStartup + .02;
        }
        catch (Exception exception) { Finish(exception); }
    }

    /// <summary>결과를 기록하고 플레이 변경을 폐기한다.</summary>
    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Output, (exception == null ? "PASS" : "FAIL: " + exception) + "\n" + string.Join("\n", results));
        if (exception == null) Debug.Log($"논리·연출 큐 검증 {results.Count}개 통과");
        else Debug.LogError("논리·연출 큐 검증 실패: " + exception);
        EditorApplication.isPlaying = false;
    }

    /// <summary>11명 실제 판단과 연속 피해·사망·결과 UI까지 검증한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        var queue = ActionPresentationQueue.Instance;
        double deadline = EditorApplication.timeSinceStartup + 40;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!queue.IsBusy, "시작 연출 완료");
        Time.timeScale = 6;
        var grid = GridManager.Instance;
        var registry = ActorPresentationRegistry.Instance;
        var turns = TurnManager.Instance;
        var coordinator = UnityEngine.Object.FindFirstObjectByType<EnemyTurnCoordinator>();
        var combat = UnityEngine.Object.FindFirstObjectByType<CombatActionPresenter>();
        var resultPresenter = UnityEngine.Object.FindFirstObjectByType<StageResultPresenter>();
        var stage = UnityEngine.Object.FindFirstObjectByType<StageStateManager>();
        var players = new EnemyTargetProvider().Collect();
        var player = players[0];
        var control = PlayerUnitControlManager.Instance;
        Check(control.TrySelectUnit(player), "시작 연출 뒤 플레이어 선택 가능");
        Check(queue.TryBeginProduction(), "자동 선택 지연 검증용 잠금 시작");
        Check(player.ActionPoint.TrySpend(player.ActionPoint.Current), "처리 중 현재 유닛 AP 소진");
        Check(control.ActiveUnit == player, "처리 중 자동 선택 보류");
        queue.EndProduction(); queue.ReleaseTurnProcessing();
        deadline = EditorApplication.timeSinceStartup + 3;
        while (control.ActiveUnit == player && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(control.ActiveUnit != player, "잠금 해제 뒤 다음 선택 가능 유닛으로 전환하거나 선택 해제");
        player.ActionPoint.RefillForTurn();
        control.TrySelectUnit(player);
        foreach (var other in players.Skip(1)) other.gameObject.SetActive(false);
        Set(player.Health, "maxHitPoint", 200); Set(player.Health, "currentHitPoint", 200);

        // 실제 에셋은 수정하지 않고 플레이 보드의 검증 구간만 비운다.
        foreach (var map in new[] { grid.WallLogicTilemap, grid.LowObstacleLogicTilemap, grid.BoundaryLogicTilemap })
            for (int x = 2; x <= 16; x++) for (int y = 2; y <= 10; y++)
                map.SetTile(map.WorldToCell(grid.GridToWorld(new GridPosition(x, y))), null);
        var candidates = UnityEngine.Object.FindObjectsByType<EnemyContext>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
        var template = candidates.First(e => e.transform.parent.name == "천하회_망치");
        foreach (var enemy in candidates) enemy.transform.parent.gameObject.SetActive(false);
        // 완성된 적 루트를 플레이 중에만 복제해 실제 컴포넌트 11개를 실행한다.
        candidates = new List<EnemyContext> { template };
        while (candidates.Count < 11)
        {
            var clone = UnityEngine.Object.Instantiate(template.transform.parent.gameObject, template.transform.parent.parent);
            clone.name = "큐 검증 적 " + candidates.Count;
            candidates.Add(clone.GetComponentInChildren<EnemyContext>(true));
        }
        Check(candidates.Count == 11, "플레이 사본에 실제 적 컴포넌트 11명 준비");
        Check(player.GridActor.TryMoveTo(new GridPosition(14, 5)), "플레이어 검증 위치 배치");
        var enemies = candidates.Take(11).ToArray();
        for (int i = 0; i < enemies.Length; i++)
        {
            var enemy = enemies[i];
            Set(enemy.GridActor, "gridPosition", new GridPosition(3 + i % 6, 3 + i / 6 * 4));
            var data = UnityEngine.Object.Instantiate(enemy.EnemyData);
            Set(enemy, "enemyData", data);
            data.Combat.allowMelee = true; data.Combat.allowRanged = false;
            data.Combat.maximumAttacks = 1;
            Set(enemy.AlertState, "currentState", EnemyAwarenessState.Alerted);
            for (var node = enemy.transform; node != null; node = node.parent) node.gameObject.SetActive(true);
        }
        yield return null;
        Check(EnemyRegistry.Instance.Enemies.Count(e => e != null && e.isActiveAndEnabled && e.IsAlive) == 11, "검증에 참여하는 적 정확히 11명");

        bool sawLogicAhead = false;
        int completedCombats = 0;
        Action<PresentationEvent> completed = _ => completedCombats++;
        combat.CombatPresentationCompleted += completed;
        turns.StartEnemyTurn();
        Check(queue.IsBusy, "적 턴 시작과 동시에 입력 잠금 유지");
        deadline = EditorApplication.timeSinceStartup + 120;
        while ((coordinator.IsRunning || turns.IsEnemyTurn || queue.IsBusy) && EditorApplication.timeSinceStartup < deadline)
        {
            if (!queue.IsProducing && queue.IsPlaying && coordinator.LastActionCount >= 11) sawLogicAhead = true;
            if (queue.IsBusy)
            {
                if (PlayerUnitControlManager.Instance.TrySelectUnit(player)) callbackFailure = "적 처리 중 수동 선택 허용";
                if (PlayerUnitActionFlowController.Instance.TryExecuteMove(new GridPosition(15, 5))) callbackFailure = "적 처리 중 플레이어 행동 허용";
                turns.EndCurrentTurn();
                if (!turns.IsEnemyTurn) callbackFailure = "대기 연출이 남은 상태에서 턴 종료";
            }
            yield return null;
        }
        combat.CombatPresentationCompleted -= completed;
        Check(!queue.HasFailed && !queue.IsBusy && !coordinator.IsRunning && turns.IsPlayerTurn, "11명 논리·연출 완료 후 플레이어 턴 복귀");
        Check(callbackFailure == null, callbackFailure ?? "생산·재생 동안 입력 및 강제 턴 전환 차단");
        Check(sawLogicAhead, "앞 연출 재생 중에도 11명 후속 논리가 먼저 완료됨");
        Check(coordinator.LastActionCount >= 11, "11명 모두 판단 실행");
        results.Add($"측정: 논리 합계 {coordinator.LastLogicMilliseconds:F2}ms / 단일 행동 최대 {coordinator.LastMaxActionMilliseconds:F2}ms / 판단 {coordinator.LastActionCount}회 / 공격 연출 {completedCombats}회");

        // 연출을 재생하기 전 두 타격과 논리 위치 변경을 모두 확정한다.
        var attacker = enemies[0];
        foreach (var other in enemies.Skip(1)) other.gameObject.SetActive(false);
        Check(attacker.GridActor.TryMoveTo(new GridPosition(13, 5)), "연속 피해 검증용 공격자 배치");
        int hp = player.Health.CurrentHitPoint;
        int displayBefore = registry.GetPresentedHitPoint(player.GridActor, hp);
        registry.TryGetVisual(attacker.GridActor, out var attackerVisual);
        registry.TryGetVisual(player.GridActor, out var playerVisual);
        int seen = 0;
        Action<PresentationEvent> started = ev =>
        {
            if (ev.TargetActor != player.GridActor) return;
            seen++;
            int expected = hp - seen;
            if (registry.GetPresentedHitPoint(player.GridActor, player.Health.CurrentHitPoint) != expected)
                callbackFailure = "피격 시작 HP 순서 불일치";
            if (!attackerVisual.IsFacingRight) callbackFailure = "공격 당시 오른쪽 표적 대신 미래 논리 위치를 바라봄";
            if (playerVisual.IsDeathPresentation) callbackFailure = "비치명타에서 조기 사망 연출";
        };
        combat.CombatPresentationStarted += started;
        for (int i = 0; i < 2; i++) Damage(queue, attacker.GridActor, player.GridActor, 1);
        Check(player.Health.CurrentHitPoint == hp - 2 && registry.GetPresentedHitPoint(player.GridActor, player.Health.CurrentHitPoint) == displayBefore,
            "두 타격 논리 확정 후에도 표시 HP는 이전 값 유지");
        Check(attacker.GridActor.TryMoveTo(new GridPosition(15, 5)), "공격 이후 미래 논리 위치는 표적 오른쪽으로 변경");
        queue.PlayQueuedEvents();
        deadline = EditorApplication.timeSinceStartup + 40;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        combat.CombatPresentationStarted -= started;
        Check(!queue.IsBusy && seen == 2 && callbackFailure == null, callbackFailure ?? "각 피격 시작에서 HP 한 단계씩 갱신하고 공격 당시 방향 재생");

        // 서로 다른 인식·조사 단계가 먼저 확정되어도 HUD는 자기 이벤트 순서에서 갱신한다.
        Set(attacker.AlertState, "currentState", EnemyAwarenessState.Suspicious);
        Check(attacker.AlertState.RequestUnaware(), "평상 복귀 논리 확정");
        Check(registry.GetPresentedEnemyState(attacker).awareness == EnemyAwarenessState.Alerted,
            "인식 상태 아이콘은 대기 중 미래 논리 상태를 표시하지 않음");
        queue.PlayQueuedEvents();
        deadline = EditorApplication.timeSinceStartup + 10;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(registry.GetPresentedEnemyState(attacker).awareness == EnemyAwarenessState.Unaware, "인식 이벤트 재생 후 아이콘 상태 반영");
        Set(attacker.AlertState, "currentState", EnemyAwarenessState.Suspicious);
        attacker.AlertState.SetSuspiciousPhase(SuspiciousBehaviorPhase.Searching);
        attacker.AlertState.SetSuspiciousPhase(SuspiciousBehaviorPhase.ReturningToRoutine);
        Check(registry.GetPresentedEnemyState(attacker).awareness == EnemyAwarenessState.Unaware, "조사 단계 연속 확정 중에도 이전 표시 유지");
        var queuedStates = ((Queue<PresentationEvent>)Get(queue, "eventQueue")).Where(e => e.Type == PresentationEventType.EnemyAwarenessChanged).ToArray();
        Check(queuedStates.Length == 2 && queuedStates[0].SuspiciousPhase == SuspiciousBehaviorPhase.Searching &&
            queuedStates[1].SuspiciousPhase == SuspiciousBehaviorPhase.ReturningToRoutine, "조사·복귀 단계가 서로 다른 스냅샷으로 보존됨");
        queue.PlayQueuedEvents();
        deadline = EditorApplication.timeSinceStartup + 10;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(registry.GetPresentedEnemyState(attacker).phase == SuspiciousBehaviorPhase.ReturningToRoutine, "조사 단계 연출 후 최종 표시 반영");

        // 생산 중 빈 큐도 완료로 취급하지 않으며, 결과 이벤트는 뒤에 추가된 확정 연출까지 기다린다.
        Check(queue.TryBeginProduction(), "논리 생산 구간 시작");
        queue.PlayQueuedEvents();
        yield return null;
        Check(queue.IsPlaying && queue.IsBusy, "생산 중 빈 큐는 입력 잠금과 소비 루프 유지");
        queue.Enqueue(PresentationEvent.StageCleared("결과 후순위 검증"));
        queue.Enqueue(PresentationEvent.CombatCameraRestore("결과보다 먼저 완료되어야 할 이벤트"));
        yield return null;
        Check(Get(resultPresenter, "pendingHandle") == null, "생산 중 결과 UI 선행 금지");
        queue.EndProduction();
        deadline = EditorApplication.timeSinceStartup + 30;
        while (Get(resultPresenter, "pendingHandle") == null && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(Get(resultPresenter, "pendingHandle") != null && queue.QueuedEventCount == 0, "기존 연출을 소진한 뒤 클리어 UI 표시");
        // 확인 클릭은 캠페인 씬 전환을 일으키므로 검증에서는 Presenter 비활성화로 핸들만 정리한다.
        resultPresenter.enabled = false;
        deadline = EditorApplication.timeSinceStartup + 10;
        while (queue.IsPlaying && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!queue.IsPlaying && Get(resultPresenter, "pendingHandle") == null && queue.IsBusy && queue.IsTurnPending,
            "결과 Presenter 정리 후에도 조정자 종료 전까지 입력 잠금 유지");
        queue.ReleaseTurnProcessing(); resultPresenter.enabled = true;

        // 치명타는 논리 사망으로 후속 표적에서 제외되지만, 화면 사망은 자기 연출 차례까지 미룬다.
        bool deathStarted = false;
        Action<PresentationEvent> death = ev =>
        {
            if (ev.TargetActor != player.GridActor) return;
            deathStarted = registry.GetPresentedHitPoint(player.GridActor, -1) == 0 && playerVisual.IsDeathPresentation;
        };
        combat.CombatPresentationStarted += death;
        Damage(queue, attacker.GridActor, player.GridActor, player.Health.CurrentHitPoint);
        Check(!player.IsAlive && new EnemyTargetProvider().Collect().Count == 0, "논리 사망 즉시 재공격 표적에서 제외");
        Check(!playerVisual.IsDeathPresentation && registry.GetPresentedHitPoint(player.GridActor, 0) > 0, "치명타 대기 중 조기 사망 표시 방지");
        queue.PlayQueuedEvents();
        deadline = EditorApplication.timeSinceStartup + 40;
        while (Get(resultPresenter, "pendingHandle") == null && EditorApplication.timeSinceStartup < deadline) yield return null;
        combat.CombatPresentationStarted -= death;
        Check(stage.IsFailed && deathStarted && Get(resultPresenter, "pendingHandle") != null && queue.QueuedEventCount == 0,
            "사망 피격 시작에서 HP 0·사망 표시 후 마지막에 실패 UI 표시");
        Check(!queue.HasFailed, "전체 실제 연출에 실행 오류 없음");

        // 예외 뒤 후속 논리를 실행하지 않으며 이미 확정된 연출은 보존한다.
        var probe = new ThrowingHandler();
        ActionLogicEventBus.Register(probe);
        try
        {
            int queuedBefore = queue.QueuedEventCount;
            var context = new ActionResolutionContext(queue);
            context.Publish(new ProbeEvent()); context.Publish(new ProbeEvent());
            context.Resolve();
            Check(context.HasFailed && queue.HasFailed && probe.Calls == 1 && queue.QueuedEventCount == queuedBefore,
                "의도한 논리 예외 후 후속 처리 정지 및 기존 연출 보존");
        }
        finally { ActionLogicEventBus.Unregister(probe); }
    }

    /// <summary>실제 피해 처리자를 통해 논리와 대응 연출을 함께 확정한다.</summary>
    private static void Damage(ActionPresentationQueue queue, GridActor attacker, GridActor target, int damage)
    {
        var context = new ActionResolutionContext(queue);
        context.Publish(new ApplyDamageLogicEvent(attacker, target, attacker.GridPosition, target.GridPosition, damage, AttackPresentationKind.EnemyMelee, "큐 검증 공격"));
        context.Resolve();
        Check(!context.HasFailed, "피해 논리 확정");
    }

    /// <summary>검증 중 런타임 복사본의 상태만 배치한다.</summary>
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    /// <summary>검증에 필요한 비공개 실행 상태만 읽는다.</summary>
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    /// <summary>예외 전파만 검증하는 독립 논리 이벤트다.</summary>
    private sealed class ProbeEvent : IActionLogicEvent { }
    /// <summary>후속 논리가 실행되지 않는지 확인하도록 첫 처리에서 예외를 낸다.</summary>
    private sealed class ThrowingHandler : IActionLogicEventHandler
    {
        // 예외가 발생한 뒤 두 번째 이벤트까지 실행되는지 확인하는 횟수다.
        public int Calls;
        /// <summary>검증 전용 이벤트만 처리한다.</summary>
        public bool CanHandle(IActionLogicEvent ev) => ev is ProbeEvent;
        /// <summary>의도한 예외를 발생시켜 문맥의 안전 정지를 확인한다.</summary>
        public void Handle(IActionLogicEvent ev, ActionResolutionContext context)
        {
            Calls++;
            throw new InvalidOperationException("큐 검증을 위한 의도한 예외");
        }
    }
    /// <summary>통과 항목을 기록하고 불일치면 검증을 종료한다.</summary>
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        results.Add("통과: " + label);
        File.WriteAllText(Output, "RUNNING\n" + string.Join("\n", results));
    }
}
