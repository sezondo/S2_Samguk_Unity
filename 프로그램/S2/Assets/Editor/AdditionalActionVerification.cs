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

/// <summary>저장 씬의 플레이 사본에서 추가 행동 UI와 공격 판정을 검사한다.</summary>
[InitializeOnLoad]
public static class AdditionalActionVerification
{
    // 재로드 이후 검증 요청과 결과를 보관하는 키·경로다.
    private const string Pending = "S2.AdditionalAction.Verify";
    private const string Output = "Temp/AdditionalAction";
    // 프레임 기반 검증 진행 상태와 결과 목록이다.
    private static IEnumerator routine;
    private static readonly List<string> results = new();

    /// <summary>플레이 진입 이후 검증 루틴을 연결한다.</summary>
    static AdditionalActionVerification() => EditorApplication.playModeStateChanged += OnMode;

    /// <summary>저장된 씬을 다시 읽고 플레이 사본에서 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Additional Actions")]
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
        if (error == null) Debug.Log($"추가 행동 UI 검사 {results.Count}개 통과");
        else Debug.LogError("추가 행동 UI 검사 실패: " + error);
        EditorApplication.isPlaying = false;
    }

    /// <summary>저장 씬의 플레이 사본에서 세 행동의 범위·판정·자원·안개를 검사한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        double until = EditorApplication.timeSinceStartup + 8;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        until = EditorApplication.timeSinceStartup + 40;
        while (ActionPresentationQueue.Instance.IsBusy && EditorApplication.timeSinceStartup < until) yield return null;
        Check(!ActionPresentationQueue.Instance.IsBusy,"입장 연출 완료");
        var unit=PlayerUnitControlManager.Instance.ActiveUnit;
        var grid=GridManager.Instance;
        var overlay=UnityEngine.Object.FindFirstObjectByType<BattleHudTacticalOverlayController>();
        var hud=UnityEngine.Object.FindFirstObjectByType<BattleHudController>();
        foreach(var input in UnityEngine.Object.FindObjectsByType<PlayerUnitInputController>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CameraKeyboardMover>(FindObjectsSortMode.None))input.enabled=false;
        foreach(var input in UnityEngine.Object.FindObjectsByType<CameraMouseZoom>(FindObjectsSortMode.None))input.enabled=false;
        var renderer=(ActionRangeOverlay)Get(overlay,"actionOverlay");
        var visible=(HashSet<GridPosition>)Get(PlayerVisionManager.Instance,"visiblePositions");
        ActionPresentationQueue.Instance.ClearQueuedEvents();
        unit.CancelAllActionSelections();unit.ActionPoint.RefillForTurn();
        var origin=unit.GridActor.GridPosition;
        int ap=unit.ActionPoint.Current,ammo=unit.GunAmmo.CurrentAmmo;
        var enemy=EnemyRegistry.Instance.Enemies.First(e=>e!=null&&e.IsAlive);
        var diagonal=new[]{new GridPosition(-1,-1),new GridPosition(-1,1),new GridPosition(1,-1),new GridPosition(1,1)}
            .Select(d=>origin+d).First(p=>grid.IsInside(p)&&!grid.IsBlocked(p)&&!grid.IsOccupied(p));
        Check(enemy.GridActor.TryMoveTo(diagonal),"대각선 근접 대상 배치");
        Reveal(enemy,diagonal,visible);
        unit.MeleeAttackAction.SelectMeleeAttackAction();
        Check(ActionRangeOverlay.TryGetSelection(unit,out var action,out var center,out int range)&&action==BattleHudActionType.Melee&&center==origin&&range==1,"근접 범위는 본체 중심 1칸");
        Check(unit.MeleeAttackAction.TryPreviewTarget(diagonal,out _,out _),"대각선 8방향 근접 가능");
        renderer.Refresh(unit,EnemyRegistry.Instance,diagonal);
        var boundary=GameObject.Find("ActionRangeBoundary").GetComponent<MeshFilter>().sharedMesh;
        var targetMesh=GameObject.Find("ActionTargetPreview").GetComponent<MeshFilter>().sharedMesh;
        Check(boundary.vertexCount>0&&targetMesh.vertexCount>0,"근접 실선과 대상 생성");
        Check(GameObject.Find("ActionRangeBoundary").GetComponent<MeshRenderer>().sortingOrder<PlayerVisionPresenter.Instance.FogSortingOrder,"추가 행동 범위는 안개 아래");
        Check(unit.ActionPoint.Current==ap&&unit.GunAmmo.CurrentAmmo==ammo,"근접 미리보기 자원 무소비");
        visible.Remove(diagonal);
        Check(!ActionRangeOverlay.TryDescribeTarget(unit,action,diagonal,out _,out _,out _),"안개 속 근접 대상 정보 비공개");
        Reveal(enemy,diagonal,visible);
        var far=FindFree(grid,p=>origin.ManhattanDistanceTo(p)==4);
        Check(enemy.GridActor.TryMoveTo(far),"근접 범위 밖 배치");Reveal(enemy,far,visible);
        Check(!unit.MeleeAttackAction.TryPreviewTarget(far,out _,out string reason)&&reason=="사거리 밖","근접 범위 초과 사유");
        Check(!unit.MeleeAttackAction.TryExecuteMeleeAttack(far,new ActionResolutionContext(ActionPresentationQueue.Instance))&&unit.ActionPoint.Current==ap,"불가 근접 실행 거부와 AP 보존");
        enemy.GridActor.TryMoveTo(diagonal);Reveal(enemy,diagonal,visible);
        { var capture=Capture(unit,overlay,hud,renderer,BattleHudActionType.Melee,diagonal,"melee",origin); while(capture.MoveNext()) yield return capture.Current; }
        Check(unit.MeleeAttackAction.TryExecuteMeleeAttack(diagonal,new ActionResolutionContext(ActionPresentationQueue.Instance))&&unit.ActionPoint.Current==ap-unit.UnitData.MeleeAttackActionPointCost,"가능 근접 실행과 AP 소비 일치");
        ActionPresentationQueue.Instance.ClearQueuedEvents();
        unit.CancelAllActionSelections();unit.ActionPoint.RefillForTurn();

        unit.SwordState.RecallToPlayer();unit.SwordThrowAction.SelectSwordThrowAction();
        var landing=FindFree(grid,p=>origin.ManhattanDistanceTo(p)==2);
        Check(unit.SwordThrowAction.TryPreviewTarget(landing,out _),"빈 칸 검 투척 가능");
        var outside=FindFree(grid,p=>origin.ManhattanDistanceTo(p)==unit.SwordThrowAction.SwordThrowRange+1);
        Check(!unit.SwordThrowAction.TryPreviewTarget(outside,out reason)&&reason=="사거리 밖","검 투척 거리 초과");
        Check(!unit.SwordThrowAction.TryExecuteSwordThrow(outside,new ActionResolutionContext(ActionPresentationQueue.Instance))&&unit.ActionPoint.Current==ap,"불가 투척 AP 보존");
        visible.Remove(landing);
        Check(ActionRangeOverlay.TryDescribeTarget(unit,BattleHudActionType.SwordThrow,landing,out bool valid,out reason,out _)&&valid&&reason.StartsWith("투척 가능"),"미탐색 칸 일반 투척 안내 유지");
        visible.Add(landing);
        renderer.Refresh(unit,EnemyRegistry.Instance,landing);
        Check(boundary.vertexCount>0&&targetMesh.vertexCount>0,"검 투척 범위·목적지·조준선 생성");
        Check(GameObject.Find("PlayerSwordThrowAction_SuspicionArea")==null&&GameObject.Find("PlayerSwordThrowAction_Warning")==null,"검 투척 임시 색상 격자 제거");
        // 경고 판정만 분리해서 검증한 뒤 기존 인식 상태와 감지 칸을 복원한다.
        var stateField=typeof(EnemyAlertState).GetField("currentState",BindingFlags.NonPublic|BindingFlags.Instance);
        var states=EnemyRegistry.Instance.Enemies.Where(e=>e!=null&&e.AlertState!=null).ToDictionary(e=>e,e=>stateField.GetValue(e.AlertState));
        foreach(var pair in states)stateField.SetValue(pair.Key.AlertState,EnemyAwarenessState.Alerted);
        var detection=(List<GridPosition>)Get(enemy.GridSight,"swordDetectionPositions");
        var oldDetection=detection.ToArray();detection.Clear();detection.Add(landing);
        Reveal(enemy,diagonal,visible);
        Check(!unit.SwordThrowAction.WillCauseSuspicion(landing),"이미 발각된 적은 새 의심 경고 제외");
        stateField.SetValue(enemy.AlertState,EnemyAwarenessState.Unaware);
        Check(unit.SwordThrowAction.WillCauseSuspicion(landing),"공개된 미발각 적의 새 의심 경고");
        visible.Remove(diagonal);
        Check(!unit.SwordThrowAction.WillCauseSuspicion(landing),"숨겨진 적의 의심 경고 비공개");
        detection.Clear();detection.AddRange(oldDetection);
        foreach(var pair in states)stateField.SetValue(pair.Key.AlertState,pair.Value);
        Reveal(enemy,diagonal,visible);
        unit.ActionPoint.TrySpend(ap);
        Check(!unit.SwordThrowAction.TryPreviewTarget(landing,out reason)&&reason=="AP 부족","선택 중 AP 부족 안내");
        unit.ActionPoint.RefillForTurn();unit.SwordThrowAction.SelectSwordThrowAction();
        { var capture=Capture(unit,overlay,hud,renderer,BattleHudActionType.SwordThrow,landing,"sword",origin); while(capture.MoveNext()) yield return capture.Current; }
        Check(unit.SwordThrowAction.TryExecuteSwordThrow(landing,new ActionResolutionContext(ActionPresentationQueue.Instance)),"빈 칸 실제 투척 승인");
        Check(unit.SwordState.CurrentPosition==landing&&unit.ActionPoint.Current==ap-unit.UnitData.SwordThrowActionPointCost,"검 위치·AP 결과 일치");
        ActionPresentationQueue.Instance.ClearQueuedEvents();
        unit.ActionPoint.RefillForTurn();unit.SwordThrowAction.SelectSwordThrowAction();
        Check(ActionRangeOverlay.TryGetSelection(unit,out _,out center,out range)&&center==landing&&range==unit.SwordThrowAction.SwordThrowRange,"다음 투척 범위는 배치된 검 기준");
        renderer.Refresh(unit,EnemyRegistry.Instance,null);
        Check((GridPosition)Get(renderer,"lastOrigin")==landing,"검 이동 후 표시 중심 갱신");
        unit.CancelAllActionSelections();renderer.Refresh(unit,EnemyRegistry.Instance,null);
        Check(boundary.vertexCount==0&&targetMesh.vertexCount==0,"행동 취소 시 표시 정리");

        var hackable=HackableRegistry.Instance.Hackables.First(h=>h!=null&&!h.IsHacked);
        var hp=hackable.GridPosition;
        var nearHack=FindFree(grid,p=>p.ManhattanDistanceTo(hp)==1);
        unit.SwordState.SetDeployedPosition(nearHack);
        Check(PlayerVisionManager.Instance.RefreshVision(null),"배치된 검으로 장치 주변 실제 시야 갱신");
        visible.Add(hp);unit.HackAction.SelectHackAction();
        Check(ActionRangeOverlay.TryGetSelection(unit,out action,out center,out range)&&action==BattleHudActionType.Hack&&center==nearHack&&range==unit.HackAction.HackRange,"해킹 범위는 배치된 검 기준");
        Check(unit.HackAction.TryPreviewTarget(hackable,out var execution,out reason),"공개된 장치 해킹 가능");
        Check(Mathf.Abs(execution.x-hp.x)<=1&&Mathf.Abs(execution.y-hp.y)<=1&&grid.CanEnter(execution),"해킹 실제 실행 공간 검사");
        renderer.Refresh(unit,EnemyRegistry.Instance,hp);
        Check(boundary.vertexCount>0&&targetMesh.vertexCount>0,"해킹 실선과 장치 조준 표시");
        visible.Remove(hp);
        Check(!ActionRangeOverlay.TryDescribeTarget(unit,action,hp,out _,out _,out _),"안개 속 장치 정보 비공개");
        Check(!unit.HackAction.TryExecuteHack(hackable,new ActionResolutionContext(ActionPresentationQueue.Instance))&&unit.ActionPoint.Current==ap,"시야 밖 해킹 거부와 AP 보존");
        visible.Add(hp);
        var remote=FindFree(grid,p=>p.ManhattanDistanceTo(hp)>unit.HackAction.HackRange);
        unit.SwordState.SetDeployedPosition(remote);visible.Add(hp);
        Check(!unit.HackAction.TryPreviewTarget(hackable,out _,out reason)&&reason=="사거리 밖","검 위치 기준 해킹 거리 초과");
        Check(!unit.HackAction.TryExecuteHack(hackable,new ActionResolutionContext(ActionPresentationQueue.Instance))&&unit.ActionPoint.Current==ap,"거리 밖 해킹 거부와 AP 보존");
        unit.SwordState.SetDeployedPosition(nearHack);PlayerVisionManager.Instance.RefreshVision(null);visible.Add(hp);
        { var capture=Capture(unit,overlay,hud,renderer,BattleHudActionType.Hack,hp,"hack",hp); while(capture.MoveNext()) yield return capture.Current; }
        Check(unit.HackAction.TryExecuteHack(hackable,new ActionResolutionContext(ActionPresentationQueue.Instance)),"실제 해킹 승인");
        Check(hackable.IsHacked&&unit.ActionPoint.Current==ap-unit.UnitData.HackActionPointCost&&unit.SwordState.CurrentPosition==execution,"해킹 완료·검 도착·AP 일치");
        visible.Add(hp);
        Check(!unit.HackAction.TryPreviewTarget(hackable,out _,out reason)&&reason=="해킹 완료","이미 해킹한 장치 불가 안내");
        renderer.Refresh(unit,EnemyRegistry.Instance,hp);
        Check(boundary.vertexCount==0,"해킹 실행 후 표시 정리");
        ActionPresentationQueue.Instance.ClearQueuedEvents();
        unit.ActionPoint.RefillForTurn();unit.GunAttackAction.SelectGunAttackAction();overlay.Refresh();
        Check(GameObject.Find("GunRangeBoundary").GetComponent<MeshFilter>().sharedMesh.vertexCount>0&&boundary.vertexCount==0,"총격 전환 시 추가 행동 표시 정리");
        unit.CancelAllActionSelections();
    }

    /// <summary>검증용 사본에서 공개 상태와 실제 시각 위치를 맞춘다.</summary>
    private static void Reveal(EnemyContext enemy,GridPosition p,HashSet<GridPosition> visible)
    {
        visible.Add(p);PlayerVisionPresenter.Instance.SetMovedEnemyVisibility(enemy.GridActor,true);
        ActorPresentationRegistry.Instance.TryGetVisual(enemy.GridActor,out var visual);
        visual.SetVisionAlpha(1);visual.SetPresentationPosition(GridManager.Instance.GridToWorld(p));
    }

    /// <summary>검증 조건에 맞는 빈 칸을 찾고 없으면 실패한다.</summary>
    private static GridPosition FindFree(GridManager grid,Func<GridPosition,bool> predicate)
    {
        for(int x=0;x<grid.Width;x++)for(int y=0;y<grid.Height;y++)
        {var p=new GridPosition(x,y);if(predicate(p)&&grid.CanEnter(p))return p;}
        throw new InvalidOperationException("검증용 빈 칸이 없습니다.");
    }

    /// <summary>검증용 카메라에서 팝업 제거와 하단 상태를 확인하고 화면을 저장한다.</summary>
    private static IEnumerator Capture(TacticalUnitContext unit,BattleHudTacticalOverlayController overlay,BattleHudController hud,
        ActionRangeOverlay renderer,BattleHudActionType action,GridPosition target,string filename,GridPosition cameraCenter)
    {
        hud.enabled=false;
        Camera.main.transform.position=GridManager.Instance.GridToWorld(cameraCenter)+new Vector3(1,-1,-10);
        Camera.main.orthographicSize=5;
        double settle=EditorApplication.timeSinceStartup+.6;
        while(EditorApplication.timeSinceStartup<settle)yield return null;
        overlay.Refresh();renderer.Refresh(unit,EnemyRegistry.Instance,target);
        var baseHud=UnityEngine.Object.FindFirstObjectByType<BattleHudBaseController>();
        baseHud.Refresh(unit,true);
        typeof(BattleHudBaseController).GetMethod("RefreshAdditionalTargetInfo",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(baseHud,new object[]{unit,action,target});
        Check(((TMPro.TMP_Text)Get(baseHud,"actionNameText")).text.Contains("가능"),filename+" 하단 상태 표시");
        Check(!((RectTransform)Get(overlay,"hitPreviewRoot")).gameObject.activeSelf&&!((UnityEngine.UI.Image)Get(overlay,"targetBracket")).enabled,filename+" 팝업·중복 대상 표시 제거");
        ScreenCapture.CaptureScreenshot(Output+"/"+filename+".png");
        double until=EditorApplication.timeSinceStartup+.7;
        while(EditorApplication.timeSinceStartup<until)yield return null;
    }

    /// <summary>검증용 사본의 내부 상태를 읽는다.</summary>
    private static object Get(object target,string field)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    /// <summary>검사 실패를 알리거나 통과 결과를 누적한다.</summary>
    private static void Check(bool valid,string label)
    {if(!valid)throw new InvalidOperationException(label);results.Add("PASS "+label);}
}


