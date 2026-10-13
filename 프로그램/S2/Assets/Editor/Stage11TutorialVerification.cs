using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장 씬의 플레이 사본에서 실제 튜토리얼과 탈출을 끝까지 검사한다.</summary>
[InitializeOnLoad]
public static class Stage11TutorialVerification
{
    // 도메인 재로드를 넘겨 실행 요청을 보관한다.
    private const string Pending = "S2.Stage11Tutorial.Verify";
    private const string Folder = "Temp/Stage11Tutorial";
    // 프레임마다 진행할 검사와 결과 목록이다.
    private static IEnumerator routine;
    private static readonly List<string> results = new();
    private static double deadline;
    private static AttackRecorder recorder;

    /// <summary>플레이 진입과 중단 이벤트를 연결한다.</summary>
    static Stage11TutorialVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>깨끗한 1-1 씬에서만 검증을 시작하며 플레이 변경을 저장하지 않는다.</summary>
    [MenuItem("Tools/S2/Verify 1-1 Tutorial Draft")]
    public static void Verify()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty) throw new InvalidOperationException("저장된 BattleTest01 편집 상태가 필요합니다.");
        EditorSceneManager.OpenScene(scene.path);
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder+"/verification.txt", "RUNNING " + DateTime.Now);
        SessionState.SetBool(Pending,true); EditorApplication.isPlaying = true;
    }

    /// <summary>재생 중 검사를 예약하고 사용자가 중단하면 남은 검사도 해제한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if(mode==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
        { SessionState.SetBool(Pending,false); results.Clear(); deadline=EditorApplication.timeSinceStartup+300; routine=Run(); EditorApplication.update+=Tick; }
        else if(mode==PlayModeStateChange.ExitingPlayMode && routine!=null) Finish(new InvalidOperationException("검증 재생이 중단되었습니다."));
    }

    /// <summary>제한 시간 안에 실제 플레이 루프와 함께 검사한다.</summary>
    private static void Tick()
    {
        if(routine==null)return;
        EditorApplication.QueuePlayerLoopUpdate();
        try { if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("튜토리얼 검증 시간 초과"); if(routine.MoveNext())return; Finish(null); }
        catch(Exception e){Finish(e);}
    }

    /// <summary>결과와 실패 시점 화면을 남기고 편집 모드로 복귀한다.</summary>
    private static void Finish(Exception error)
    {
        EditorApplication.update-=Tick; (routine as IDisposable)?.Dispose(); routine=null;
        if(recorder!=null)ActionLogicEventBus.Unregister(recorder); recorder=null;
        File.WriteAllText(Folder+"/verification.txt",(error==null?"PASS":"FAIL "+error)+"\n"+string.Join("\n",results));
        if(error==null)Debug.Log($"1-1 튜토리얼 전체 진행 검사 {results.Count}개 통과"); else Debug.LogError("1-1 튜토리얼 검증 실패: "+error);
        EditorApplication.isPlaying=false;
    }

    /// <summary>모든 안내·행동·적 턴·해킹을 실행하고 자유 탈출로 실제 승리를 확인한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground=true;
        var t=UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();
        Check(t!=null,"튜토리얼 씬 연결");
        while(t.PlayerTurnNumber==0)yield return null;
        var c=t.Context; recorder=new AttackRecorder(); ActionLogicEventBus.Register(recorder);
        Check(t.HasValidReference()&&t.HasValidData(),"필수 참조·진행 데이터");
        Check(t.Data.steps[0].kind==BattleTutorialStepKind.Dialogue && !BattleMenuController.IsOpen,"시작 직후 수첩 대신 대사");
        Check(c.presenter.transform.Find("Instruction")==null,"상단 검은 지시창 제거");
        int firstAction=Array.FindIndex(t.Data.steps,x=>x.kind==BattleTutorialStepKind.Action);
        Check(!t.Data.steps.Take(firstAction).Any(x=>x.kind==BattleTutorialStepKind.Guide),"첫 이동 전 자동 수첩 없음");
        var maskCanvas=c.spotlight.transform.parent.GetComponent<Canvas>();
        Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x=>x!=maskCanvas).All(x=>x.sortingOrder<maskCanvas.sortingOrder),"가림막이 메뉴 아이콘까지 덮는 최상단");
        Check(c.presenter.transform.Find("Continue")==null,"기존 하단 대화 진행 버튼 제거");
        Check(c.player.GridActor.GridPosition==new GridPosition(23,14),"원래 플레이어 시작점 유지");
        Check(!c.door.IsOpen,"시작 시 보안문 닫힘");
        var visited=new HashSet<int>(); var manualPages=new HashSet<int>(); int actions=0;
        while(!t.IsFreePlay)
        {
            if(t.HasFailed)throw new InvalidOperationException("튜토리얼 진행 실패 "+t.StepIndex);
            var step=t.CurrentStep;
            if(t.IsWaitingForContinue)
            {
                int turn=t.PlayerTurnNumber; int hp=c.player.Health.CurrentHitPoint; int ap=c.player.ActionPoint.Current;
                c.turns.EndCurrentTurn();
                Check(c.turns.IsPlayerTurn && t.PlayerTurnNumber==turn,"대사·안내 중 턴 종료 차단 "+step.id);
                Check(!c.actions.TryExecuteMove(new GridPosition(24,14))&&c.player.ActionPoint.Current==ap&&c.player.Health.CurrentHitPoint==hp,"안내 중 행동 차단 "+step.id);
                Check(!c.spotlight.gameObject.activeSelf,"대사·수첩 중 가림막 해제 "+step.id);
                if(step.kind==BattleTutorialStepKind.Guide)
                    Check(manualPages.Add(step.manualPage),"수첩 페이지 자동 안내는 최초 1회 "+step.manualPage);
                if(step.kind==BattleTutorialStepKind.Dialogue && visited.Add(t.StepIndex))
                {
                    t.Continue(); yield return null;
                    // 타이핑 첫 입력과 전체 문장 표시를 별도로 확인한다.
                    var bubble=UnityEngine.Object.FindFirstObjectByType<SpeechBubbleView>();
                    Check(bubble!=null,"대화 말풍선 활성화 "+step.id);
                    Check(bubble.IsTextFullyVisible,"첫 대화 입력으로 타이핑 완료 "+step.id);
                    var bubbleData=new SerializedObject(bubble);
                    var bodyImage=(UnityEngine.UI.Image)bubbleData.FindProperty("bubbleImage").objectReferenceValue;
                    var tailImage=(UnityEngine.UI.Image)bubbleData.FindProperty("tailImage").objectReferenceValue;
                    Check(bodyImage.sprite!=null&&bodyImage.type==UnityEngine.UI.Image.Type.Sliced&&bodyImage.color==Color.white,"흰색 9슬라이스 몸통 "+step.id);
                    Check(tailImage!=null&&tailImage.sprite!=null&&!tailImage.raycastTarget,"화자 꼬리 연결·클릭 통과 "+step.id);
                    yield return null;
                    var texts=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(x=>x.gameObject.name=="Dialogue"&&x.gameObject.activeInHierarchy).ToArray();
                    Check(texts.Length==1 && texts[0].text.Contains(step.text),"말풍선 본문 표시 "+step.id);
                    Check(texts[0].textInfo.characterCount>=step.text.Length && texts[0].maxVisibleCharacters>=texts[0].textInfo.characterCount,"긴 대사 전체 글자 공개 "+step.id);
                    Check(!texts[0].isTextOverflowing,"말풍선 줄바꿈·높이 "+step.id);
                    Check(texts[0].color.r<.3f&&texts[0].color.g<.3f,"흰 바탕의 짙은 본문 색 "+step.id);
                    if(t.StepIndex==0||step.speaker=="천하회 큰못"||step.text.Contains("무력화가"))
                    { ScreenCapture.CaptureScreenshot(Folder+"/dialogue-"+t.StepIndex+".png"); yield return Pause(.2); }
                }
                t.Continue(); yield return null; continue;
            }
            if(t.IsWaitingForAction)
            {
                var unit=c.player; int before=unit.ActionPoint.Current;
                Check(c.control.ActiveUnit==unit,"지정 유진 선택 "+step.id);
                Check(!c.actions.TryExecuteMove(new GridPosition(0,0))&&unit.ActionPoint.Current==before,"잘못된 칸·행동 실행 차단 "+step.id);
                int expectedCost=1;
                if(step.action==BattleHudActionType.Move)
                {
                    var path=new List<GridPosition>();
                    Check(GridPathfinder.TryFindPath(c.grid,unit.GridActor.GridPosition,step.target,9,path),"실제 이동 경로 "+step.id);
                    expectedCost=unit.GridMoveAction.CalculateMoveActionPointCost(path.Count);
                }
                Check(before>=expectedCost,"필수 행동 AP 충분 "+step.id);
                yield return null;
                Check(c.spotlight.gameObject.activeSelf && c.presenter.CurrentFocus==BattleTutorialPresenter.FocusKind.ActionButton,"행동 선택 전 버튼만 노출 "+step.id);
                CheckSpotlightInput(c,"버튼 "+step.id);
                if(actions==0)
                {
                    yield return null;
                    var moveButton=UnityEngine.Object.FindObjectsByType<BattleHudActionButtonView>(FindObjectsSortMode.None).First(x=>x.gameObject.activeInHierarchy&&x.ActionType==BattleHudActionType.Move);
                    Check(moveButton.HasTutorialHint,"첫 이동 버튼 청록 강조");
                    Check(!BattleMenuController.IsOpen,"첫 행동을 수첩이 가리지 않음");
                    ScreenCapture.CaptureScreenshot(Folder+"/opening-first-move.png"); yield return Pause(.2);
                }
                if(actions==0)
                {
                    var menu=UnityEngine.Object.FindFirstObjectByType<BattleMenuController>();
                    menu.OpenMenu(); yield return null;
                    Check(!c.spotlight.gameObject.activeSelf,"메뉴 중 가림막 해제");
                    menu.Close(); yield return null;
                    Check(c.spotlight.gameObject.activeSelf,"메뉴 닫은 뒤 가림막 복원");
                }
                SelectAction(c,step.action); yield return null;
                if(step.action!=BattleHudActionType.SwordRecall)
                {
                    Check(c.presenter.CurrentFocus==BattleTutorialPresenter.FocusKind.TargetCell,"행동 선택 후 목표만 노출 "+step.id);
                    CheckSpotlightInput(c,"목표 "+step.id);
                    if(actions==0)
                    {
                        ScreenCapture.CaptureScreenshot(Folder+"/opening-target-spotlight.png"); yield return Pause(.2);
                        c.player.CancelAllActionSelections(); yield return null;
                        Check(c.presenter.CurrentFocus==BattleTutorialPresenter.FocusKind.ActionButton,"선택 취소 후 버튼 노출 복원");
                        SelectAction(c,step.action); yield return null;
                    }
                }
                Check(Execute(c,step),"기존 행동 실행 성공 "+step.id);
                Check(!c.spotlight.gameObject.activeSelf,"행동 실행 즉시 가림막 해제 "+step.id);
                Check(unit.ActionPoint.Current==before-expectedCost,"실제 AP 소모 "+step.id);
                Check(!Execute(c,step),"같은 단계 중복 실행 차단 "+step.id);
                actions++;
                while(c.queue.IsBusy)yield return null;
                if(step.action==BattleHudActionType.Move||step.action==BattleHudActionType.SwordThrow||step.action==BattleHudActionType.Hack)
                {ScreenCapture.CaptureScreenshot(Folder+"/action-"+t.StepIndex+".png");yield return Pause(.15);}
                if(step.id.Contains("첫 총격"))Check(c.bigNail.Health.CurrentHitPoint==3,"첫 총격 확정 명중 피해 2");
                if(step.id.Contains("검 근접"))Check(!c.bigNail.IsAlive,"검 근접 피해 3으로 첫 적 처치");
                if(step.id.Contains("검 정찰 2"))Check(PlayerVisionManager.Instance.IsVisible(c.hammer.GridActor.GridPosition)&&!c.hammer.AlertState.IsAlerted,$"검 시야로 망치 발견·발각 없음: 검 {unit.SwordState.CurrentPosition}, 망치 {c.hammer.GridActor.GridPosition}, 가시 {PlayerVisionManager.Instance.IsVisible(c.hammer.GridActor.GridPosition)}, 경계 {c.hammer.AlertState.CurrentState}, 기하 {PlayerVisionGeometry.CanObserve(c.grid, unit.SwordState.CurrentPosition,c.hammer.GridActor.GridPosition,unit.UnitData.SwordVisionRange,false)}");
                if(step.id.Contains("두 번째 접근"))Check(!c.hammer.AlertState.IsAlerted,"대기 칸에서 망치에게 발각되지 않음");
                if(step.id.Contains("공격용 검"))Check(c.hammer.Health.CurrentHitPoint==1&&!unit.SwordState.IsRecalled,"투척 피해 2·맨손 마무리 준비");
                if(step.id.Contains("맨손 마무리"))Check(!c.hammer.IsAlive,"맨손 피해 1로 망치 처치");
                if(step.action==BattleHudActionType.Hack)Check(c.terminal.IsHacked&&c.door.IsOpen,"실제 해킹·문 개방 완료");
                yield return null; continue;
            }
            if(step.kind==BattleTutorialStepKind.EndTurn && t.AllowsEndTurn && c.turns.IsPlayerTurn)
            {
                yield return null;
                Check(c.presenter.CurrentFocus==BattleTutorialPresenter.FocusKind.EndTurnButton,"수동 턴 종료 버튼만 노출 "+step.id);
                CheckSpotlightInput(c,"턴 종료 "+step.id);
                if(t.PlayerTurnNumber==1) { ScreenCapture.CaptureScreenshot(Folder+"/opening-endturn-spotlight.png"); yield return Pause(.2); }
                int turn=t.PlayerTurnNumber;
                if(turn==1)Check(PlayerVisionManager.Instance.IsVisible(c.patrol.GridActor.GridPosition)&&!c.patrol.AlertState.IsAlerted,"코너에서 순찰 적 발견·미발각");
                c.turns.EndCurrentTurn();
                while(t.PlayerTurnNumber==turn&&!t.HasFailed)yield return null;
                if(turn==1)Check(!PlayerVisionManager.Instance.IsVisible(c.patrol.GridActor.GridPosition),"순찰 적이 시야 밖으로 사라짐");
                if(turn==3)Check(c.player.Health.CurrentHitPoint==c.player.Health.MaxHitPoint,"첫 적 공격 확정 회피");
                if(turn==t.Data.secondCombatTurn)Check(c.player.Health.CurrentHitPoint==c.player.Health.MaxHitPoint-2&&CombatTargetRules.IsMeleeAdjacent(c.player.GridActor.GridPosition,c.hammer.GridActor.GridPosition),"망치 접근·근접 피해 2·주먹 거리");
            }
            yield return null;
        }
        Check(manualPages.Count==4&&t.IntroducedManualPageCount==4,"시야·전투·검·침입 자동 수첩 각 1회");
        Check(!c.spotlight.gameObject.activeSelf,"자유 조작 가림막 해제");
        Check(t.PlayerTurnNumber==10,"10턴부터 자유 조작");
        Check(actions==17,"지정 행동 17개 완료");
        Check(recorder.results.Count==3&&recorder.results.Count(x=>x.IsHit)==2,"튜토리얼 전용 총격 명중 2회·회피 1회");
        Check(recorder.results[0].CoverResult.HasCover && recorder.results[0].CoverResult.CoverPosition==new GridPosition(33,15),"첫 적 공격에 기존 배관 엄폐 적용");
        while(c.stage.IsPlaying)
        {
            var path=new List<GridPosition>();
            Check(GridPathfinder.TryFindPath(c.grid,c.player.GridActor.GridPosition,c.goal.GoalPosition,80,path),"열린 문을 지나는 탈출 경로");
            int distance=Math.Min(path.Count,c.player.ActionPoint.Current*3);
            if(distance==0){c.turns.EndCurrentTurn();yield return null;continue;}
            c.control.TrySelectUnit(c.player); c.player.GridMoveAction.SelectMoveAction();
            Check(c.actions.TryExecuteMove(path[distance-1]),"자유 이동 승인");
            // 클리어 연출은 결과 창 확인을 기다리므로 승리 확정 뒤에는 큐를 기다리지 않는다.
            while(c.queue.IsBusy&&c.stage.IsPlaying)yield return null;
            if(c.stage.IsPlaying){c.turns.EndCurrentTurn();while(!c.turns.IsPlayerTurn)yield return null;}
        }
        Check(c.stage.CurrentState==StageState.Cleared,"승강기 도착으로 실제 스테이지 클리어");
        // 결과 창은 사용자의 확인을 기다린다. 창 표시까지는 확인하되 다음 씬으로 넘기지 않는다.
        var resultPresenter=UnityEngine.Object.FindFirstObjectByType<StageResultPresenter>();
        var resultRoot=(GameObject)new SerializedObject(resultPresenter).FindProperty("resultRoot").objectReferenceValue;
        while(!resultRoot.activeInHierarchy)yield return null;
        Check(resultRoot.activeInHierarchy,"이동 연출 완료 후 클리어 결과 창 표시");
        yield return Pause(.2);
        ScreenCapture.CaptureScreenshot(Folder+"/cleared.png"); yield return Pause(.4);
    }

    /// <summary>실제 조작과 같은 선택 함수·행동 흐름을 사용한다.</summary>
    private static bool Execute(BattleTutorialContext c,BattleTutorialStep step)
    {
        switch(step.action)
        {
            case BattleHudActionType.Move:c.player.GridMoveAction.SelectMoveAction();return c.actions.TryExecuteMove(step.target);
            case BattleHudActionType.Gun:c.player.GunAttackAction.SelectGunAttackAction();return c.actions.TryExecuteGunAttack(step.target);
            case BattleHudActionType.Melee:c.player.MeleeAttackAction.SelectMeleeAttackAction();return c.actions.TryExecuteMeleeAttack(step.target);
            case BattleHudActionType.SwordThrow:c.player.SwordThrowAction.SelectSwordThrowAction();return c.actions.TryExecuteSwordThrow(step.target);
            case BattleHudActionType.SwordRecall:return c.actions.TryExecuteSwordRecall();
            case BattleHudActionType.Hack:c.player.HackAction.SelectHackAction();return c.actions.TryExecuteHack(c.terminal);
            default:return false;
        }
    }

    /// <summary>가려진 영역의 실제 UI 레이캐스트 차단과 구멍의 통과를 검사한다.</summary>
    private static void CheckSpotlightInput(BattleTutorialContext c,string stage)
    {
        Vector2 exposed=c.spotlight.rectTransform.TransformPoint(c.spotlight.Aperture.center);
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        var hits=new List<UnityEngine.EventSystems.RaycastResult>();
        pointer.position=exposed; UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.All(x=>x.gameObject!=c.spotlight.gameObject),"노출 영역 포인터 통과 "+stage);
        hits.Clear(); pointer.position=new Vector2(12,12); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Any(x=>x.gameObject==c.spotlight.gameObject),"검은 영역 포인터 차단 "+stage);
        Check(c.spotlight.color==new Color(0,0,0,.7f),"가림막 검정 불투명도 70% "+stage);
    }

    /// <summary>플레이어가 누르는 행동 선택 단계를 실행 판정과 분리해 검사한다.</summary>
    private static void SelectAction(BattleTutorialContext c,BattleHudActionType action)
    {
        switch(action)
        {
            case BattleHudActionType.Move:c.player.GridMoveAction.SelectMoveAction();break;
            case BattleHudActionType.Gun:c.player.GunAttackAction.SelectGunAttackAction();break;
            case BattleHudActionType.Melee:c.player.MeleeAttackAction.SelectMeleeAttackAction();break;
            case BattleHudActionType.SwordThrow:c.player.SwordThrowAction.SelectSwordThrowAction();break;
            case BattleHudActionType.Hack:c.player.HackAction.SelectHackAction();break;
        }
    }

    /// <summary>스크린샷을 저장할 때 실제 화면이 그려질 때까지 기다린다.</summary>
    private static IEnumerator Pause(double seconds){double end=EditorApplication.timeSinceStartup+seconds;while(EditorApplication.timeSinceStartup<end)yield return null;}

    /// <summary>실패한 조건을 즉시 기록한다.</summary>
    private static void Check(bool condition,string description){if(!condition)throw new InvalidOperationException(description);results.Add(description);}

    /// <summary>실제 공격 판정 이벤트를 기록한다.</summary>
    private sealed class AttackRecorder:IActionLogicEventHandler
    {
        // 게임 상태를 변경하지 않고 실제 명중 결과만 검사한다.
        public readonly List<AttackResult> results=new();
        public bool CanHandle(IActionLogicEvent e)=>e is AttackResolvedLogicEvent;
        public void Handle(IActionLogicEvent e,ActionResolutionContext context)=>results.Add(((AttackResolvedLogicEvent)e).Result);
    }
}

