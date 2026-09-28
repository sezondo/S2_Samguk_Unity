using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>총격 선택 중 사거리 경계와 현재 공개된 적의 조준 표시를 그린다.</summary>
public sealed class GunRangeOverlay : IDisposable
{
    // 범위와 대상 표시를 분리해 범위 변경 때만 경계를 다시 만든다.
    private readonly MovementOverlayMesh rangeMesh;
    private readonly MovementOverlayMesh targetMesh;
    // 현재 사거리 안의 칸 집합과 마지막 표시 기준이다.
    private readonly HashSet<GridPosition> area = new();
    private TacticalUnitContext lastUnit;
    private GridPosition lastPosition;
    private int lastRange = -1;
    // 총격 경계·가능 대상과 공격 불가 대상 색상이다.
    private static readonly Color AimColor = new(1f, .65f, .32f, .95f);
    private static readonly Color UnavailableColor = new(.55f, .61f, .6f, .9f);

    /// <summary>HUD가 명시적으로 제공하는 머티리얼로 안개 아래에 선을 준비한다.</summary>
    public GunRangeOverlay(Material material)
    {
        rangeMesh = new MovementOverlayMesh("GunRangeBoundary", material, 1900);
        targetMesh = new MovementOverlayMesh("GunTargetPreview", material, 1901);
    }

    /// <summary>현재 총격 선택과 적 표시 상태를 반영한다. 숨은 적은 조회 결과를 그리지 않는다.</summary>
    public void Refresh(TacticalUnitContext unit, EnemyRegistry enemies, GridPosition? pointer)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || unit == null || !unit.IsAlive || unit.GunAttackAction == null ||
            !unit.GunAttackAction.IsGunAttackSelected || ActionPresentationQueue.Instance == null ||
            ActionPresentationQueue.Instance.IsBusy || TurnManager.Instance == null || !TurnManager.Instance.IsPlayerTurn)
        { Hide(); return; }
        var gun = unit.GunAttackAction;
        if (lastUnit != unit || lastPosition != unit.GridActor.GridPosition || lastRange != gun.GunAttackRange)
        {
            lastUnit = unit; lastPosition = unit.GridActor.GridPosition; lastRange = gun.GunAttackRange;
            DrawRange(grid);
        }
        targetMesh.Clear();
        var vision = PlayerVisionPresenter.Instance;
        var registry = ActorPresentationRegistry.Instance;
        float size = grid.CellSize;
        if (vision != null && registry != null)
        foreach (EnemyContext enemy in enemies.Enemies)
        {
            if (enemy == null || !enemy.IsAlive || !vision.ShouldShowEnemyActorInSnapshot(enemy.GridActor) ||
                !registry.TryGetVisual(enemy.GridActor, out var visual) || visual.VisionAlpha <= .01f ||
                PlayerVisionManager.Instance == null || !PlayerVisionManager.Instance.IsVisible(enemy.GridActor.GridPosition)) continue;
            GridPosition p = enemy.GridActor.GridPosition;
            bool selected = pointer.HasValue && pointer.Value == p;
            bool available = gun.TryPreviewTarget(p, out _, out string reason);
            if (!selected && !available) continue;
            Vector3 to = grid.GridToWorld(p); to.z = -.09f;
            targetMesh.Bracket(to, size * (selected ? 1 : .85f), size * .033f, available ? AimColor : UnavailableColor);
            if (!selected) continue;
            Vector3 from = grid.GridToWorld(lastPosition); from.z = -.09f;
            if (available) targetMesh.Line(from, to, size * .025f, AimColor);
            else if (reason == "벽에 막힘" && GridLineOfSight.TryGetFirstBlockingCell(grid, lastPosition, p, out GridPosition blocked))
            {
                Vector3 stop = grid.GridToWorld(blocked); stop.z = -.09f;
                targetMesh.Line(from, stop, size * .025f, AimColor);
                targetMesh.Line(stop, to, size * .018f, UnavailableColor, size * .2f, .3f);
                Vector3 d = new(size * .09f, size * .09f);
                targetMesh.Line(stop - d, stop + d, size * .035f, AimColor);
                d.y = -d.y;
                targetMesh.Line(stop - d, stop + d, size * .035f, AimColor);
            }
        }
        targetMesh.Apply();
    }

    /// <summary>이동 가능성과 무관한 실제 무기 맨해튼 사거리의 외곽만 실선으로 표시한다.</summary>
    private void DrawRange(GridManager grid)
    {
        rangeMesh.Clear(); area.Clear();
        for (int x = Mathf.Max(0, lastPosition.x - lastRange); x <= Mathf.Min(grid.Width - 1, lastPosition.x + lastRange); x++)
        for (int y = Mathf.Max(0, lastPosition.y - lastRange); y <= Mathf.Min(grid.Height - 1, lastPosition.y + lastRange); y++)
        {
            GridPosition p = new(x, y);
            if (lastPosition.ManhattanDistanceTo(p) <= lastRange) area.Add(p);
        }
        float size = grid.CellSize, h = size * .5f;
        foreach (GridPosition p in area)
        {
            Vector3 c = grid.GridToWorld(p); c.z = -.05f;
            Edge(p, GridPosition.Up, c + new Vector3(-h,h), c + new Vector3(h,h));
            Edge(p, GridPosition.Down, c + new Vector3(-h,-h), c + new Vector3(h,-h));
            Edge(p, GridPosition.Left, c + new Vector3(-h,-h), c + new Vector3(-h,h));
            Edge(p, GridPosition.Right, c + new Vector3(h,-h), c + new Vector3(h,h));
        }
        rangeMesh.Apply();
        // 이웃 칸이 사거리 밖인 면만 그린다.
        void Edge(GridPosition p, GridPosition offset, Vector3 a, Vector3 b)
        {
            if (!area.Contains(p + offset)) rangeMesh.Line(a, b, size * .028f, AimColor);
        }
    }

    /// <summary>행동 전환·연출·선택 해제 시 범위와 조준 표시를 정리한다.</summary>
    public void Hide()
    {
        if (lastUnit == null) return;
        lastUnit = null; lastRange = -1; rangeMesh.Hide(); targetMesh.Hide();
    }

    /// <summary>소유한 런타임 메시를 해제한다.</summary>
    public void Dispose() { rangeMesh.Dispose(); targetMesh.Dispose(); }
}
