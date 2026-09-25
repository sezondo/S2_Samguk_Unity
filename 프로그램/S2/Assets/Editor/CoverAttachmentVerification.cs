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
public static class CoverAttachmentVerification
{
    // 도메인 리로드 이후에도 검증 시작 요청을 전달하는 키다.
    private const string PendingKey = "S2.CoverAttachmentVerification.Pending";
    // 검사 결과와 현재 실행 중인 단계다.
    private static readonly List<string> results = new();
    private static IEnumerator routine;
    // 프레임 및 연출 완료를 기다리는 편집기 시간이다.
    private static double nextStep;

    /// <summary>플레이 전환 후 검증 루틴을 연결한다.</summary>
    static CoverAttachmentVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// <summary>실제 씬의 두 엄폐 컴포넌트에 접촉 설정을 명시적으로 저장한다.</summary>
    [MenuItem("Tools/S2/Configure Cover Attachment")]
    public static void Configure()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("씬의 미저장 변경을 먼저 처리해야 합니다.");
        var covers = UnityEngine.Object.FindObjectsByType<LowCoverIdlePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (covers.Length != 2) throw new InvalidOperationException("설정할 엄폐 컴포넌트가 두 개여야 합니다.");
        foreach (var cover in covers)
        {
            var data = new SerializedObject(cover);
            data.FindProperty("lowCoverContactOffset").vector2Value = new Vector2(0.9f, 0.35f);
            data.FindProperty("wallCoverContactOffset").vector2Value = new Vector2(0.55f, 0.6f);
            data.FindProperty("surfaceGap").floatValue = 0.02f;
            data.FindProperty("transitionDuration").floatValue = 0.15f;
            data.FindProperty("lowCoverTransitionDuration").floatValue = 0.24f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cover);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
    }

    /// <summary>사용자가 명시적으로 실행한 경우에만 실제 씬의 일시적인 플레이 검증을 시작한다.</summary>
    [MenuItem("Tools/S2/Verify Cover Attachment")]
    public static void Verify()
    {
        RequireEditScene();
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("검증 전 씬의 미저장 변경을 먼저 처리해야 합니다.");
        Directory.CreateDirectory("Temp/CoverAttachment");
        File.WriteAllText("Temp/CoverAttachment/verification.txt", "RUNNING");
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
        File.WriteAllText("Temp/CoverAttachment/verification.txt",
            (error == null ? "PASS" : "FAIL: " + error) + "\n" + string.Join("\n", results));
        if (error == null) Debug.Log($"엄폐 부착 검증 {results.Count}개 통과");
        else Debug.LogError("엄폐 부착 검증 실패: " + error);
        (routine as IDisposable)?.Dispose();
        routine = null;
        EditorApplication.isPlaying = false;
    }

    /// <summary>실제 씬 참조·Animator·연출 큐로 주요 엄폐 전환을 검증한다.</summary>
    private static IEnumerator Run()
    {
        Application.runInBackground = true;
        // 적에게도 엄폐가 연결되어 있으므로 이 검증의 대상인 플레이어·NPC만 선택한다.
        var covers = Array.FindAll(UnityEngine.Object.FindObjectsByType<LowCoverIdlePresenter>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID),
            cover => Read<TacticalUnitContext>(cover, "unitContext") != null);
        Require(covers.Length == 2, "플레이어와 NPC 엄폐 참조");
        // 원래 꺼져 있는 NPC도 플레이 사본에서만 활성화하여 같은 구현을 검사한다.
        foreach (var cover in covers) cover.transform.root.gameObject.SetActive(true);
        yield return 0.5f;
        double introDeadline = EditorApplication.timeSinceStartup + 40.0;
        while (ActionPresentationQueue.Instance.IsPlaying && EditorApplication.timeSinceStartup < introDeadline) yield return 0.1f;
        var tile = ScriptableObject.CreateInstance<Tile>();
        var directions = new[] { GridPosition.Left, GridPosition.Right, GridPosition.Up, GridPosition.Down };
        foreach (var cover in covers)
        {
            var unit = Read<TacticalUnitContext>(cover, "unitContext");
            var grid = Read<GridManager>(cover, "gridManager");
            var visual = Read<ActorVisualController>(cover, "visualController");
            var queue = Read<ActionPresentationQueue>(cover, "presentationQueue");
            Require(cover.enabled && unit.IsAlive && !queue.IsPlaying, unit.name + " 초기화");
            // 기존 전투 지역에서 떨어진 테스트 구역을 플레이 사본에서만 비운다.
            var center = new GridPosition(4 + Array.IndexOf(covers, cover) * 8, 4);
            foreach (var map in new[] { grid.WallLogicTilemap, grid.LowObstacleLogicTilemap, grid.BoundaryLogicTilemap })
                for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++)
                    map.SetTile(map.WorldToCell(grid.GridToWorld(center + new GridPosition(x, y))), null);
            Rebuild(grid);
            Require(unit.GridActor.TryMoveTo(center), "테스트 칸 이동");
            visual.BeginMovePresentation();
            visual.SetPresentationPosition(grid.GridToWorld(center));
            visual.EndMovePresentation();
            visual.TryPlayAnimationState("Idle", 0f);
            yield return 0.25f;
            Vector3 logicalWorld = unit.GridActor.transform.position;
            int hp = unit.Health.CurrentHitPoint;
            int ap = unit.ActionPoint.Current;
            foreach (bool low in new[] { true, false })
            {
                foreach (var direction in directions)
                {
                    ClearAround(grid, center);
                    cover.RefreshCover();
                    yield return 0.25f;
                    visual.FaceRight();
                    SetTile(grid, low ? grid.LowObstacleLogicTilemap : grid.WallLogicTilemap, center + direction, tile);
                    cover.RefreshCover();
                    yield return 0.25f;
                    Require(cover.SelectedCoverPosition == center + direction && cover.HasSelectedCover, $"{unit.name} {(low ? "낮은" : "높은")} {direction} 선택");
                    Require(Vector3.Dot(visual.CoverWorldOffset, new Vector3(direction.x, direction.y, 0)) > 0.01f, "엄폐 방향 위치 보정");
                    Require(visual.CurrentAnimationStateName == (low ? "LowCoverIdle" : "WallCoverIdle"), "엄폐 자세");
                    Require(visual.IsFacingRight == (direction.x >= 0), "좌우 엄폐 방향 및 위아래 방향 보존");
                    visual.FaceLeft(); visual.FaceRight();
                    Require(visual.IsFacingRight == (direction.x >= 0), "일반 방향보다 엄폐 방향 우선");
                    Require(unit.GridActor.GridPosition == center && unit.GridActor.transform.position == logicalWorld &&
                        grid.TryGetActorAt(center, out var occupant) && occupant == unit.GridActor && unit.Health.CurrentHitPoint == hp && unit.ActionPoint.Current == ap,
                        "논리 위치·점유·HP·AP 보존");
                }
            }
            ClearAround(grid, center); cover.RefreshCover(); yield return 0.25f;
            SetTile(grid, grid.WallLogicTilemap, center + GridPosition.Right, tile);
            SetTile(grid, grid.WallLogicTilemap, center + GridPosition.Left, tile);
            cover.RefreshCover(); yield return 0.25f;
            Require(cover.SelectedCoverPosition == center + GridPosition.Left, "동률 고정 선택");
            SetTile(grid, grid.LowObstacleLogicTilemap, center + GridPosition.Up, tile);
            cover.RefreshCover();
            Require(cover.SelectedCoverPosition == center + GridPosition.Left, "새 후보보다 기존 선택 유지");
            SetTile(grid, grid.WallLogicTilemap, center + GridPosition.Left, null);
            cover.RefreshCover(); yield return 0.25f;
            Require(cover.SelectedCoverPosition == center + GridPosition.Up, "선택 소멸 시 가까운 후보 재선택");
            ClearAround(grid, center); cover.RefreshCover(); yield return 0.25f;
            SetTile(grid, grid.BoundaryLogicTilemap, center + GridPosition.Left, tile);
            cover.RefreshCover(); yield return 0.25f;
            Require(!cover.HasSelectedCover && visual.CoverWorldOffset.sqrMagnitude < 0.00001f, "경계 제외 및 원위치 복귀");
            SetTile(grid, grid.BoundaryLogicTilemap, center + GridPosition.Left, null);
            SetTile(grid, grid.WallLogicTilemap, center + GridPosition.Left, tile);
            cover.RefreshCover(); yield return 0.25f;
            Vector3 contactPosition = visual.transform.position;
            visual.BeginCombatPresentation(center, center + GridPosition.Right, true, false);
            Require(visual.IsFacingRight, "공격 방향 최우선");
            yield return 0.2f;
            Require(visual.transform.position == contactPosition, "공격 위치 유지");
            visual.EndCombatPresentation();
            Require(!visual.IsFacingRight, "공격 후 엄폐 방향 복귀");
            visual.BeginCombatPresentation(center, center + GridPosition.Right, false, false);
            yield return 0.2f;
            Require(!visual.IsFacingRight && visual.transform.position == contactPosition, "피격 시 엄폐 방향·위치 유지");
            visual.EndCombatPresentation();
            // 두 칸 이동은 실제 큐로 실행하며 끝 칸의 오른쪽 엄폐로 다시 붙는다.
            var end = center + new GridPosition(0, -2);
            SetTile(grid, grid.LowObstacleLogicTilemap, end + GridPosition.Right, tile);
            Require(unit.GridActor.TryMoveTo(end), "이동 결과 논리 반영");
            queue.Enqueue(PresentationEvent.MoveActor(unit.GridActor, center, center + GridPosition.Down, MovePresentationPhase.Start));
            queue.Enqueue(PresentationEvent.MoveActor(unit.GridActor, center + GridPosition.Down, end, MovePresentationPhase.End));
            Require(queue.PlayQueuedEvents(), "실제 이동 큐 시작");
            Require(visual.IsMovingPresentation && (visual.transform.position - contactPosition).sqrMagnitude < 0.00001f, "이동 시작 위치 연속성");
            yield return 1.5f;
            Require(!queue.IsPlaying && !visual.IsMovingPresentation && cover.SelectedCoverPosition == end + GridPosition.Right && visual.IsFacingRight,
                "연속 이동 종료·엄폐 재부착");
            Require((visual.transform.position - grid.GridToWorld(end) - visual.CoverWorldOffset).sqrMagnitude < 0.00001f, "이동 후 오프셋 중복 없음");
        }
        // 전투 통합 Presenter에서 공격자 엄폐 복귀와 피격자의 사망 잔해 위치를 검증한다.
        var first = Read<TacticalUnitContext>(covers[0], "unitContext");
        var second = Read<TacticalUnitContext>(covers[1], "unitContext");
        var firstVisual = Read<ActorVisualController>(covers[0], "visualController");
        var secondVisual = Read<ActorVisualController>(covers[1], "visualController");
        var combatQueue = Read<ActionPresentationQueue>(covers[0], "presentationQueue");
        Vector3 deathPosition = firstVisual.transform.position;
        combatQueue.Enqueue(PresentationEvent.CombatAction(second.GridActor, first.GridActor, second.GridActor.GridPosition,
            first.GridActor.GridPosition, AttackPresentationKind.PlayerGun, AttackResult.GuaranteedHit(),
            new DamageResult(true, 100, 100, 0, false, true), true));
        Require(combatQueue.PlayQueuedEvents(), "실제 전투 큐 시작");
        // 전투 시작에 추가된 준비 박자가 끝난 뒤 실제 공격 방향을 검사한다.
        yield return 0.15f;
        Require(!secondVisual.IsFacingRight, "전투 Presenter의 공격 방향 우선");
        yield return 2.0f;
        Require(!combatQueue.IsPlaying && secondVisual.IsFacingRight, "전투 큐 종료 후 공격자 엄폐 복귀");
        Require(firstVisual.IsDeathPresentation && firstVisual.CurrentAnimationStateName == "Death" && firstVisual.transform.position == deathPosition,
            "사망 잔해 자세·접촉 위치 유지");
        covers[0].RefreshCover(); yield return 0.25f;
        Require(!covers[0].HasSelectedCover && firstVisual.transform.position == deathPosition, "사망 후 엄폐 선택 해제·잔해 위치 보존");
    }

    /// <summary>현재 칸에 인접한 엄폐 타일만 검증용으로 제거한다.</summary>
    private static void ClearAround(GridManager grid, GridPosition center)
    {
        foreach (var direction in new[] { GridPosition.Left, GridPosition.Right, GridPosition.Up, GridPosition.Down })
            foreach (var map in new[] { grid.WallLogicTilemap, grid.LowObstacleLogicTilemap }) SetTile(grid, map, center + direction, null);
    }

    /// <summary>플레이 사본의 논리 타일을 바꾸고 보드 상태를 재구축한다.</summary>
    private static void SetTile(GridManager grid, Tilemap map, GridPosition position, TileBase tile)
    {
        map.SetTile(map.WorldToCell(grid.GridToWorld(position)), tile);
        Rebuild(grid);
    }

    /// <summary>실제 런타임의 타일 분류·점유 유지 경로를 사용한다.</summary>
    private static void Rebuild(GridManager grid) => typeof(GridManager).GetMethod("RebuildCellStates", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(grid, null);

    /// <summary>검증 대상이 직렬화해 둔 명시적 참조를 읽는다.</summary>
    private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

    /// <summary>각 동작 검사의 통과 기록을 남기고 실패 즉시 중단한다.</summary>
    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        results.Add(description);
    }
}
