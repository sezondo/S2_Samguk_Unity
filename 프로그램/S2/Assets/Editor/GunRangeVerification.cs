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

/// <summary>저장 씬의 플레이 사본에서 총격 UI와 공격 판정를 검사한다.</summary>
[InitializeOnLoad]
public static class GunRangeVerification
{
    // 재로드 이후 검증 요청과 결과를 보관하는 키·경로다.
    private const string Pending = "S2.GunRange.Verify";
    private const string Output = "Temp/GunRange";
    // 프레임 기반 검증 진행 상태와 결과 목록이다.
    private static IEnumerator routine;
    private static readonly List<string> results = new();

    /// <summary>플레이 진입 이후 검증 루틴을 연결한다.</summary>
    static GunRangeVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>저장된 씬을 다시 읽고 플레이 사본에서 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Gun Range")]
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
        if (error == null) Debug.Log($"총격 UI 검사 {results.Count}개 통과");
        else Debug.LogError("총격 UI 검사 실패: " + error);
        EditorApplication.isPlaying = false;
    }

    /// <summary>플레이 사본에서 총격 표시와 실행 판정의 일치를 검사한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        double until = EditorApplication.timeSinceStartup + 8;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        until = EditorApplication.timeSinceStartup + 40;
        while (ActionPresentationQueue.Instance.IsBusy && EditorApplication.timeSinceStartup < until) yield return null;
        Check(!ActionPresentationQueue.Instance.IsBusy, "입장 연출 완료");
        var unit = PlayerUnitControlManager.Instance.ActiveUnit;
        var gun = unit.GunAttackAction;
        var grid = GridManager.Instance;
        var overlay = UnityEngine.Object.FindFirstObjectByType<BattleHudTacticalOverlayController>();
        var hud = UnityEngine.Object.FindFirstObjectByType<BattleHudController>();
        Check(overlay.HasValidReference(), "총격 머티리얼·HUD 필수 참조");
        foreach (var input in UnityEngine.Object.FindObjectsByType<PlayerUnitInputController>(FindObjectsSortMode.None)) input.enabled = false;
        unit.CancelAllActionSelections(); gun.SelectGunAttackAction();
        Check(gun.IsGunAttackSelected, "총격 선택");
        var renderer = (GunRangeOverlay)Get(overlay, "gunOverlay");
        renderer.Refresh(unit, EnemyRegistry.Instance, null);
        var boundary = GameObject.Find("GunRangeBoundary").GetComponent<MeshFilter>().sharedMesh;
        Check(boundary.vertexCount > 0, "주황 사거리 경계 생성");
        Check(GameObject.Find("GunRangeBoundary").GetComponent<MeshRenderer>().sortingOrder < PlayerVisionPresenter.Instance.FogSortingOrder, "사거리 표시는 안개 아래");
        Check(!((RectTransform)Get(overlay,"hitPreviewRoot")).gameObject.activeSelf, "기존 명중률 팝업 숨김");
        int ap = unit.ActionPoint.Current, ammo = unit.GunAmmo.CurrentAmmo;
        var origin = unit.GridActor.GridPosition;
        var enemies = EnemyRegistry.Instance.Enemies.ToArray();
        Check(enemies.Length >= 2, "검증 대상 적 확보");
        var enemy = enemies[0];
        GridPosition direction = new[] {GridPosition.Down, GridPosition.Right, GridPosition.Up, GridPosition.Left}
            .First(d => grid.IsInside(origin + d + d) && !grid.IsBlocked(origin + d) && !grid.IsOccupied(origin + d) &&
                !grid.IsBlocked(origin + d + d) && !grid.IsOccupied(origin + d + d));
        var target = origin + direction + direction;
        Check(enemy.GridActor.TryMoveTo(target), "검증 적을 사거리 안으로 배치");
        var visible = (HashSet<GridPosition>)Get(PlayerVisionManager.Instance, "visiblePositions");
        visible.Add(target);
        PlayerVisionPresenter.Instance.SetMovedEnemyVisibility(enemy.GridActor, true);
        ActorPresentationRegistry.Instance.TryGetVisual(enemy.GridActor, out var visual);
        visual.SetVisionAlpha(1);
        Check(gun.TryPreviewTarget(target, out var actor, out var reason) && actor == enemy.GridActor, "공격 가능 대상 판정");
        renderer.Refresh(unit, EnemyRegistry.Instance, target);
        int availableVertices = GameObject.Find("GunTargetPreview").GetComponent<MeshFilter>().sharedMesh.vertexCount;
        Check(availableVertices > 0, "가능 대상 조준선·모서리 생성");
        Check(unit.ActionPoint.Current == ap && unit.GunAmmo.CurrentAmmo == ammo, "미리보기 자원 무소비");

        // 둘째 적을 닫힌 문처럼 등록해 동적 차단과 열림 복구를 재현한다.
        var blocker = enemies[1].GridActor;
        Check(blocker.TryMoveTo(origin + direction), "동적 차단 검증 칸 배치");
        grid.RegisterSightBlocker(blocker); visible.Add(target);
        Check(!gun.TryPreviewTarget(target, out _, out reason) && reason == "벽에 막힘", "닫힌 구조물의 사선 차단");
        Check(GridLineOfSight.TryGetFirstBlockingCell(grid, origin, target, out var hit) && hit == origin + direction, "조준선의 첫 차단 칸 일치");
        Check(!gun.TryExecuteGunAttack(target, new ActionResolutionContext(ActionPresentationQueue.Instance)), "차단 대상 실제 발사 거부");
        Check(unit.ActionPoint.Current == ap && unit.GunAmmo.CurrentAmmo == ammo, "차단 공격 AP·탄약 무소비");
        grid.UnregisterSightBlocker(blocker); visible.Add(target);
        Check(gun.TryPreviewTarget(target, out _, out _), "동적 차단 해제 후 공격 복구");
        visible.Remove(target);
        Check(!gun.TryPreviewTarget(target, out actor, out reason) && actor == null && reason == "시야 밖", "안개 속 대상 정보 비공개");
        renderer.Refresh(unit, EnemyRegistry.Instance, target);
        Check(GameObject.Find("GunTargetPreview").GetComponent<MeshFilter>().sharedMesh.vertexCount < availableVertices, "숨은 적 조준 표시 제거");
        visible.Add(target);

        var outside = new GridPosition(-1,-1);
        for (int x=0;x<grid.Width && outside.x<0;x++)
        for (int y=0;y<grid.Height && outside.x<0;y++)
        { var p=new GridPosition(x,y); if(origin.ManhattanDistanceTo(p)==gun.GunAttackRange+1 && !grid.IsBlocked(p)&&!grid.IsOccupied(p))outside=p; }
        Check(outside.x >= 0 && enemy.GridActor.TryMoveTo(outside), "사거리 밖 검증 대상 배치");
        visible.Add(outside);
        Check(!gun.TryPreviewTarget(outside,out _,out reason)&&reason=="사거리 밖", "최대 사거리 초과 차단");
        Check(!gun.TryExecuteGunAttack(outside,new ActionResolutionContext(ActionPresentationQueue.Instance)), "사거리 밖 실제 발사 거부");
        Check(unit.ActionPoint.Current==ap && unit.GunAmmo.CurrentAmmo==ammo,"사거리 밖 공격 자원 보존");
        enemy.GridActor.TryMoveTo(target); visible.Add(target);
        // 낮은 엄폐는 이동을 막아도 사선을 막지 않는 기존 규칙을 확인한다.
        bool lowChecked=false;
        for(int x=1;x<grid.Width-1&&!lowChecked;x++)
        for(int y=1;y<grid.Height-1&&!lowChecked;y++)
        {
            var p=new GridPosition(x,y);
            if(!grid.IsLowObstacle(p)||grid.IsSightBlocked(p))continue;
            Check(GridLineOfSight.HasLineOfSight(grid,p+GridPosition.Left,p+GridPosition.Right),"낮은 엄폐 사선 통과");
            lowChecked=true;
        }
        Check(lowChecked,"낮은 엄폐 사례 검사 완료");
        var camera=Camera.main;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None))input.enabled=false;
        camera.transform.position=grid.GridToWorld(origin)+new Vector3(1,-1,-10);camera.orthographicSize=5;
        PlayerVisionPresenter.Instance.SetMovedEnemyVisibility(enemy.GridActor,true);visual.SetVisionAlpha(1);
        until=EditorApplication.timeSinceStartup+.5;
        while(EditorApplication.timeSinceStartup<until)yield return null;
        hud.enabled=false;
        visual.SetPresentationPosition(grid.GridToWorld(target));
        var baseHud=UnityEngine.Object.FindFirstObjectByType<BattleHudBaseController>();
        var nameText=(TMPro.TMP_Text)Get(baseHud,"actionNameText");
        var detailText=(TMPro.TMP_Text)Get(baseHud,"actionDetailText");
        var refreshInfo=typeof(BattleHudBaseController).GetMethod("RefreshGunTargetInfo",BindingFlags.NonPublic|BindingFlags.Instance);
        refreshInfo.Invoke(baseHud,new object[]{unit,target});
        Check(nameText.text=="총격 · 공격 가능"&&detailText.text.Contains("명중"),"명중률을 하단 기존 행동 정보에 표시");
        grid.RegisterSightBlocker(blocker);visible.Add(target);
        refreshInfo.Invoke(baseHud,new object[]{unit,target});
        Check(nameText.text=="총격 · 벽에 막힘","벽 차단 사유를 하단에 표시");
        grid.UnregisterSightBlocker(blocker);visible.Add(target);
        visual.SetVisionAlpha(0);nameText.text="총격";
        refreshInfo.Invoke(baseHud,new object[]{unit,target});
        Check(nameText.text=="총격","화면에서 숨은 대상의 하단 정보 비공개");
        visual.SetVisionAlpha(1);
        refreshInfo.Invoke(baseHud,new object[]{unit,target});
        overlay.Refresh();
        renderer.Refresh(unit,EnemyRegistry.Instance,target);
        ScreenCapture.CaptureScreenshot(Output+"/gun.png");
        until=EditorApplication.timeSinceStartup+.6;
        while(EditorApplication.timeSinceStartup<until)yield return null;
        gun.CancelGunAttackAction();renderer.Refresh(unit,EnemyRegistry.Instance,null);
        Check(boundary.vertexCount==0&&GameObject.Find("GunTargetPreview").GetComponent<MeshFilter>().sharedMesh.vertexCount==0,"취소 시 범위·조준 정리");
        gun.SelectGunAttackAction();visible.Add(target);
        Check(gun.TryExecuteGunAttack(target,new ActionResolutionContext(ActionPresentationQueue.Instance)),"공격 가능 대상 실제 발사 승인");
        Check(unit.ActionPoint.Current==ap-unit.UnitData.GunAttackActionPointCost&&unit.GunAmmo.CurrentAmmo==ammo-1,"승인된 발사의 AP·탄약 정확히 1회 소비");
        renderer.Refresh(unit,EnemyRegistry.Instance,target);
        Check(boundary.vertexCount==0,"발사 후 사거리 표시 정리");
    }

    /// <summary>검증용 사본의 내부 상태를 읽는다.</summary>
    private static object Get(object target,string field)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    /// <summary>검사 실패를 알리거나 통과 결과를 누적한다.</summary>
    private static void Check(bool valid,string label)
    {if(!valid)throw new InvalidOperationException(label);results.Add("PASS "+label);}
}

