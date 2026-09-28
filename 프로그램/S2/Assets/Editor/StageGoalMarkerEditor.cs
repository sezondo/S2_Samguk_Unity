using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>승인된 1-1 목표 효과를 설치하고 저장 씬의 플레이 사본에서 검증한다.</summary>
[InitializeOnLoad]
public static class StageGoalMarkerEditor
{
    // 목표 효과 에셋과 검증 결과 경로다.
    private const string Folder = "Assets/Art/Vfx/Goal";
    private const string Output = "Temp/GoalMarker";
    private const string Pending = "S2.GoalMarker.Verify";
    // 프레임별 검증 실행과 결과를 보관한다.
    private static IEnumerator routine;
    private static readonly List<string> results = new();

    /// <summary>도메인 재로드 뒤 플레이 진입 검증을 연결한다.</summary>
    static StageGoalMarkerEditor() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>실제 씬의 안개 위 렌더링과 위치·논리 불변을 검사한다.</summary>
    [MenuItem("Tools/S2/Verify Goal Marker")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "BattleTest01" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("저장된 BattleTest01 편집 상태에서 검증해야 합니다.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/verification.txt", "RUNNING");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>플레이 초기화 뒤 프레임 기반 검증을 시작한다.</summary>
    private static void OnMode(PlayModeStateChange mode)
    {
        if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        results.Clear();
        routine = Run();
        EditorApplication.update += Tick;
    }

    /// <summary>검증을 진행하고 성공·실패 모두 플레이 사본을 폐기한다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("검증 중 플레이가 종료되었습니다.");
            if (routine.MoveNext()) return;
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>결과를 저장하고 편집 상태로 돌아간다.</summary>
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        File.WriteAllText(Output + "/verification.txt", (error == null ? "PASS" : "FAIL " + error) + "\n" + string.Join("\n", results));
        if (error == null) Debug.Log($"목표 효과 검사 {results.Count}개 통과");
        else Debug.LogError("목표 효과 검사 실패: " + error);
        EditorApplication.isPlaying = false;
    }

    /// <summary>안개를 공개하지 않고 목표를 촬영하며 밝기 주기와 비활성화를 검사한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        // Start에서 예약되는 입장 연출보다 검사가 먼저 끝나지 않도록 초기 프레임을 기다린다.
        double startup = EditorApplication.timeSinceStartup + 8;
        while (EditorApplication.timeSinceStartup < startup) yield return null;
        double deadline = EditorApplication.timeSinceStartup + 40;
        while ((ActionPresentationQueue.Instance == null || ActionPresentationQueue.Instance.IsBusy) && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(ActionPresentationQueue.Instance != null && !ActionPresentationQueue.Instance.IsBusy, "입장 연출 완료");
        var presenter = UnityEngine.Object.FindFirstObjectByType<StageGoalMarkerPresenter>();
        var goal = UnityEngine.Object.FindFirstObjectByType<StageGoal>();
        var vision = PlayerVisionPresenter.Instance;
        var renderer = presenter.GetComponent<MeshRenderer>();
        var grid = goal.GridManager;
        var snapshot = vision.PresentedSnapshot;
        var position = goal.GoalPosition;
        Check(presenter.HasValidReference() && presenter.HasValidData(), "목표 효과 필수 참조와 수치");
        Check(Vector3.Distance(renderer.transform.position, grid.GridToWorld(position)) < .0001f, "목표 칸 중심 일치");
        Check(Mathf.Abs(renderer.bounds.size.x / StageGoalMarkerPresenter.MeshCellRatio - grid.CellSize) < .0001f, "한 칸 경계 크기 일치");
        Check(renderer.sortingLayerName == vision.FogSortingLayerName && renderer.sortingOrder > vision.FogSortingOrder, "안개 위 정렬");
        Check(!snapshot.IsVisible(position), "목표 칸은 현재 시야 밖");
        Check(!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), "목표 셰이더 컴파일");
        Check(Mathf.Abs(renderer.sharedMaterial.GetFloat("_PulsePeriod")-2.8f)<.001f, "밝기 주기 2.8초");
        Check(Mathf.Abs(renderer.sharedMaterial.GetFloat("_ParticlePeriod")-3.4f)<.001f, "입자 주기 3.4초");
        var camera = Camera.main;
        Check(camera != null, "검증 카메라 존재");
        foreach (var input in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None)) input.enabled = false;
        foreach (var input in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None)) input.enabled = false;
        camera.transform.position = new Vector3(renderer.transform.position.x,renderer.transform.position.y,camera.transform.position.z);
        camera.orthographicSize = 2.5f;
        var block = new MaterialPropertyBlock();
        for (int sample=0;sample<2;sample++)
        {
            block.SetFloat("_PreviewTime",sample*1.4f);renderer.SetPropertyBlock(block);
            double until=EditorApplication.timeSinceStartup+.5;
            while(EditorApplication.timeSinceStartup<until) yield return null;
            ScreenCapture.CaptureScreenshot(Output+(sample==0?"/dim.png":"/bright.png"));
            until=EditorApplication.timeSinceStartup+.5;
            while(EditorApplication.timeSinceStartup<until) yield return null;
        }
        renderer.SetPropertyBlock(null);
        Check(ReferenceEquals(snapshot,vision.PresentedSnapshot) && !vision.PresentedSnapshot.IsVisible(position), "시야 스냅샷·비공개 상태 불변");
        Check(goal.GoalPosition == position, "목표 판정 좌표 불변");
        presenter.enabled=false;
        Check(!renderer.enabled,"컴포넌트 비활성 시 표시 정리");
        presenter.enabled=true;
        Check(renderer.enabled,"재활성 시 표시 복구");
    }

    /// <summary>각 검증 항목의 성공을 기록하거나 실패를 알린다.</summary>
    private static void Check(bool valid, string description)
    {
        if (!valid) throw new InvalidOperationException(description);
        results.Add("PASS " + description);
    }
}
