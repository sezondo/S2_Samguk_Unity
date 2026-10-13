using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>통제된 첫 플레이를 기존 행동·논리·연출 흐름으로 실행한다.</summary>
[DefaultExecutionOrder(-50)]
public sealed class BattleTutorialController : MonoBehaviour
{
    // 수정 가능한 단계와 씬 기능 참조다.
    [SerializeField] private BattleTutorialData data;
    [SerializeField] private BattleTutorialContext context;
    // 현재 씬에만 적용되는 튜토리얼 입력 정책이다. 다른 씬은 연결 없이 기존 규칙을 쓴다.
    public static BattleTutorialController Instance { get; private set; }
    // 현재 단계와 필수 행동·턴 전환 완료 상태다.
    private int stepIndex;
    private bool waitingAction, actionAccepted, waitingContinue, waitingTurn, internalTurnRequest;
    // 잘못된 참조나 행동 결과가 발생하면 입력 잠금을 유지한다.
    private bool failed;
    // 망치·큰못의 지정 공격에만 일회 적용할 판정이다.
    private bool enemyMissPending;
    // 스테이지에서 자동 소개한 수첩 페이지다. 직접 열람과 별개로 중복 소개를 막는다.
    private readonly HashSet<int> introducedManualPages = new();
    public int IntroducedManualPageCount => introducedManualPages.Count;
    // 실행 코루틴과 임시 대사 데이터다.
    private Coroutine routine, enemyRoutine;
    private DialogueSequenceData runtimeDialogue;

    public int StepIndex => stepIndex;
    public int PlayerTurnNumber { get; private set; }
    public BattleTutorialStep CurrentStep => data != null && data.steps != null && stepIndex < data.steps.Length ? data.steps[stepIndex] : null;
    public bool IsFreePlay { get; private set; }
    public bool HasFailed => failed;
    public BattleTutorialContext Context => context;
    public BattleTutorialData Data => data;
    public bool IsWaitingForAction => waitingAction && !actionAccepted && !failed;
    public bool IsWaitingForContinue => waitingContinue && !failed;
    public bool AllowsEndTurn => !failed && (internalTurnRequest || IsFreePlay || waitingTurn && context.turns.IsPlayerTurn);
    public bool AllowsGameplayInput => !failed && (IsFreePlay || IsWaitingForAction);

    /// <summary>초기화 직후부터 허용되지 않은 조작을 차단할 단일 진행자를 등록한다.</summary>
    private void Awake()
    {
        if (Instance != null) { Debug.LogError("튜토리얼 진행자가 중복 배치되어 있습니다.", this); enabled = false; return; }
        Instance = this;
    }

    /// <summary>씬 초기화와 캠페인 로딩이 끝난 뒤 첫 턴과 안내를 시작한다.</summary>
    private IEnumerator Start()
    {
        yield return null;
        var bootstrap = CampaignBootstrap.Instance;
        while (bootstrap != null && bootstrap.Context.SceneTransitionController.IsLoading) yield return null;
        if (!HasValidReference() || !HasValidData()) { Fail("튜토리얼 초기 구성을 확인하십시오."); yield break; }
        context.turns.TurnStarted += HandleTurnStarted;
        context.stage.StageStateChanged += HandleStageStateChanged;
        context.presenter.ContinueRequested += Continue;
        while (BattleMenuController.IsOpen) yield return null;
        context.turns.StartPlayerTurn();
        routine = StartCoroutine(RunSteps());
    }

    /// <summary>진행 중단과 씬 종료 시 구독과 임시 대사 데이터를 정리한다.</summary>
    private void OnDestroy()
    {
        if (context != null)
        {
            if (context.turns != null) context.turns.TurnStarted -= HandleTurnStarted;
            if (context.stage != null) context.stage.StageStateChanged -= HandleStageStateChanged;
            if (context.presenter != null) context.presenter.ContinueRequested -= Continue;
        }
        if (runtimeDialogue != null) Destroy(runtimeDialogue);
        if (Instance == this) Instance = null;
    }

    /// <summary>지정 행동 종류와 플레이어만 HUD·단축키 선택을 허용한다.</summary>
    public bool AllowsAction(BattleHudActionType action, TacticalUnitContext unit)
        => !failed && context != null && unit == context.player && (IsFreePlay || IsWaitingForAction && CurrentStep.action == action);

