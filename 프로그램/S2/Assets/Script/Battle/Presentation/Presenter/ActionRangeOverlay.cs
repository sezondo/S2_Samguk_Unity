using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>검 투척·근접·해킹의 실제 범위와 공개된 대상 정보를 팝업 없이 표시한다.</summary>
public sealed class ActionRangeOverlay : IDisposable
{
    // 공통 선 머티리얼을 사용하는 안개 아래 범위·대상 메시다.
    private readonly MovementOverlayMesh rangeMesh;
    private readonly MovementOverlayMesh targetMesh;
    // 외곽선 생성용 칸 집합과 범위 갱신 기준이다.
    private readonly HashSet<GridPosition> area = new();
    private TacticalUnitContext lastUnit;
    private BattleHudActionType? lastAction;
    private GridPosition lastOrigin;
    private int lastRange;
    // 총격과 같은 가능/불가 색상, 새 의심을 유발하는 투척 경고 색상이다.
    private static readonly Color AvailableColor = new(1f, .65f, .32f, .95f);
    private static readonly Color UnavailableColor = new(.55f, .61f, .6f, .9f);
    private static readonly Color WarningColor = new(1f, .35f, .12f, .95f);

    /// <summary>기존 HUD가 제공한 머티리얼로 표시 메시를 준비한다.</summary>
    public ActionRangeOverlay(Material material)
    {
        rangeMesh = new MovementOverlayMesh("ActionRangeBoundary", material, 1900);
        targetMesh = new MovementOverlayMesh("ActionTargetPreview", material, 1901);
    }

    /// <summary>선택된 추가 행동의 종류·기준 칸·데이터 사거리를 반환한다.</summary>
    public static bool TryGetSelection(TacticalUnitContext unit, out BattleHudActionType action, out GridPosition origin, out int range)
    {
        action = default; origin = default; range = 0;
        if (unit == null || !unit.IsAlive) return false;
        if (unit.MeleeAttackAction != null && unit.MeleeAttackAction.IsMeleeAttackSelected)
        { action = BattleHudActionType.Melee; origin = unit.GridActor.GridPosition; range = 1; return true; }
        if (unit.SwordThrowAction != null && unit.SwordThrowAction.IsSwordThrowSelected)
        { action = BattleHudActionType.SwordThrow; origin = unit.SwordState.CurrentPosition; range = unit.SwordThrowAction.SwordThrowRange; return true; }
        if (unit.HackAction != null && unit.HackAction.IsHackSelected)
        { action = BattleHudActionType.Hack; origin = unit.HackAction.HackOriginPosition; range = unit.HackAction.HackRange; return true; }
        return false;
    }

    /// <summary>적의 논리 시야·표시 스냅샷·알파를 확인해 숨은 대상 정보를 차단한다.</summary>
    public static bool IsActorVisible(GridActor actor)
    {
        if (actor == null || PlayerVisionManager.Instance == null || !PlayerVisionManager.Instance.IsVisible(actor.GridPosition) ||
            PlayerVisionPresenter.Instance == null || !PlayerVisionPresenter.Instance.ShouldShowEnemyActorInSnapshot(actor) ||
            ActorPresentationRegistry.Instance == null || !ActorPresentationRegistry.Instance.TryGetVisual(actor, out var visual)) return false;
        return !visual.IsDeathPresentation && visual.VisionAlpha > .01f;
    }

    /// <summary>표시 가능한 대상만 설명한다. 검은 미탐색 빈 칸에도 투척 가능하지만 숨은 적 정보는 읽지 않는다.</summary>
    public static bool TryDescribeTarget(TacticalUnitContext unit, BattleHudActionType action, GridPosition target,
        out bool valid, out string reason, out bool warning)
    {
        valid = false; reason = ""; warning = false;
        var grid = GridManager.Instance;
        if (grid == null || !grid.IsInside(target)) return false;
        if (action == BattleHudActionType.SwordThrow)
        {
            valid = unit.SwordThrowAction.TryPreviewTarget(target, out reason);
            if (valid)
            {
                if (PlayerVisionManager.Instance != null && PlayerVisionManager.Instance.IsVisible(target) &&
                    grid.TryGetActorAt(target, out var actor) && actor != unit.GridActor && IsActorVisible(actor) && actor.GetComponent<IDamageable>() != null)
                    reason = "타격 가능";
                warning = unit.SwordThrowAction.WillCauseSuspicion(target);
                if (warning) reason += " · 새 의심 예상";
            }
            return true;
        }
        if (PlayerVisionManager.Instance == null || !PlayerVisionManager.Instance.IsVisible(target)) return false;
        if (action == BattleHudActionType.Melee)
        {
            valid = unit.MeleeAttackAction.TryPreviewTarget(target, out var actor, out reason);
            return actor != null && IsActorVisible(actor);
        }
        if (action == BattleHudActionType.Hack && PlayerVisionPresenter.Instance != null &&
            PlayerVisionPresenter.Instance.IsPositionPresentedVisible(target) && HackableRegistry.Instance != null &&
            HackableRegistry.Instance.TryGetHackableAt(target, out var hackable))
        {
            valid = unit.HackAction.TryPreviewTarget(hackable, out _, out reason);
            return true;
        }
        return false;
    }

