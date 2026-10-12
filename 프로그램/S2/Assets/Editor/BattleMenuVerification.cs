using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>실제 플레이에서 수첩·버튼·입력 잠금·종료를 검사하고 화면을 저장한다.</summary>
[InitializeOnLoad]
public static class BattleMenuVerification
{
    // 재생 전환을 넘겨 검사 요청과 종료 확인을 저장한다.
    private const string Pending = "S2.Menu.Verify", QuitPending = "S2.Menu.VerifyQuit";
    private const string Folder = "Temp/BattleMenu";
    // 편집기 프레임에서 진행할 검사와 결과 목록이다.
    private static IEnumerator routine;
    private static readonly List<string> results = new();
    private static double deadline;

    /// <summary>재생 진입과 실제 종료에 따른 편집 모드 복귀를 관찰한다.</summary>
    static BattleMenuVerification() => EditorApplication.playModeStateChanged += Mode;

    /// <summary>저장 씬의 플레이 사본에서만 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Battle Manual and Menu")]
    public static void Verify()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 1-1 편집 씬이 필요합니다.");
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "/verification.txt", "RUNNING");
        SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
    }

    /// <summary>도메인 재로드 뒤 검사와 종료 결과 기록을 재개한다.</summary>
    private static void Mode(PlayModeStateChange mode)
    {
        if (mode == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false); results.Clear(); deadline = EditorApplication.timeSinceStartup + 120;
            routine = Run(); EditorApplication.update += Tick;
        }
        if (mode == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(QuitPending, false))
        {
            SessionState.SetBool(QuitPending, false);
            File.AppendAllText(Folder + "/verification.txt", "\nPASS 실제 종료 버튼으로 재생 중단·편집 모드 복귀");
            Debug.Log("수첩·메뉴 검사 및 실제 종료 버튼 동작이 통과했습니다.");
        }
    }

    /// <summary>실제 플레이 프레임을 진행하며 제한 시간과 실패를 기록한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("메뉴 검사 시간 초과");
            if (routine.MoveNext()) return;
            EditorApplication.update -= Tick; routine = null;
        }
        catch (Exception e)
        {
            EditorApplication.update -= Tick; routine = null; SessionState.SetBool(QuitPending, false);
            File.WriteAllText(Folder + "/verification.txt", "FAIL " + e + "\n" + string.Join("\n", results));
            Debug.LogError("수첩·메뉴 검사 실패: " + e); EditorApplication.isPlaying = false;
        }
    }

    /// <summary>수첩 조립, 페이지 넘김, 입력 격리와 창 복귀를 검사한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        var c = UnityEngine.Object.FindFirstObjectByType<BattleMenuContext>();
        var tutorial = UnityEngine.Object.FindFirstObjectByType<BattleTutorialController>();
        while (tutorial.PlayerTurnNumber==0) yield return null;
        Check(!BattleMenuController.IsOpen,"시작 자동 수첩 제거");
        // 첫 안내가 순찰 이후로 옮겨졌으므로 대사·첫 이동·적 턴을 실제 규칙으로 진행한다.
        while (!c.controller.IsTutorialGuide)
        {
            if (tutorial.HasFailed) throw new InvalidOperationException("첫 안내까지 진행 실패");
            if (tutorial.IsWaitingForContinue) tutorial.Continue();
            else if (tutorial.IsWaitingForAction)
            {
                if (tutorial.CurrentStep.action != BattleHudActionType.Move) throw new InvalidOperationException("첫 안내 전 예상하지 않은 행동");
                tutorial.Context.player.GridMoveAction.SelectMoveAction();
                if (!tutorial.Context.actions.TryExecuteMove(tutorial.CurrentStep.target)) throw new InvalidOperationException("첫 안내 전 이동 실패");
                while (tutorial.Context.queue.IsBusy) yield return null;
            }
            else if (tutorial.AllowsEndTurn && tutorial.Context.turns.IsPlayerTurn) tutorial.Context.turns.EndCurrentTurn();
            yield return null;
        }
        Check(tutorial.PlayerTurnNumber==2,"순찰 이후에 첫 자동 수첩 안내");
        Check(c.controller.HasValidReference() && c.manualView.HasValidReference(), "모든 메뉴·수첩 참조");
        Check(((RectTransform)c.manualOpen.transform).anchoredPosition.y == -24 && ((RectTransform)c.menuOpen.transform).anchoredPosition.y == -24, "수첩·메뉴 우상단 배치");
        Check(c.manualOpen.transform.Find("수첩 이름") == null && c.menuOpen.transform.Find("문구") == null && c.menuOpen.transform.Find("톱니바퀴") != null, "이름 제거·톱니바퀴 메뉴");
        var cards = UnityEngine.Object.FindFirstObjectByType<BattleHudUnitCardPanelController>();
        var cardRoot = (RectTransform)new SerializedObject(cards).FindProperty("unitCardRoot").objectReferenceValue;
        Check(cardRoot.anchoredPosition.y == -160, "유진 상태창 아래 배치");
        var menuCanvas = c.canvasRect.GetComponent<Canvas>();
        Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(x => x != menuCanvas).All(x => x.sortingOrder < menuCanvas.sortingOrder), "메뉴가 기존 HUD·말풍선보다 앞에 표시됨");
        Check(Time.timeScale == 0 && BattleMenuController.BlocksInput, "튜토리얼 안내 정지·입력 잠금");
        int step = tutorial.StepIndex; var position = tutorial.Context.player.GridActor.GridPosition;
        tutorial.Context.turns.EndCurrentTurn();
        Check(!tutorial.Context.actions.TryExecuteMove(new GridPosition(24, 14)), "수첩 뒤 이동 요청 차단");
        Check(tutorial.StepIndex == step && tutorial.Context.player.GridActor.GridPosition == position, "팝업 중 진행·위치 유지");
        ScreenCapture.CaptureScreenshot(Folder + "/manual-01.png"); yield return Pause(.5);
        for (int i = 0; i < 5; i++)
        {
            if (i != c.manualView.Page)
            {
                int before = c.manualView.Page;
                c.manualView.TurnTo(i);
                Check(c.manualView.IsTurning && !c.manualClose.gameObject.activeSelf, "넘김 중 덮기 버튼 숨김");
                Check(c.pages[before].activeSelf && c.pages[i].activeSelf, "다음 펼침 미리 표시");
                Check(c.pages[i].transform.GetSiblingIndex() < c.pages[before].transform.GetSiblingIndex(), "목적 페이지 아래 배치");
                Check(c.tabs.All(x => !x.interactable), "넘김 중 색인 잠금");
                c.controller.Close();
                Check(c.controller.CurrentWindow == BattleMenuController.Window.Manual && tutorial.StepIndex == step, "넘김 중 닫기·확인 방지");
                yield return Pause(.14);
                ScreenCapture.CaptureScreenshot(Folder + "/turn-forward-" + i + ".png");
                while (c.manualView.IsTurning) yield return null;
                Check(c.manualClose.gameObject.activeSelf && c.manualClose.interactable, "넘김 후 덮기 복원");
            }
            yield return Pause(.2);
            Check(c.manualClose.targetGraphic.canvasRenderer.GetColor().r > .7f, "덮기 버튼 활성 색 복원");
            Check(c.manualView.Page == i && c.pages.Count(x => x.activeSelf) == 1, "수첩 페이지 " + (i + 1));
            foreach (var text in c.pages[i].GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Check(!text.isTextOverflowing, "문구 영역 " + (i + 1) + ": " + text.name);
            }
            ScreenCapture.CaptureScreenshot(Folder + "/manual-0" + (i + 1) + ".png"); yield return Pause(.25);
        }
        c.previous.onClick.Invoke();
        Check(c.pages[3].transform.GetSiblingIndex() < c.pages[4].transform.GetSiblingIndex(), "이전 페이지 아래 배치");
        Check(c.pages[4].activeSelf && c.pages[3].activeSelf, "역방향 다음 펼침 준비");
        yield return Pause(.14);
        ScreenCapture.CaptureScreenshot(Folder + "/turn-backward.png");
        // 편집기 검사 주기와 렌더링 주기는 다르므로 실제 종이의 펼침 상태를 기다린다.
        while (c.manualView.IsTurning && (c.rightPages[3].localScale.x < .01f || c.leftPages[4].localScale.x > .001f)) yield return null;
        Check(c.manualView.IsTurning, "역방향 펼침 단계 관찰");
        Check(c.pages[3].transform.GetSiblingIndex() > c.pages[4].transform.GetSiblingIndex(), "이전 종이 펼칠 때 위 배치");
        ScreenCapture.CaptureScreenshot(Folder + "/turn-backward-unfold.png");
        while (c.manualView.IsTurning) yield return null;
        Check(c.manualView.Page == 3 && c.manualClose.gameObject.activeSelf, "역방향 넘김 완료");
        c.manualView.TurnTo(0);
        yield return Pause(.12);
        c.manualView.Open(2, c.guideText.text);
        Check(!c.manualView.IsTurning && c.pages.Count(x => x.activeSelf) == 1 && c.leftPages.All(x => x.localScale == Vector3.one) && c.rightPages.All(x => x.localScale == Vector3.one), "넘김 중 재열기 복원");
        foreach (var text in c.pages[2].GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "재열기 문구 " + text.name); }
        c.manualClose.onClick.Invoke(); yield return Pause(.1);
        Check(!BattleMenuController.IsOpen && Time.timeScale == 1 && tutorial.StepIndex != step, "안내 덮기 후 튜토리얼 재개");
        c.menuOpen.onClick.Invoke();
        Check(c.controller.CurrentWindow == BattleMenuController.Window.Menu && !c.settings.interactable, "메뉴 열기·설정 준비 중");
        ScreenCapture.CaptureScreenshot(Folder + "/menu.png"); yield return Pause(.3);
        int pausedStep = tutorial.StepIndex;
        tutorial.Continue();
        Check(tutorial.StepIndex == pausedStep, "일반 메뉴에서 대사 진행 차단");
        c.menuManual.onClick.Invoke(); Check(c.controller.CurrentWindow == BattleMenuController.Window.Manual, "메뉴에서 매뉴얼 열기");
        c.manualClose.onClick.Invoke(); Check(c.controller.CurrentWindow == BattleMenuController.Window.Menu && Time.timeScale == 0, "수첩에서 메뉴로 복귀");
        c.menuQuit.onClick.Invoke();
        Check(EventSystem.current.currentSelectedGameObject == c.quitCancel.gameObject, "종료 확인 기본 선택 취소");
        ScreenCapture.CaptureScreenshot(Folder + "/quit.png"); yield return Pause(.3);
        c.quitCancel.onClick.Invoke(); Check(c.controller.CurrentWindow == BattleMenuController.Window.Menu, "종료 취소 후 메뉴 복귀");
        c.menuClose.onClick.Invoke(); yield return Pause(.1);
        c.manualOpen.onClick.Invoke(); Check(c.controller.CurrentWindow == BattleMenuController.Window.Manual, "HUD 수첩 버튼");
        c.manualClose.onClick.Invoke(); yield return Pause(.1);
        Check(!BattleMenuController.IsOpen && Time.timeScale == 1, "HUD 수첩 덮기 후 게임 복귀");
        ScreenCapture.CaptureScreenshot(Folder + "/hud-layout.png"); yield return Pause(.3);
        c.menuOpen.onClick.Invoke(); yield return Pause(.1);
        var button = c.menuManual;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        button.OnPointerEnter(pointer); button.OnPointerDown(pointer);
        Check(button.transform.localScale.x < 1, "버튼 누름 상태");
        ScreenCapture.CaptureScreenshot(Folder + "/button-pressed.png"); yield return Pause(.2);
        button.OnPointerUp(pointer); button.OnPointerExit(pointer);
        Check(button.transform.localScale == Vector3.one, "버튼 뗌·포인터 이탈 복원");
        c.menuQuit.onClick.Invoke();
        File.WriteAllText(Folder + "/verification.txt", "PASS " + results.Count + "\n" + string.Join("\n", results));
        SessionState.SetBool(QuitPending, true);
        c.quitAccept.onClick.Invoke();
        yield break;
    }

    /// <summary>화면 렌더링과 입력 프레임 진행을 기다린다.</summary>
    private static IEnumerator Pause(double seconds) { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) yield return null; }
    /// <summary>실패 위치를 바로 드러내고 성공 조건을 기록한다.</summary>
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); results.Add(message); }
}