    /// <summary>필수 행동의 종류뿐 아니라 목표 칸까지 검사한다.</summary>
    public bool AllowsTarget(BattleHudActionType action, TacticalUnitContext unit, GridPosition target)
        => AllowsAction(action, unit) && (IsFreePlay || action == BattleHudActionType.SwordRecall || target == CurrentStep.target);

    /// <summary>실제로 승인된 행동만 완료 대기 상태로 바꿔 중복 요청을 막는다.</summary>
    public void NotifyActionExecuted(BattleHudActionType action, GridPosition target)
    {
        if (!IsFreePlay && IsWaitingForAction && CurrentStep.action == action)
        {
            actionAccepted = true;
            context.presenter.HideGuidance();
        }
    }

    /// <summary>두 총격의 확정 명중과 첫 적 공격의 확정 회피만 적용한다.</summary>
    public bool TryGetForcedHit(ResolveAttackLogicEvent attack, out bool isHit)
    {
        isHit = false;
        if (failed || IsFreePlay) return false;
        if (enemyMissPending && attack.Attacker == context.bigNail.GridActor && attack.Target == context.player.GridActor)
        { enemyMissPending = false; return true; }
        if (waitingAction && CurrentStep.action == BattleHudActionType.Gun && attack.Attacker == context.player.GridActor && attack.TargetPosition == CurrentStep.target)
        { isHit = true; return true; }
        return false;
    }

    /// <summary>대사는 먼저 타이핑을 완성하고 다음 입력에 넘긴다. 안내는 한 번에 닫는다.</summary>
    public void Continue()
    {
        // 수첩 안내의 확인은 창을 닫는 경로로 통일한다. 일반 메뉴는 대사를 넘기지 않는다.
        if (BattleMenuController.IsOpen)
        {
            if (BattleMenuController.Instance.IsTutorialGuide) BattleMenuController.Instance.Close();
            return;
        }
        if (!IsWaitingForContinue) return;
        if (context.dialogue.IsPlaying) context.dialogue.Advance();
        else waitingContinue = false;
    }

    /// <summary>대사·안내·행동·턴을 순서대로 실행하며 연출 완료 뒤에만 다음 단계로 넘어간다.</summary>
    private IEnumerator RunSteps()
    {
        for (stepIndex = 0; stepIndex < data.steps.Length && !failed && context.stage.IsPlaying; stepIndex++)
        {
            while (BattleMenuController.IsOpen) yield return null;
            context.presenter.HideGuidance();
            var step = CurrentStep;
            context.player.CancelAllActionSelections();
            switch (step.kind)
            {
                case BattleTutorialStepKind.Dialogue:
                    waitingContinue = true;
                    context.presenter.EnableDialogueInput();
                    runtimeDialogue = ScriptableObject.CreateInstance<DialogueSequenceData>();
                    runtimeDialogue.name = step.id;
                    runtimeDialogue.steps = new[] { new DialogueStepData { lines = new[] { new DialogueLineData { speakerTag = step.speaker, text = step.text, charactersPerSecond = 36 } } } };
                    if (!context.dialogue.TryPlay(runtimeDialogue, context.speakers)) { Fail("말풍선 대사를 시작하지 못했습니다."); yield break; }
                    while (context.dialogue.IsPlaying && !failed) yield return null;
                    waitingContinue = false;
                    Destroy(runtimeDialogue); runtimeDialogue = null;
                    context.presenter.DisableDialogueInput();
                    break;
                case BattleTutorialStepKind.Guide:
                    if (!introducedManualPages.Add(step.manualPage)) break;
                    waitingContinue = true;
                    context.presenter.ShowGuide(step.manualPage, step.text);
                    while (waitingContinue && !failed) yield return null;
                    context.presenter.DisableDialogueInput();
                    break;
                case BattleTutorialStepKind.Camera:
                    context.presenter.ShowMarker(step.target);
                    yield return MoveCamera(step.target);
                    break;
                case BattleTutorialStepKind.Action:
                    var focus = step.action == BattleHudActionType.SwordThrow ? step.target : context.player.GridActor.GridPosition;
                    yield return MoveCamera(focus);
                    actionAccepted = false; waitingAction = true;
                    context.control.TrySelectUnit(context.player);
                    context.presenter.ShowActionGuidance(step.action, step.target);
                    while (!actionAccepted && !failed) yield return null;
                    waitingAction = false;
                    yield return WaitForQueue();
                    if (!HasExpectedResult(step)) { Fail($"{step.id}의 결과가 기획과 다릅니다."); yield break; }
                    break;
                case BattleTutorialStepKind.EndTurn:
                    waitingTurn = true;
                    if (step.turn == data.firstCombatTurn || step.turn == data.secondCombatTurn)
                    {
                        internalTurnRequest = true;
                        context.turns.EndCurrentTurn();
                        internalTurnRequest = false;
                    }
                    if (waitingTurn && !internalTurnRequest && context.turns.IsPlayerTurn) context.presenter.ShowEndTurnGuidance();
                    while (waitingTurn && !failed) yield return null;
                    context.presenter.HideGuidance();
                    break;
                case BattleTutorialStepKind.FreePlay:
                    IsFreePlay = true;
                    context.control.TrySelectUnit(context.player);
                    context.presenter.ShowMarker(context.goal.GoalPosition);
                    yield break;
            }
        }
        routine = null;
    }

