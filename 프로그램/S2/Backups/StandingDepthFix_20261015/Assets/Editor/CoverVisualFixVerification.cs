using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>BattleTest01의 실제 엄폐·이동·전투 Presenter를 플레이 모드에서 검증한다. 테스트 변경은 저장하지 않는다.</summary>
[InitializeOnLoad]
public static class CoverVisualFixVerification
{
    /// <summary>검증한 씬에서 자동 그림자 메시 차이만 제거한 파일을 편집기 안에서 즉시 다시 연다.</summary>
    [MenuItem("Tools/S2/Load Clean Cover Fix Scene")]
    public static void LoadCleanScene()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 변경이 있습니다.");
        const string scenePath = "Assets/Scenes/Test/BattleTest01.unity";
        File.Copy("Temp/CoverVisualFix/BattleTest01.cleaned.unity", scenePath, true);
        EditorSceneManager.OpenScene(scenePath);
        AssetDatabase.ImportAsset(scenePath);
    }
    // 도메인 리로드 이후에도 검증 시작 요청을 전달하는 키다.
    private const string PendingKey = "S2.CoverVisualFixVerification.Pending";
    // 검사 결과와 현재 실행 중인 단계다.
    private static readonly List<string> results = new();
    private static IEnumerator routine;
    // 프레임 및 연출 완료를 기다리는 편집기 시간이다.
    private static double nextStep;

    /// <summary>플레이 전환 후 검증 루틴을 연결한다.</summary>
    static CoverVisualFixVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }


    /// <summary>실제 낮은 엄폐 소품과 배관 담장에 정렬 참조를 연결한다. 기존 설정은 보존한다.</summary>
    [MenuItem("Tools/S2/Configure Cover Prop Depth")]
    public static void Configure()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("씬의 미저장 변경을 먼저 처리해야 합니다.");
        var units = UnityEngine.Object.FindObjectsByType<TacticalUnitRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var visuals = UnityEngine.Object.FindObjectsByType<ActorPresentationRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (units.Length != 1 || visuals.Length != 1) throw new InvalidOperationException("등록소가 각각 하나여야 합니다.");
        int count = 0;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (renderer.sprite == null || !renderer.gameObject.activeInHierarchy || !new HashSet<string> { "MechanicBench11", "SalvagePipes11", "ConstructionCart", "CoverBattery08", "CoverTimber08", "CoverCart08", "ScrapCompressor11", "RubbleBroken10", "GeneratorSkid_v3", "CoveredParts_v3", "SalvageHoist16", "PipeWallV", "PipeWallH" }.Contains(renderer.sprite.name)) continue;
            var component = renderer.GetComponent<PropDepthPresenter>();
            // 같은 잔해 원화를 사용하는 후면 장식은 낮은 엄폐가 아니므로 이번 연결에서 제외한다.
            if (renderer.name == "후면_무너진기초18") { if (component != null) Undo.DestroyObjectImmediate(component); continue; }
            if (component != null) { count++; continue; }
            component = Undo.AddComponent<PropDepthPresenter>(renderer.gameObject);
            var data = new SerializedObject(component);
            data.FindProperty("propRenderer").objectReferenceValue = renderer;
            data.FindProperty("unitRegistry").objectReferenceValue = units[0];
            data.FindProperty("presentationRegistry").objectReferenceValue = visuals[0];
            data.FindProperty("groundLocalY").floatValue = 0f;
            data.FindProperty("boundaryHysteresis").floatValue = 0.03f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            count++;
        }
        if (count != 17) throw new InvalidOperationException($"낮은 소품 11개와 배관 담장 6개를 확인해야 합니다. 현재 {count}개");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
    /// <summary>사용자가 명시적으로 실행한 경우에만 실제 씬의 일시적인 플레이 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Cover Visual Fix")]
    public static void Verify()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("검증 전 씬의 미저장 변경을 먼저 처리해야 합니다.");
        Directory.CreateDirectory("Temp/CoverVisualFix");
        File.WriteAllText("Temp/CoverVisualFix/verification.txt", "RUNNING");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    /// <summary>대상 씬과 편집 상태를 확인한다.</summary>
    private static void RequireEditScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "BattleTest01")
            throw new InvalidOperationException("BattleTest01 편집 모드에서 실행해야 합니다.");
    }

    /// <summary>플레이 시작 후 초기화 프레임을 기다리고 검증한다.</summary>
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);
        results.Clear();
        routine = Run();
        nextStep = EditorApplication.timeSinceStartup + 1.0;
        EditorApplication.update += Tick;
    }

    /// <summary>실제 게임 프레임 사이에서 검증 단계를 실행하고 실패를 파일에 남긴다.</summary>
    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("검증 중 플레이가 중단됐습니다.");
            if (!routine.MoveNext()) { Finish(null); return; }
            nextStep = EditorApplication.timeSinceStartup + (routine.Current is float delay ? delay : 0.02f);
        }
        catch (Exception error) { Finish(error); }
    }

    /// <summary>검증 결과를 기록하고 플레이를 종료해 일시적인 배치를 폐기한다.</summary>
    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        File.WriteAllText("Temp/CoverVisualFix/verification.txt",
            (error == null ? "PASS" : "FAIL: " + error) + "\n" + string.Join("\n", results));
        if (error == null) Debug.Log($"엄폐 전환·정렬 검증 {results.Count}개 통과");
        else Debug.LogError("엄폐 전환·정렬 검증 실패: " + error);
        (routine as IDisposable)?.Dispose();
        routine = null;
        EditorApplication.isPlaying = false;
    }


    /// <summary>혼합 엄폐의 대기 전환과 실제 낮은 소품 전체의 앞뒤 정렬을 검증한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        double deadline = EditorApplication.timeSinceStartup + 40;
        while (ActionPresentationQueue.Instance.IsPlaying && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
        var cover = UnityEngine.Object.FindObjectsByType<LowCoverIdlePresenter>(FindObjectsSortMode.None)[0];
        var unit = Read<TacticalUnitContext>(cover, "unitContext");
        var visual = Read<ActorVisualController>(cover, "visualController");
        var grid = Read<GridManager>(cover, "gridManager");
        var queue = Read<ActionPresentationQueue>(cover, "presentationQueue");
        var center = new GridPosition(4, 4);
        foreach (var map in new[] { grid.WallLogicTilemap, grid.LowObstacleLogicTilemap, grid.BoundaryLogicTilemap })
            for (int x = 1; x <= 7; x++) for (int y = 1; y <= 8; y++) map.SetTile(map.WorldToCell(grid.GridToWorld(new GridPosition(x,y))), null);
        Rebuild(grid);
        var tile = ScriptableObject.CreateInstance<Tile>();
        SetTile(grid, grid.WallLogicTilemap, center + GridPosition.Left, tile);
        SetTile(grid, grid.LowObstacleLogicTilemap, center + GridPosition.Right, tile);
        Require(unit.GridActor.TryMoveTo(center), "혼합 엄폐 테스트 칸");
        visual.BeginMovePresentation();
        visual.SetPresentationPosition(grid.GridToWorld(center + GridPosition.Down));
        var before = visual.transform.position;
        queue.Enqueue(PresentationEvent.MoveActor(unit.GridActor, center + GridPosition.Down, center));
        Require(queue.PlayQueuedEvents(), "실제 혼합 엄폐 이동 큐");
        double moveDeadline = EditorApplication.timeSinceStartup + 5;
        bool sawIdle = false;
        while (EditorApplication.timeSinceStartup < moveDeadline)
        {
            if (!visual.IsMovingPresentation)
            {
                Require(visual.CurrentAnimationStateName == "LowCoverIdle", "이동 종료 첫 프레임부터 낮은 엄폐 요청");
                Require(visual.Animator.GetCurrentAnimatorStateInfo(0).IsName("LowCoverIdle"), "이동 종료 첫 프레임의 실제 Animator 자세");
                Require(!visual.Animator.IsInTransition(0), "일반 대기/높은 엄폐 크로스페이드 없음");
                sawIdle = true;
                break;
            }
            yield return 0.01f;
        }
        Require(sawIdle, "이동 종료 관찰");
        Sprite idleSprite = visual.TargetRenderer.sprite;
        for (int i = 0; i < 10; i++)
        {
            yield return 0.02f;
            Require(visual.TargetRenderer.sprite == idleSprite && visual.CurrentAnimationStateName == "LowCoverIdle", "혼합 엄폐 후속 프레임 안정");
        }
        // 논리 좌표가 미래 칸을 가리켜도 Idle 동기화는 현재 화면의 칸을 사용해야 한다.
        Require(unit.GridActor.TryMoveTo(new GridPosition(4,7)), "미래 논리 좌표 준비");
        visual.BeginMovePresentation();
        visual.SetPresentationPosition(grid.GridToWorld(center));
        visual.TryPlayAnimationState("Move", 0f);
        visual.EndMovePresentation();
        visual.TryPlayAnimationState("Idle", 0.2f);
        Require(visual.CurrentAnimationStateName == "LowCoverIdle" && cover.SelectedCoverPosition == center + GridPosition.Right,
            "대기 동기화가 미래 논리 칸 대신 표시 칸 사용");
        // 이후 검증은 위치를 고정하고 정렬만 비교한다. 플레이 종료 시 모두 원복된다.
        cover.enabled = false;
        yield return 0.3f;
        visual.TryPlayAnimationState("LowCoverIdle", 0f);
        visual.FaceLeft();
        var props = UnityEngine.Object.FindObjectsByType<PropDepthPresenter>(FindObjectsSortMode.InstanceID);
        Require(props.Length == 17, "낮은 소품 11개와 배관 담장 6개 정렬 연결");
        foreach (var mover in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None)) mover.enabled = false;
        foreach (var zoom in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None)) zoom.enabled = false;
        var camera = Camera.main;
        Require(camera != null, "검수 카메라");
        // 화면 검수용 안개와 HUD 숨김은 플레이 사본에만 적용한다.
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (renderer.name == "Soft Fog" || renderer.name == "Outside Fog") renderer.gameObject.SetActive(false);
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        Vector3 logical = unit.GridActor.transform.position;
        int hp = unit.Health.CurrentHitPoint, ap = unit.ActionPoint.Current;
        foreach (var prop in props) prop.enabled = false;
        foreach (var prop in props)
        {
            prop.enabled = true;
            var renderer = prop.PropRenderer;
            Color color = renderer.color;
            foreach (bool front in new[] { true, false })
            {
                Vector3 ground = renderer.transform.TransformPoint(new Vector3(0.3f, prop.GroundLocalY + (front ? -0.15f : 0.3f), 0));
                visual.transform.position += ground - visual.GroundWorldPosition;
                camera.transform.position = new Vector3(renderer.bounds.center.x, renderer.bounds.center.y + 0.2f, camera.transform.position.z);
                camera.orthographicSize = 1.8f;
                yield return 0.25f;
                prop.RefreshDepth();
                int actorOrder = visual.TargetRenderer.sortingOrder;
                Require(front ? actorOrder > renderer.sortingOrder : actorOrder < renderer.sortingOrder, renderer.name + (front ? " 앞 정렬" : " 뒤 정렬"));
                Require(renderer.color == color, "소품 알파·RGB 유지");
                Require(unit.GridActor.transform.position == logical && unit.Health.CurrentHitPoint == hp && unit.ActionPoint.Current == ap, "정렬이 논리 위치·HP·AP 변경하지 않음");
                ScreenCapture.CaptureScreenshot("Temp/CoverVisualFix/" + renderer.sprite.name + (front ? "-front.png" : "-behind.png"));
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                yield return 0.2f;
                if (!front)
                {
                    // 다른 건물이 캐릭터를 올리는 경우에도 가까운 소품 뒤라는 제약이 우선한다.
                    var fake = new GameObject("검증용 전면 건물").AddComponent<SpriteRenderer>();
                    fake.sortingLayerID = renderer.sortingLayerID;
                    fake.sortingOrder = renderer.sortingOrder + 100;
                    visual.SetBuildingFrontSorting(fake, true);
                    Require(visual.TargetRenderer.sortingOrder < renderer.sortingOrder, "건물 전면 보정이 소품 뒤 순서를 덮어쓰지 않음");
                    visual.BeginVisionSortingOverride(renderer.sortingLayerName, renderer.sortingOrder + 1000);
                    prop.RefreshDepth();
                    Require(visual.TargetRenderer.sortingOrder == renderer.sortingOrder + 1000, "안개 위 임시 공격 정렬 우선");
                    visual.EndVisionSortingOverride();
                    Require(visual.TargetRenderer.sortingOrder < renderer.sortingOrder, "안개 정렬 종료 후 소품 뒤로 복귀");
                    visual.SetBuildingFrontSorting(fake, false);
                    UnityEngine.Object.Destroy(fake.gameObject);
                    Vector3 fixedPosition = visual.transform.position;
                    prop.enabled = false;
                    Require(visual.transform.position == fixedPosition, "정렬 비활성화가 붙는 위치를 변경하지 않음");
                    prop.enabled = true;
                }
            }
            prop.enabled = false;
        }
        // 실제 씬의 정렬 요소에서 벗어나면 모든 임시 요청을 회수한다.
        visual.SetPresentationPosition(new Vector3(-50, -50, 0));
        yield return 0.2f;
        Require(visual.TargetRenderer.sortingOrder == 0, "소품에서 벗어난 뒤 원래 정렬 복원");
    }

    /// <summary>검증 대상의 명시적 직렬화 참조를 읽는다.</summary>
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

    /// <summary>검증 사본의 타일 변경을 실제 보드 분류에 반영한다.</summary>
    private static void SetTile(GridManager grid, UnityEngine.Tilemaps.Tilemap map, GridPosition position, UnityEngine.Tilemaps.TileBase tile)
    {
        map.SetTile(map.WorldToCell(grid.GridToWorld(position)), tile);
        Rebuild(grid);
    }

    /// <summary>점유를 보존하는 실제 보드 재구축을 호출한다.</summary>
    private static void Rebuild(GridManager grid) => typeof(GridManager).GetMethod("RebuildCellStates", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grid, null);

    /// <summary>실패 원인을 기록하고 검증을 중단한다.</summary>
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        results.Add(description);
    }
    /// <summary>낮은 엄폐 논리 타일과 주변 그림을 읽어 조정 전 대응 관계를 기록한다.</summary>
    [MenuItem("Tools/S2/Audit Low Cover")]
    public static void Audit()
    {
        RequireEditScene();
        Directory.CreateDirectory("Temp/CoverTune");
        var grid = UnityEngine.Object.FindFirstObjectByType<GridManager>();
        var sprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var lines = new List<string>();
        foreach (var prop in UnityEngine.Object.FindObjectsByType<PropDepthPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) lines.Add($"PROP {prop.name} sprite={prop.PropRenderer.sprite.name} active={prop.gameObject.activeInHierarchy} ground={prop.GroundLocalY}");
        var map = grid.LowObstacleLogicTilemap;
        foreach (var cell in map.cellBounds.allPositionsWithin)
        {
            if (!map.HasTile(cell)) continue;
            var world = map.GetCellCenterWorld(cell);
            var pos = grid.WorldToGrid(world);
            var nearby = new List<string>();
            foreach (var r in sprites)
            {
                if (r.sprite == null || !r.gameObject.activeInHierarchy || !AssetDatabase.GetAssetPath(r.sprite).Contains("S3MapArt")) continue;
                var b = r.bounds;
                if (Mathf.Abs(b.center.x-world.x)<b.extents.x+0.3f && Mathf.Abs(b.center.y-world.y)<b.extents.y+0.3f)
                    nearby.Add(r.name+"/"+r.sprite.name+"/depth="+(r.GetComponent<PropDepthPresenter>()!=null));
            }
            lines.Add($"LOW {pos.x},{pos.y} world={world:F2} : {string.Join(" | ",nearby)}");
        }
        File.WriteAllLines("Temp/CoverTune/audit.txt", lines);
    }
    /// <summary>실제 화면 비교로 선정한 낮은 엄폐 접촉 거리와 시간만 두 유닛에 적용한다.</summary>
    [MenuItem("Tools/S2/Apply Low Cover Tuning")]
    public static void ApplyTuning()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 변경이 있습니다.");
        foreach(var cover in UnityEngine.Object.FindObjectsByType<LowCoverIdlePresenter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var data=new SerializedObject(cover);
            data.FindProperty("lowCoverContactOffset").vector2Value=new Vector2(0.9f,0.35f);
            data.FindProperty("lowCoverTransitionDuration").floatValue=0.24f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cover);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }
}
