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
public static class CoverTuningVerification
{
    // 도메인 리로드 이후에도 검증 시작 요청을 전달하는 키다.
    private const string PendingKey = "S2.CoverTuningVerification.Pending";
    // 검사 결과와 현재 실행 중인 단계다.
    private static readonly List<string> results = new();
    private static IEnumerator routine;
    // 프레임 및 연출 완료를 기다리는 편집기 시간이다.
    private static double nextStep;

    /// <summary>플레이 전환 후 검증 루틴을 연결한다.</summary>
    static CoverTuningVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }


    /// <summary>사용자가 명시적으로 실행한 경우에만 실제 씬의 일시적인 플레이 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Cover Tuning")]
    public static void Verify()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("검증 전 씬의 미저장 변경을 먼저 처리해야 합니다.");
        Directory.CreateDirectory("Temp/CoverTune");
        File.WriteAllText("Temp/CoverTune/verification.txt", "RUNNING");
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
        File.WriteAllText("Temp/CoverTune/verification.txt",
            (error == null ? "PASS" : "FAIL: " + error) + "\n" + string.Join("\n", results));
        if (error == null) Debug.Log($"엄폐 거리·보간 검증 {results.Count}개 통과");
        else Debug.LogError("엄폐 거리·보간 검증 실패: " + error);
        (routine as IDisposable)?.Dispose();
        routine = null;
        EditorApplication.isPlaying = false;
    }


    // 확인한 실제 낮은 엄폐 소품과 각 소품에 대응하는 논리 칸이다.
    private static readonly Dictionary<string, GridPosition[]> Samples = new()
    {
        {"CoverBattery08", new[]{new GridPosition(40,18),new GridPosition(41,18)}},
        {"MechanicBench11", new[]{new GridPosition(31,15),new GridPosition(32,15)}},
        {"ScrapCompressor11", new[]{new GridPosition(33,15)}},
        {"SalvagePipes11", new[]{new GridPosition(38,20)}},
        {"ConstructionCart", new[]{new GridPosition(38,24),new GridPosition(39,24)}},
        {"CoverTimber08", new[]{new GridPosition(34,23),new GridPosition(35,23)}},
        {"RubbleBroken10", new[]{new GridPosition(36,23),new GridPosition(37,23),new GridPosition(38,23)}},
        {"CoverCart08", new[]{new GridPosition(27,25),new GridPosition(28,25)}},
        {"GeneratorSkid_v3", new[]{new GridPosition(33,21)}},
        {"CoveredParts_v3", new[]{new GridPosition(35,21),new GridPosition(36,21)}},
        {"SalvageHoist16", new[]{new GridPosition(29,21),new GridPosition(30,21),new GridPosition(31,21),new GridPosition(32,21),new GridPosition(30,22)}}
    };

    /// <summary>실제 소품 주변의 진입 가능한 칸에서 엄폐 자세와 간격을 촬영한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        double deadline = EditorApplication.timeSinceStartup + 40;
        while (ActionPresentationQueue.Instance.IsPlaying && EditorApplication.timeSinceStartup < deadline) yield return 0.1f;
        var cover = UnityEngine.Object.FindFirstObjectByType<LowCoverIdlePresenter>();
        var unit = Read<TacticalUnitContext>(cover, "unitContext");
        var visual = Read<ActorVisualController>(cover, "visualController");
        var grid = Read<GridManager>(cover, "gridManager");
        foreach (var mover in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None)) mover.enabled = false;
        foreach (var zoom in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None)) zoom.enabled = false;
        var camera = Camera.main;
        foreach (var r in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (r.name == "Soft Fog" || r.name == "Outside Fog") r.gameObject.SetActive(false);
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        var sprites = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        int hp = unit.Health.CurrentHitPoint, ap = unit.ActionPoint.Current;
        foreach (var sample in Samples)
        {
            SpriteRenderer prop = Array.Find(sprites, r => r.sprite != null && r.sprite.name == sample.Key && r.gameObject.activeInHierarchy);
            Require(prop != null, sample.Key+" 활성 그림");
            foreach (var side in new[]{GridPosition.Left,GridPosition.Right,GridPosition.Down,GridPosition.Up})
            {
                bool found=false;
                GridPosition chosen=default;
                float best=float.PositiveInfinity;
                foreach(var cell in sample.Value)
                {
                    var candidate=cell+side;
                    if(!grid.CanEnter(candidate)) continue;
                    float distance=(grid.GridToWorld(candidate)-prop.transform.position).sqrMagnitude;
                    if(distance<best) {found=true; chosen=candidate; best=distance;}
                }
                if(!found) { results.Add(sample.Key+" "+side+" 진입 불가: 생략"); continue; }
                cover.enabled=false;
                visual.BeginMovePresentation();
                yield return 0.35f;
                Require(unit.GridActor.TryMoveTo(chosen), sample.Key+" 테스트 칸 이동");
                visual.SetPresentationPosition(grid.GridToWorld(chosen));
                cover.enabled=true;
                visual.EndMovePresentation();
                visual.TryPlayAnimationState("Idle",0f);
                yield return 0.4f;
                Require(unit.GridActor.GridPosition==chosen && unit.Health.CurrentHitPoint==hp && unit.ActionPoint.Current==ap,"표시 조정 중 논리 칸·HP·AP 유지");
                camera.transform.position=new Vector3(prop.bounds.center.x,prop.bounds.center.y,camera.transform.position.z);
                camera.orthographicSize=Mathf.Max(2.0f,prop.bounds.extents.y+0.8f);
                string label=side.x<0?"left":side.x>0?"right":side.y<0?"below":"above";
                results.Add($"{sample.Key} {label} grid={chosen} selected={cover.SelectedCoverPosition} offset={visual.CoverWorldOffset:F4} foot={visual.GroundWorldPosition:F3} actorOrder={visual.TargetRenderer.sortingOrder} propOrder={prop.sortingOrder}");
                yield return 0.15f;
                ScreenCapture.CaptureScreenshot("Temp/CoverTune/"+sample.Key+"-"+label+".png");
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                yield return 0.2f;
            }
        }
        // 통제된 낮은 엄폐 칸으로 실제 이동 큐를 실행하여 중간 위치가 화면 프레임에 존재하는지 기록한다.
        var center=new GridPosition(4,4);
        foreach(var map in new[]{grid.WallLogicTilemap,grid.LowObstacleLogicTilemap,grid.BoundaryLogicTilemap})
            for(int x=2;x<7;x++) for(int y=2;y<7;y++) map.SetTile(map.WorldToCell(grid.GridToWorld(new GridPosition(x,y))),null);
        SetTile(grid,grid.LowObstacleLogicTilemap,center+GridPosition.Right,ScriptableObject.CreateInstance<Tile>());
        cover.enabled=false;
        visual.BeginMovePresentation();
        yield return 0.35f;
        Require(unit.GridActor.TryMoveTo(center),"보간 관찰용 도착 칸");
        visual.SetPresentationPosition(grid.GridToWorld(center+GridPosition.Down));
        cover.enabled=true;
        var queue=Read<ActionPresentationQueue>(cover,"presentationQueue");
        queue.Enqueue(PresentationEvent.MoveActor(unit.GridActor,center+GridPosition.Down,center));
        Require(queue.PlayQueuedEvents(),"보간 관찰 실제 이동 큐");
        var trace=new List<string>{"frame,time,offsetX,offsetY,moving,animation"};
        float start=Time.time;
        int frame=-1, intermediate=0;
        float firstIdle=-1,lastChange=-1,lastX=0;
        while(Time.time-start<2f)
        {
            if(frame!=Time.frameCount)
            {
                frame=Time.frameCount;
                float x=visual.CoverWorldOffset.x;
                trace.Add($"{frame},{Time.time-start:F5},{x:F5},{visual.CoverWorldOffset.y:F5},{visual.IsMovingPresentation},{visual.CurrentAnimationStateName}");
                if(!visual.IsMovingPresentation)
                {
                    if(firstIdle<0) firstIdle=Time.time-start;
                    if(x>0.0001f && Mathf.Abs(x-lastX)>0.00001f){ intermediate++; lastChange=Time.time-start; }
                }
                lastX=x;
            }
            yield return 0f;
        }
        File.WriteAllLines("Temp/CoverTune/interpolation.csv",trace);
        Require(intermediate>=2,"엄폐 위치가 복수 프레임에 걸쳐 보간됨");
        results.Add($"보간 관찰: 변화 프레임={intermediate}, 첫 대기={firstIdle:F4}s, 최종 변화={lastChange:F4}s, 관찰 구간={lastChange-firstIdle:F4}s");
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
}