    /// <summary>자동 AP·그리드 변경으로 단계를 건너뛰지 않고 기존 행동 결과를 확인한다.</summary>
    private bool HasExpectedResult(BattleTutorialStep step)
    {
        switch (step.action)
        {
            case BattleHudActionType.Move: return context.player.GridActor.GridPosition == step.target;
            case BattleHudActionType.SwordRecall: return context.player.SwordState.IsRecalled;
            case BattleHudActionType.SwordThrow: return context.player.SwordState.CurrentPosition == step.target;
            case BattleHudActionType.Hack: return context.terminal.IsHacked && context.door.IsOpen;
            default: return true;
        }
    }

    /// <summary>턴 번호를 갱신하고 일반 AI 대신 지정된 튜토리얼 적 턴을 실행한다.</summary>
    private void HandleTurnStarted(TurnSide side)
    {
        context.presenter.HideGuidance();
        if (side == TurnSide.Player) { PlayerTurnNumber++; waitingTurn = false; }
        else if (enemyRoutine == null) enemyRoutine = StartCoroutine(RunEnemyTurn());
    }

    /// <summary>첫 순찰·확정 회피·망치 접근 공격을 기존 논리 이벤트와 연출로 처리한다.</summary>
    private IEnumerator RunEnemyTurn()
    {
        yield return null;
        yield return WaitForQueue();
        var resolution = new ActionResolutionContext(context.queue);
        if (PlayerTurnNumber == 1)
        {
            if (!MoveEnemy(context.patrol, data.patrolDestination, 6, resolution)) yield break;
        }
        else if (PlayerTurnNumber == data.firstCombatTurn)
        {
            enemyMissPending = true;
            context.bigNail.ActionPoint.RefillForTurn();
            if (!context.bigNail.AttackAction.TryExecuteAttack(context.player.GridActor, resolution)) { Fail("큰못의 회피 학습 공격을 실행하지 못했습니다."); yield break; }
            context.bigNail.ActionPoint.TrySpend(context.bigNail.EnemyData.Combat.rangedCost);
        }
        else if (PlayerTurnNumber == data.secondCombatTurn)
        {
            context.hammer.ActionPoint.RefillForTurn();
            if (!MoveEnemy(context.hammer, data.hammerAttackPosition, 3, resolution)) yield break;
            context.hammer.ActionPoint.TrySpend(context.hammer.EnemyData.Combat.moveCost);
            if (!context.hammer.AttackAction.TryExecuteAttack(context.player.GridActor, resolution, EnemyPlannedActionKind.Melee)) { Fail("망치의 근접 공격을 실행하지 못했습니다."); yield break; }
            context.hammer.ActionPoint.TrySpend(context.hammer.EnemyData.Combat.meleeCost);
        }
        resolution.Resolve();
        if (resolution.HasFailed) { Fail("튜토리얼 적 턴의 논리 처리가 실패했습니다."); yield break; }
        context.queue.PlayQueuedEvents();
        yield return WaitForQueue();
        enemyMissPending = false;
        // 메뉴가 열린 마지막 프레임에도 적 턴 종료 요청을 잃지 않고 재개를 기다린다.
        while (BattleMenuController.IsOpen && !failed && context.stage.IsPlaying) yield return null;
        enemyRoutine = null;
        if (!failed && context.stage.IsPlaying) context.turns.EndCurrentTurn();
    }

