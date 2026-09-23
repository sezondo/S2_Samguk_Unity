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
public static class CharacterUiVerification
{
    // 도메인 리로드 이후 실행 요청과 검증 결과를 보관한다.
    private const string Pending = "S2.CharacterUI.Verify";
    private const string Output = "Temp/CharacterUI/verification.txt";
    private static readonly List<string> results = new();
    private static IEnumerator routine;
    private static double nextStep;
    // 이벤트 콜백 안에서 예외를 던져 실제 연출을 중단하지 않고 결과만 수집한다.
    private static string callbackFailure;

    /// <summary>플레이 진입 후 실제 씬 검증을 시작한다.</summary>
    static CharacterUiVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>편집 씬을 보존한 채 검증용 플레이를 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Character UI")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "BattleTest01" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태에서 실행해야 합니다.");
        Directory.CreateDirectory("Temp/CharacterUI");
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
        if (exception == null) Debug.Log($"캐릭터 UI 검증 {results.Count}개 통과");
        else Debug.LogError("캐릭터 UI 검증 실패: " + exception);
        EditorApplication.isPlaying = false;
    }

    /// <summary>그리드 표시·발 높이·수색 색상·발각 점멸·카메라 잔류를 실제 플레이에서 확인한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        var queue = ActionPresentationQueue.Instance;
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(!queue.IsBusy, "시작 연출 완료");
        var registry = ActorPresentationRegistry.Instance;
        var grid = GridManager.Instance;
        var overlay = UnityEngine.Object.FindFirstObjectByType<BattleHudTacticalOverlayController>();
        var camera = (Camera)Get(overlay, "worldCamera");
        var canvas = (Canvas)Get(overlay, "hudCanvas");
        var selected = (UnityEngine.UI.Image)Get(overlay, "selectedUnitBracket");
        var icons = (Dictionary<EnemyContext, UnityEngine.UI.Image>)Get(overlay, "enemyStateIcons");
        var enemies = EnemyRegistry.Instance.Enemies.Where(e => e.transform.root.name == "천하회_배치").ToArray();
        Check(enemies.Length == 3, "천하회 3명 확인");
        var player = PlayerUnitControlManager.Instance.ActiveUnit;
        Check(registry.TryGetVisual(player.GridActor, out var playerVisual), "선택 유닛 비주얼 연결");
        float playerFeet = playerVisual.GroundWorldPosition.y - playerVisual.transform.position.y;
        foreach (var enemy in enemies)
        {
            Check(registry.TryGetVisual(enemy.GridActor, out var visual), enemy.transform.parent.name + " 비주얼 연결");
            Check(Mathf.Abs(visual.GroundWorldPosition.y - visual.transform.position.y - playerFeet - .1f) < .001f,
                enemy.transform.parent.name + " 사용자 화면 기준 추가 높이 보정 유지");
            Check(visual.TargetRenderer.transform.parent == visual.transform, enemy.transform.parent.name + " 그림 위치만 자식에서 보정");
            Check(Mathf.Abs(visual.transform.lossyScale.x - .45f) < .001f, enemy.transform.parent.name + " 기존 크기 유지");
            visual.SetVisionAlpha(1);
        }
        Check(!UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(r=>r.name.Contains("TemporaryRing")), "노란 임시 링 없음");
        foreach (float zoom in new[]{3.5f, 8f})
        {
            camera.orthographicSize = zoom;
            overlay.Refresh();
            Vector3 center = playerVisual.transform.position - playerVisual.CoverWorldOffset;
            Vector2 expected = camera.WorldToScreenPoint(center);
            Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, selected.rectTransform.position);
            Check(Vector2.Distance(expected, actual) < 1f, "줌 " + zoom + " 선택 표시 중앙 일치");
            float cellPixels = camera.WorldToScreenPoint(center+Vector3.right*grid.CellSize).x - expected.x;
            Check(Mathf.Abs(selected.rectTransform.rect.width * canvas.scaleFactor - cellPixels) < 1f,
                "줌 " + zoom + " 선택 표시 한 칸 크기");
        }
        var source = enemies.First(e=>e.transform.parent.name.Contains("큰못"));
        registry.TryGetVisual(source.GridActor, out var sourceVisual);
        camera.orthographicSize = 3.5f;
        camera.transform.position = new Vector3(sourceVisual.transform.position.x, sourceVisual.transform.position.y, camera.transform.position.z);
        foreach (var enemy in enemies)
        {
            Check(enemy.AlertState.RequestSuspicion(default), enemy.transform.parent.name + " 수색 요청");
            enemy.AlertState.SetSuspiciousPhase(SuspiciousBehaviorPhase.Searching);
        }
        queue.PlayQueuedEvents();
        deadline=EditorApplication.timeSinceStartup+8;
        while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline) yield return null;
        Check(!queue.IsBusy,"수색 표시 큐 완료");
        overlay.Refresh();
        foreach (var enemy in enemies)
        {
            registry.TryGetVisual(enemy.GridActor, out var visual);
            CheckWhite(visual,enemy.transform.parent.name+" 수색 중 원본 색 유지");
            Check(icons[enemy].enabled,"수색 눈 활성화 "+enemy.name);
            Vector2 head = camera.WorldToScreenPoint(visual.HeadWorldPosition);
            Vector2 icon = RectTransformUtility.WorldToScreenPoint(null,icons[enemy].rectTransform.position);
            Check(icon.y - icons[enemy].rectTransform.rect.height*canvas.scaleFactor*.5f >= head.y+7,
                enemy.transform.parent.name+" 눈이 머리 기준점 위에 위치");
        }
        ScreenCapture.CaptureScreenshot("Temp/CharacterUI/search.png");
        for(int i=0;i<12;i++) yield return null;
        // 수색 진입 이벤트를 실제 큐로 재생해 HUD의 켜짐·꺼짐과 종료 후 상태를 확인한다.
        var assets = (BattleHudAssetSet)Get(overlay, "assetSet");
        queue.Enqueue(PresentationEvent.SuspicionDetected(source.GridActor.GridPosition, source));
        queue.PlayQueuedEvents();
        int searchLitSegments = 0;
        int searchDarkSegments = 0;
        bool searchPreviouslyLit = false;
        deadline = EditorApplication.timeSinceStartup + 8;
        do
        {
            overlay.Refresh();
            bool flashing = registry.IsAlertIconFlashing(source.GridActor);
            bool lit = registry.IsAlertIconVisible(source.GridActor);
            if (flashing)
            {
                if (lit && !searchPreviouslyLit) searchLitSegments++;
                if (!lit && searchPreviouslyLit) searchDarkSegments++;
                if (icons[source].enabled != lit) throw new Exception("수색 점멸과 실제 HUD 표시 불일치");
                if (lit && icons[source].sprite != assets.EnemyInvestigating)
                    throw new Exception("수색 점멸에 주황 눈 이외의 그림 사용");
                CheckWhite(sourceVisual, "수색 점멸 중 원본 색 유지", false);
            }
            searchPreviouslyLit = lit;
            yield return null;
        } while (queue.IsBusy && EditorApplication.timeSinceStartup < deadline);
        Check(!queue.IsBusy, "수색 점멸 큐 완료");
        Check(searchLitSegments == 3 && searchDarkSegments == 3, "주황 눈 켜짐·꺼짐 각각 3회");
        overlay.Refresh();
        Check(icons[source].enabled && !registry.IsAlertIconFlashing(source.GridActor), "수색 점멸 종료 후 주황 눈 유지");
        Check(source.AlertState.IsSuspicious, "수색 점멸이 논리 인식 상태를 바꾸지 않음");
        // 실제 애드 전파 경로를 실행한다. 테스트 중 논리 반응 이동도 정상 처리한다.
        Vector3 focus = grid.GridToWorld(source.GridActor.GridPosition);
        float zoomBefore = camera.orthographicSize;
        camera.transform.position += new Vector3(3,2,0);
        var context = new ActionResolutionContext(queue);
        context.Publish(new AlertTriggeredLogicEvent(player.GridActor.GridPosition,source,source.GridSight));
        context.Resolve();
        Check(!context.HasFailed,"실제 발각 전파 논리 완료");
        queue.PlayQueuedEvents();
        int litSegments=0; bool previouslyLit=false; bool captured=false;
        deadline=EditorApplication.timeSinceStartup+30;
        do
        {
            bool lit=registry.IsAlertIconVisible(source.GridActor);
            if(lit && !previouslyLit) litSegments++;
            previouslyLit=lit;
            CheckWhite(sourceVisual,"발각 진행 중 원본 색 유지",false);
            if(lit&&!captured)
            {
                overlay.Refresh();
                Check(icons[source].enabled,"발각 점멸 켜짐 구간의 빨간 눈 활성화");
                ScreenCapture.CaptureScreenshot("Temp/CharacterUI/alert.png"); captured=true;
            }
            yield return null;
        } while(queue.IsBusy && EditorApplication.timeSinceStartup<deadline);
        Check(!queue.IsBusy,"발각과 반응 연출 큐 완료");
        Check(litSegments==3,"빨간 눈 3회 점멸: "+litSegments);
        overlay.Refresh();
        Check(!icons[source].enabled&&!registry.IsAlertIconVisible(source.GridActor),"발각 종료 후 빨간 눈 숨김");
        Check(Vector2.Distance(camera.transform.position,focus)<.001f,"카메라가 최초 발각 적 위치에 도착");
        Check(Mathf.Abs(camera.orthographicSize-zoomBefore)<.001f,"발각 시 기존 줌 유지");
        for(int i=0;i<15;i++)yield return null;
        Check(Vector2.Distance(camera.transform.position,focus)<.001f,"큐 종료 뒤 카메라 자동 복귀 없음");
        ScreenCapture.CaptureScreenshot("Temp/CharacterUI/after-alert.png");
        for(int i=0;i<10;i++)yield return null;
    }

    /// <summary>RGB만 검사해 전장의 가시성 알파는 방해하지 않는다.</summary>
    private static void CheckWhite(ActorVisualController visual,string message,bool record=true)
    {
        Color c=visual.TargetRenderer.color;
        bool white=Mathf.Abs(c.r-1)<.001f&&Mathf.Abs(c.g-1)<.001f&&Mathf.Abs(c.b-1)<.001f;
        if(record)Check(white,message);else if(!white)throw new Exception(message);
    }
    /// <summary>검증용으로 연결된 상태만 읽는다.</summary>
    private static object Get(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    /// <summary>검사 결과를 기록하고 실패 시 중단한다.</summary>
    private static void Check(bool condition,string message)
    {
        if(!condition)throw new Exception(message);
        results.Add("PASS "+message);
    }
}




