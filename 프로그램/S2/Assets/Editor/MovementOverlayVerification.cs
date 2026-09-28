using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장 씬의 플레이 사본에서 이동 UI와 새 발각 경고 회귀를 검사한다.</summary>
[InitializeOnLoad]
public static class MovementOverlayVerification
{
    // 재로드 이후 검증 요청과 결과를 보관하는 키·경로다.
    private const string Pending = "S2.Movement.Verify";
    private const string Output = "Temp/MovementOverlay";
    // 프레임 기반 검증 진행 상태와 결과 목록이다.
    private static IEnumerator routine;
    private static readonly List<string> results = new();

    /// <summary>플레이 진입 이후 검증 루틴을 연결한다.</summary>
    static MovementOverlayVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>저장된 씬을 다시 읽고 플레이 사본에서 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Movement Overlay")]
    public static void Verify()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "BattleTest01" || scene.isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태에서 검증해야 합니다.");
        EditorSceneManager.OpenScene(scene.path);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/verification.txt", "RUNNING");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>초기화가 끝난 플레이 사본에서 검사를 예약한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false); results.Clear(); routine = Run();
        EditorApplication.update += Tick;
    }

    /// <summary>검증을 진행하고 성공·실패 모두 편집 상태로 돌아간다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        try { if (routine.MoveNext()) return; Finish(null); }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>검증 결과를 저장하며 플레이에서 바꾼 상태는 폐기한다.</summary>
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Output + "/verification.txt", (error == null ? "PASS" : "FAIL " + error) + "\n" + string.Join("\n", results));
        if (error == null) Debug.Log($"이동 UI 검사 {results.Count}개 통과");
        else Debug.LogError("이동 UI 검사 실패: " + error);
        EditorApplication.isPlaying = false;
    }

    /// <summary>실제 경로 비용·경계·안개·발각 조건·취소 정리를 검사하고 결과 화면을 촬영한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        double until = EditorApplication.timeSinceStartup + 8;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        until = EditorApplication.timeSinceStartup + 40;
        while (ActionPresentationQueue.Instance.IsBusy && EditorApplication.timeSinceStartup < until) yield return null;
        Check(!ActionPresentationQueue.Instance.IsBusy, "입장 연출 완료");
        var unit = PlayerUnitControlManager.Instance.ActiveUnit;
        Check(unit != null && unit.GridMoveRangeHighlighter.enabled, "실제 선택 유닛의 이동 표시 연결");
        var move = unit.GridMoveAction;
        var grid = GridManager.Instance;
        foreach (var input in UnityEngine.Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None)) input.enabled = false;
        foreach (var input in UnityEngine.Object.FindObjectsByType<PlayerUnitInputController>(FindObjectsSortMode.None)) input.enabled = false;
        move.SelectMoveAction();
        Check(move.IsMoveSelected, "이동 행동 선택");
        int step = move.MoveDistancePerActionPoint;
        Check(move.CalculateMoveActionPointCost(step) == 1 && move.CalculateMoveActionPointCost(step + 1) == 2 && move.CalculateMoveActionPointCost(step * 2 + 1) == 3, "3·6·9칸 AP 경계 비용");
        var distances = new Dictionary<GridPosition, int>();
        GridPathfinder.FindReachablePositionDistances(grid, unit.GridActor.GridPosition, move.MoveRange, distances);
        Check(distances.Values.Any(d => d > step * 2), "실제 지형에서 3 AP 경로 존재");
        for (int tier = 1; tier <= 3; tier++)
        {
            var target = distances.First(p => p.Value > (tier - 1) * step && p.Value <= tier * step).Key;
            move.RefreshPathPreview(target);
            Check(move.CalculateMoveActionPointCost(move.PreviewPath.Count) == tier, tier + " AP 경로 미리보기 일치");
        }
        var meshObject = GameObject.Find(unit.name + "_APBoundaries");
        Check(meshObject != null && meshObject.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0, "AP 경계 메시 생성");
        Check(meshObject.GetComponent<MeshRenderer>().sortingOrder < PlayerVisionPresenter.Instance.FogSortingOrder, "이동 표시는 안개 아래");
        Check(!ShaderUtil.ShaderHasError(meshObject.GetComponent<MeshRenderer>().sharedMaterial.shader), "선 셰이더 컴파일");
        Check(!UnityEngine.Object.FindObjectsByType<GridCellHighlighter>(FindObjectsSortMode.None).Any(h => h.name.Contains("RangeHighlighter") || h.name.Contains("PathPreviewHighlighter") || h.name.Contains("WarningHighlighter")), "기존 임시 이동 사각형 제거");

        // 플레이 사본의 감지 집합만 통제해 기존 발각·신규 적·안개 조건을 분리 검증한다.
        var enemies = EnemyRegistry.Instance.Enemies.ToArray();
        var visible = (HashSet<GridPosition>)Get(PlayerVisionManager.Instance, "visiblePositions");
        var savedVisible = visible.ToArray();
        var savedStates = enemies.Select(e => e.AlertState.CurrentState).ToArray();
        var savedSights = enemies.Select(e => ((HashSet<GridPosition>)Get(e.GridSight, "detectedPositionSet")).ToArray()).ToArray();
        var point = distances.First().Key;
        var sample = new[] { point };
        try
        {
            for (int i = 0; i < enemies.Length; i++)
            {
                Set(enemies[i].AlertState, "currentState", EnemyAwarenessState.Alerted);
                ((HashSet<GridPosition>)Get(enemies[i].GridSight, "detectedPositionSet")).Clear();
            }
            var enemy = enemies[0];
            visible.Add(enemy.GridActor.GridPosition);
            ((HashSet<GridPosition>)Get(enemy.GridSight, "detectedPositionSet")).Add(point);
            Check(unit.GridMoveRiskEvaluator.FindFirstPreviewRiskIndex(sample) == -1, "이미 발각된 적의 예상 경고 숨김");
            Set(enemy.AlertState, "currentState", EnemyAwarenessState.Unaware);
            Check(unit.GridMoveRiskEvaluator.FindFirstPreviewRiskIndex(sample) == 0, "새 미발각 적의 위험 경고 유지");
            Set(enemy.AlertState, "currentState", EnemyAwarenessState.Suspicious);
            Check(unit.GridMoveRiskEvaluator.FindFirstPreviewRiskIndex(sample) == 0, "의심 상태에서 새 발각 경고 유지");
            visible.Remove(enemy.GridActor.GridPosition);
            Check(unit.GridMoveRiskEvaluator.FindFirstPreviewRiskIndex(sample) == -1, "안개 밖 적의 감지 범위 비공개");
        }
        finally
        {
            visible.Clear(); visible.UnionWith(savedVisible);
            for (int i = 0; i < enemies.Length; i++)
            {
                Set(enemies[i].AlertState, "currentState", savedStates[i]);
                var set = (HashSet<GridPosition>)Get(enemies[i].GridSight, "detectedPositionSet");
                set.Clear(); set.UnionWith(savedSights[i]);
            }
        }
        var best = distances.Where(p => p.Value == step + 1 && PlayerVisionManager.Instance.IsVisible(p.Key))
            .OrderBy(p => p.Key.y).First().Key;
        move.RefreshPathPreview(best);
        // 카메라 변경은 플레이 사본에만 적용한다.
        var camera = Camera.main;
        foreach (var input in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None)) input.enabled = false;
        foreach (var input in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None)) input.enabled = false;
        camera.transform.position = grid.GridToWorld(unit.GridActor.GridPosition) + new Vector3(1, -1, -10);
        camera.orthographicSize = 6;
        until = EditorApplication.timeSinceStartup + .5;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        var overlay = UnityEngine.Object.FindFirstObjectByType<BattleHudTacticalOverlayController>();
        UnityEngine.Object.FindFirstObjectByType<BattleHudController>().enabled = false;
        overlay.enabled = false;
        move.RefreshPathPreview(best);
        Check(GameObject.Find("MoveAPPreview") == null, "이동 AP 팝업 제거");
        ScreenCapture.CaptureScreenshot(Output + "/movement.png");
        until = EditorApplication.timeSinceStartup + .7;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        unit.ActionPoint.TrySpend(2);
        yield return null; yield return null;
        Check(move.MoveRange == step, "AP 소비 후 범위 축소");
        move.RefreshPathPreview(best);
        Check(move.PreviewPath.Count == 0, "AP 부족 도착 칸의 경로 제거");
        move.CancelMoveAction();
        Check(meshObject.GetComponent<MeshFilter>().sharedMesh.vertexCount == 0, "취소 시 경계 정리");
        Check(GameObject.Find(unit.name + "_MoveRoute").GetComponent<MeshFilter>().sharedMesh.vertexCount == 0, "취소 시 경로 정리");
        move.SelectMoveAction();
        GridPosition adjacent = distances.First(p => p.Value == 1).Key;
        move.RefreshPathPreview(adjacent);
        int expectedCost = move.CalculateMoveActionPointCost(move.PreviewPath.Count);
        int before = unit.ActionPoint.Current;
        Check(move.TryExecuteMoveTo(adjacent, new ActionResolutionContext(ActionPresentationQueue.Instance)), "실제 이동 실행");
        Check(unit.ActionPoint.Current == before - expectedCost && unit.GridActor.GridPosition == adjacent, "미리보기와 실제 AP 소비·도착 위치 일치");
    }

    /// <summary>검증용 사본의 비공개 상태를 읽는다.</summary>
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    /// <summary>검증용 사본의 상태만 변경한다.</summary>
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    /// <summary>실패 조건을 즉시 알리고 통과 결과를 누적한다.</summary>
    private static void Check(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException(label);
        results.Add("PASS " + label);
    }
}