    /// <summary>적의 이동도 경로를 검증한 뒤 기존 이동 논리와 연출을 사용한다.</summary>
    private bool MoveEnemy(EnemyContext enemy, GridPosition target, int maximumDistance, ActionResolutionContext resolution)
    {
        var path = new List<GridPosition>();
        if (!GridPathfinder.TryFindPath(context.grid, enemy.GridActor.GridPosition, target, maximumDistance, path) ||
            EnemyMovementUtility.MoveAlongPath(enemy, path, resolution, "튜토리얼 지정 적 이동") != path.Count)
        { Fail($"{enemy.name}의 지정 이동 경로를 실행하지 못했습니다."); return false; }
        return true;
    }

    /// <summary>공격·이동·발각·문 개방 연출을 모두 마칠 때까지 진행을 잠근다.</summary>
    private IEnumerator WaitForQueue()
    {
        while (context.queue.IsBusy && !failed)
        {
            if (context.queue.HasFailed) { Fail("연출 큐가 실패해 튜토리얼을 중단합니다."); yield break; }
            yield return null;
        }
    }

    /// <summary>맵 소개와 목표 행동 전에 부드럽게 카메라를 이동한다.</summary>
    private IEnumerator MoveCamera(GridPosition target)
    {
        var from = context.worldCamera.transform.position;
        var to = context.grid.GridToWorld(target); to.z = from.z;
        float elapsed = 0;
        while (elapsed < 0.55f && !failed)
        {
            if (BattleMenuController.IsOpen) { yield return null; continue; }
            elapsed += Time.unscaledDeltaTime;
            context.worldCamera.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / 0.55f));
            yield return null;
        }
        context.worldCamera.transform.position = to;
    }

    /// <summary>승리·실패 시 안내와 말풍선을 정리하고 추가 진행을 중지한다.</summary>
    private void HandleStageStateChanged(StageState previous, StageState next)
    {
        if (next == StageState.Playing) return;
        if (routine != null) StopCoroutine(routine);
        if (enemyRoutine != null) StopCoroutine(enemyRoutine);
        context.dialogue.Stop(); context.presenter.HideAll();
    }

    /// <summary>예상 밖 결과는 진행을 중단하고 오류 로그에 원인을 남긴다.</summary>
    private void Fail(string reason)
    {
        failed = true;
        context?.presenter?.HideGuidance();
        Debug.LogError($"1-1 튜토리얼: {reason}", this);
    }

    /// <summary>진행에 필요한 모든 기능은 Context의 명시적 연결로 검사한다.</summary>
    public bool HasValidReference()
    {
        bool valid = data != null && context != null && context.grid != null && context.player != null &&
            context.patrol != null && context.bigNail != null && context.hammer != null && context.terminal != null &&
            context.door != null && context.goal != null && context.turns != null && context.stage != null &&
            context.queue != null && context.control != null && context.actions != null && context.worldCamera != null && context.spotlight != null && context.selectedHud != null && context.endTurnButton != null &&
            context.dialogue != null && context.speakers != null && context.speakers.Length == 3 && context.presenter != null;
        if (!valid) Debug.LogError("1-1 튜토리얼 Context에 필수 참조가 없습니다.", this);
        return valid;
    }

    /// <summary>빈 단계·중복 ID·범위 밖 목표와 AP 전제를 검사한다.</summary>
    public bool HasValidData()
    {
        if (data.steps == null || data.steps.Length == 0 || context.player.UnitData.StartTurnActionPoint != 3) return false;
        var ids = new HashSet<string>();
        foreach (var step in data.steps)
            if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id) ||
                string.IsNullOrWhiteSpace(step.text) || !context.grid.IsInside(step.target) ||
                (step.kind == BattleTutorialStepKind.Guide && (step.manualPage < 0 || step.manualPage > 4))) return false;
        return true;
    }
}