    /// <summary>실제 행동 기준 범위와 가능한 대상들을 갱신한다.</summary>
    public void Refresh(TacticalUnitContext unit, EnemyRegistry enemies, GridPosition? pointer)
    {
        var grid = GridManager.Instance;
        if (grid == null || !TryGetSelection(unit, out var action, out var origin, out int range) ||
            ActionPresentationQueue.Instance == null || ActionPresentationQueue.Instance.IsBusy ||
            TurnManager.Instance == null || !TurnManager.Instance.IsPlayerTurn)
        { Hide(); return; }
        if (lastUnit != unit || lastAction != action || lastOrigin != origin || lastRange != range)
        {
            lastUnit = unit; lastAction = action; lastOrigin = origin; lastRange = range;
            DrawRange(grid, action, origin, range);
        }
        targetMesh.Clear();
        if (action == BattleHudActionType.Hack && HackableRegistry.Instance != null)
        {
            foreach (var hackable in HackableRegistry.Instance.Hackables)
                if (hackable != null) DrawTarget(hackable.GridPosition, false);
        }
        else if (enemies != null)
        {
            foreach (var enemy in enemies.Enemies)
                if (enemy != null && enemy.IsAlive && IsActorVisible(enemy.GridActor)) DrawTarget(enemy.GridActor.GridPosition, false);
        }
        if (pointer.HasValue) DrawTarget(pointer.Value, true);
        targetMesh.Apply();

        // 포인터 대상은 한 번만 그린다. 미탐색 칸은 메시도 숨기되 하단의 일반 투척 가능 여부는 유지한다.
        void DrawTarget(GridPosition target, bool selected)
        {
            if (!selected && pointer.HasValue && pointer.Value == target) return;
            if (PlayerVisionManager.Instance == null || !PlayerVisionManager.Instance.IsVisible(target) ||
                !TryDescribeTarget(unit, action, target, out bool valid, out string reason, out bool warning) || (!valid && !selected)) return;
            Vector3 to = grid.GridToWorld(target); to.z = -.09f;
            Color color = !valid ? UnavailableColor : warning ? WarningColor : AvailableColor;
            targetMesh.Bracket(to, grid.CellSize * (selected ? 1f : .85f), grid.CellSize * .033f, color);
            if (!selected || !valid) return;
            Vector3 from = grid.GridToWorld(origin); from.z = -.09f;
            targetMesh.Line(from, to, grid.CellSize * .025f, color);
        }
    }

    /// <summary>근접은 주변 8칸, 다른 행동은 맨해튼 거리 외곽을 실선으로 그린다.</summary>
    private void DrawRange(GridManager grid, BattleHudActionType action, GridPosition origin, int range)
    {
        area.Clear(); rangeMesh.Clear();
        for (int x = Mathf.Max(0, origin.x-range); x <= Mathf.Min(grid.Width-1, origin.x+range); x++)
        for (int y = Mathf.Max(0, origin.y-range); y <= Mathf.Min(grid.Height-1, origin.y+range); y++)
        {
            var p = new GridPosition(x,y);
            // 중심 칸은 외곽선을 잇기 위한 표시 전용이며 실제 대상 판정은 행동이 담당한다.
            if (action == BattleHudActionType.Melee || origin.ManhattanDistanceTo(p) <= range) area.Add(p);
        }
        float size = grid.CellSize, h = size*.5f;
        foreach (var p in area)
        {
            Vector3 c = grid.GridToWorld(p); c.z = -.05f;
            Edge(p, GridPosition.Up,c+new Vector3(-h,h),c+new Vector3(h,h));
            Edge(p, GridPosition.Down,c+new Vector3(-h,-h),c+new Vector3(h,-h));
            Edge(p, GridPosition.Left,c+new Vector3(-h,-h),c+new Vector3(-h,h));
            Edge(p, GridPosition.Right,c+new Vector3(h,-h),c+new Vector3(h,h));
        }
        rangeMesh.Apply();
        void Edge(GridPosition p,GridPosition d,Vector3 a,Vector3 b)
        { if (!area.Contains(p+d)) rangeMesh.Line(a,b,size*.028f,AvailableColor); }
    }

    /// <summary>선택 취소·행동 전환·연출 시 표시와 범위 캐시를 정리한다.</summary>
    public void Hide() { lastUnit = null; lastAction = null; rangeMesh.Hide(); targetMesh.Hide(); }
    /// <summary>HUD 제거 시 소유 메시를 해제한다.</summary>
    public void Dispose() { rangeMesh.Dispose(); targetMesh.Dispose(); }
}

