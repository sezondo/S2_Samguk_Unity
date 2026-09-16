using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

/// <summary>
/// 격자 보드의 크기, 좌표 변환, 고정 이동불가 상태, 칸 점유와 동적 시야 차단 상태를 관리한다.
/// 벽·낮은 장애물·경계는 이동을 막고 벽·경계는 시야를 막는다. 경계는 엄폐에 포함하지 않는다.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Board")]
    // 보드의 가로 칸 수다.
    [SerializeField] private int width = 10;
    // 보드의 세로 칸 수다.
    [SerializeField] private int height = 10;
    // 그리드 한 칸이 Unity 월드에서 차지하는 크기다.
    [SerializeField] private float cellSize = 1f;
    // 그리드 (0, 0) 칸의 기준 월드 위치다.
    [SerializeField] private Vector3 originWorldPosition;

    [Header("Logic Tilemap")]
    // 이동과 시야를 모두 차단하는 벽 칸을 제공하는 필수 논리 타일맵이다.
    [FormerlySerializedAs("logicTilemap")]
    [SerializeField] private Tilemap wallLogicTilemap;
    // 이동은 차단하지만 시야는 통과시키는 낮은 장애물 칸을 제공한다. 낮은 장애물이 없는 씬은 비워둘 수 있다.
    [SerializeField] private Tilemap lowObstacleLogicTilemap;
    // 이동·시야만 막고 엄폐 효과나 자세를 제공하지 않는 경계다. 경계를 사용하지 않는 씬은 비워둘 수 있다.
    [SerializeField] private Tilemap boundaryLogicTilemap;

    [Header("Gizmos")]
    // Scene 뷰에서 보드 선과 점유 칸을 그릴지 정한다.
    [SerializeField] private bool drawGizmos = true;
    // Scene 뷰에 표시할 보드 선 색이다.
    [SerializeField] private Color gridColor = new(0.25f, 0.75f, 1f, 0.35f);
    // Scene 뷰에 표시할 고정 이동불가 칸 색이다.
    [SerializeField] private Color blockedColor = new(0.25f, 0.25f, 0.25f, 0.6f);
    // Scene 뷰에 표시할 점유 칸 색이다.
    [SerializeField] private Color occupiedColor = new(1f, 0.35f, 0.25f, 0.45f);

    // 보드 좌표별 칸 상태를 저장하는 런타임 상태 DB다.
    private readonly Dictionary<GridPosition, GridCellState> cellByPosition = new();
    // 닫힌 문처럼 런타임에 시야를 막는 GridActor 목록이다. 일반 캐릭터 점유자는 등록하지 않는다.
    private readonly HashSet<GridActor> dynamicSightBlockers = new();

    // 씬에서 사용하는 단일 그리드 매니저 인스턴스다.
    public static GridManager Instance { get; private set; }

    // 외부에서 읽는 보드 가로 칸 수다.
    public int Width => width;
    // 외부에서 읽는 보드 세로 칸 수다.
    public int Height => height;
    // 외부에서 읽는 한 칸의 월드 크기다.
    public float CellSize => cellSize;
    // 이동과 시야를 모두 차단하는 벽 논리 타일맵이다.
    public Tilemap WallLogicTilemap => wallLogicTilemap;
    // 이동만 차단하는 낮은 장애물 논리 타일맵이다.
    public Tilemap LowObstacleLogicTilemap => lowObstacleLogicTilemap;
    // 엄폐 판정에서 제외되는 선택적 맵 경계 타일맵이다.
    public Tilemap BoundaryLogicTilemap => boundaryLogicTilemap;

    // 동적 시야 차단물이 등록되거나 해제될 때 발생한다.
    public event Action SightBlockingChanged;

    /// <summary>
    /// 씬의 단일 GridManager 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(GridManager)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        Instance = this;
        RebuildCellStates();
    }

    /// <summary>
    /// 인스펙터에서 보드 설정이 바뀌면 두 논리 타일맵 기준으로 런타임 칸 상태를 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        if (wallLogicTilemap != null && width > 0 && height > 0 && cellSize > 0f)
        {
            RebuildCellStates();
        }
    }

    /// <summary>
    /// 현재 인스턴스가 제거될 때 싱글톤 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 지정한 칸이 보드 범위 안에 있는지 확인한다.
    /// </summary>
    public bool IsInside(GridPosition position)
    {
        return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
    }

    /// <summary>
    /// 보드 칸 좌표를 Unity 월드 좌표로 변환한다.
    /// </summary>
    public Vector3 GridToWorld(GridPosition position)
    {
        return originWorldPosition + new Vector3(position.x * cellSize, position.y * cellSize, 0f);
    }

    /// <summary>
    /// Unity 월드 좌표를 가장 가까운 보드 칸 좌표로 변환한다.
    /// </summary>
    public GridPosition WorldToGrid(Vector3 worldPosition)
    {
        Vector3 localPosition = worldPosition - originWorldPosition;
        int x = Mathf.RoundToInt(localPosition.x / cellSize);
        int y = Mathf.RoundToInt(localPosition.y / cellSize);
        return new GridPosition(x, y);
    }

    /// <summary>
    /// 지정한 칸에 새 말이 들어갈 수 있는지 확인한다.
    /// </summary>
    public bool CanEnter(GridPosition position)
    {
        return TryGetCellState(position, out GridCellState cellState) && cellState.CanEnter;
    }

    /// <summary>
    /// 지정한 칸이 고정 이동불가 칸인지 확인한다.
    /// </summary>
    public bool IsBlocked(GridPosition position)
    {
        return TryGetCellState(position, out GridCellState cellState) && cellState.IsBlocked;
    }

    /// <summary>
    /// 지정한 칸이 이동은 막지만 시야는 통과시키는 낮은 장애물 칸인지 확인한다.
    /// </summary>
    public bool IsLowObstacle(GridPosition position)
    {
        return IsInside(position) && HasTileAt(lowObstacleLogicTilemap, position);
    }

    /// <summary>보드 안의 지정한 칸에 벽 논리 타일이 있는지 확인한다.</summary>
    public bool IsWall(GridPosition position)
    {
        return IsInside(position) && HasTileAt(wallLogicTilemap, position);
    }

    /// <summary>보드 안의 기준 칸에서 상하좌우 한 칸에 벽이 있는지 확인한다.</summary>
    public bool HasAdjacentWall(GridPosition position)
    {
        return IsInside(position) &&
               (IsWall(position + GridPosition.Up) ||
                IsWall(position + GridPosition.Down) ||
                IsWall(position + GridPosition.Left) ||
                IsWall(position + GridPosition.Right));
    }

    /// <summary>보드 안의 기준 칸에서 상하좌우 한 칸에 낮은 엄폐물이 있는지 확인한다.</summary>
    public bool HasAdjacentLowObstacle(GridPosition position)
    {
        return IsInside(position) &&
               (IsLowObstacle(position + GridPosition.Up) ||
                IsLowObstacle(position + GridPosition.Down) ||
                IsLowObstacle(position + GridPosition.Left) ||
                IsLowObstacle(position + GridPosition.Right));
    }

    /// <summary>
    /// 지정한 칸이 다른 GridActor에게 점유되어 있는지 확인한다.
    /// </summary>
    public bool IsOccupied(GridPosition position)
    {
        return TryGetCellState(position, out GridCellState cellState) && cellState.IsOccupied;
    }

    /// <summary>
    /// 지정한 칸이 벽·경계 타일 또는 등록된 동적 구조물에 의해 시야가 막힌 칸인지 확인한다.
    /// </summary>
    public bool IsSightBlocked(GridPosition position)
    {
        if (HasTileAt(wallLogicTilemap, position) || HasTileAt(boundaryLogicTilemap, position))
        {
            return true;
        }

        foreach (GridActor sightBlocker in dynamicSightBlockers)
        {
            if (sightBlocker != null &&
                sightBlocker.isActiveAndEnabled &&
                sightBlocker.OccupyCell &&
                sightBlocker.GridPosition == position)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 닫힌 문처럼 현재 시야를 막는 동적 구조물을 등록한다.
    /// </summary>
    public bool RegisterSightBlocker(GridActor sightBlocker)
    {
        if (sightBlocker == null)
        {
            Debug.LogError($"{nameof(GridManager)}에 비어 있는 동적 시야 차단물을 등록할 수 없습니다.", this);
            return false;
        }

        if (!sightBlocker.OccupyCell)
        {
            Debug.LogError($"{nameof(GridManager)}: 동적 시야 차단물 {sightBlocker.name}은 현재 칸 점유가 활성화되어 있어야 합니다.", sightBlocker);
            return false;
        }

        if (!IsInside(sightBlocker.GridPosition))
        {
            Debug.LogError($"{nameof(GridManager)}: 동적 시야 차단물 {sightBlocker.name}의 좌표 {sightBlocker.GridPosition}은 보드 범위 밖입니다.", sightBlocker);
            return false;
        }

        if (!dynamicSightBlockers.Add(sightBlocker))
        {
            return true;
        }

        SightBlockingChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 열린 문처럼 더 이상 시야를 막지 않는 동적 구조물의 등록을 해제한다.
    /// </summary>
    public void UnregisterSightBlocker(GridActor sightBlocker)
    {
        if (sightBlocker == null || !dynamicSightBlockers.Remove(sightBlocker))
        {
            return;
        }

        SightBlockingChanged?.Invoke();
    }

    /// <summary>
    /// 지정한 칸에 등록된 GridActor를 가져온다.
    /// </summary>
    public bool TryGetActorAt(GridPosition position, out GridActor actor)
    {
        if (TryGetCellState(position, out GridCellState cellState) && cellState.OccupiedActor != null)
        {
            actor = cellState.OccupiedActor;
            return true;
        }

        actor = null;
        return false;
    }

    /// <summary>
    /// 지정한 칸의 상태를 가져온다.
    /// </summary>
    public bool TryGetCellState(GridPosition position, out GridCellState cellState)
    {
        return cellByPosition.TryGetValue(position, out cellState);
    }

    /// <summary>
    /// GridActor를 지정한 칸의 점유자로 등록한다.
    /// </summary>
    public bool RegisterActor(GridActor actor, GridPosition position)
    {
        if (actor == null)
        {
            return false;
        }

        if (!IsInside(position))
        {
            Debug.LogWarning($"{nameof(GridManager)}: {actor.name} 오브젝트의 시작 칸 {position}은 보드 범위 밖입니다.", this);
            return false;
        }

        if (IsBlocked(position))
        {
            Debug.LogWarning($"{nameof(GridManager)}: {actor.name} 오브젝트의 시작 칸 {position}은 이동불가 칸입니다.", this);
            return false;
        }

        if (TryGetActorAt(position, out GridActor existingActor) && existingActor != actor)
        {
            Debug.LogWarning($"{nameof(GridManager)}: {position} 칸은 이미 {existingActor.name} 오브젝트가 점유하고 있습니다.", this);
            return false;
        }

        if (!TryGetCellState(position, out GridCellState cellState))
        {
            Debug.LogWarning($"{nameof(GridManager)}: {position} 칸 상태를 찾지 못해 {actor.name} 오브젝트를 등록할 수 없습니다.", this);
            return false;
        }

        cellState.SetOccupiedActor(actor);
        return true;
    }

    /// <summary>
    /// 지정한 칸에서 GridActor 점유 등록을 해제한다.
    /// </summary>
    public void UnregisterActor(GridActor actor, GridPosition position)
    {
        if (actor == null)
        {
            return;
        }

        if (TryGetCellState(position, out GridCellState cellState))
        {
            cellState.TryClearOccupiedActor(actor);
        }
    }

    /// <summary>
    /// GridActor의 점유 칸을 시작 칸에서 목표 칸으로 옮긴다.
    /// </summary>
    public bool TryMoveActor(GridActor actor, GridPosition from, GridPosition to)
    {
        if (actor == null || !TryGetCellState(from, out GridCellState fromCell) || !TryGetCellState(to, out GridCellState toCell))
        {
            return false;
        }

        if (fromCell.OccupiedActor != actor || !toCell.CanEnter)
        {
            return false;
        }

        fromCell.TryClearOccupiedActor(actor);
        toCell.SetOccupiedActor(actor);
        return true;
    }

    /// <summary>
    /// Scene 뷰에서 보드 선과 점유 칸을 시각화한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }
        if (wallLogicTilemap != null && width > 0 && height > 0 && cellSize > 0f)
        {
            RebuildCellStates();
        }

        float safeCellSize = Mathf.Max(0.01f, cellSize);
        Gizmos.color = gridColor;

        for (int x = 0; x <= width; x++)
        {
            Vector3 start = originWorldPosition + new Vector3(x * safeCellSize - safeCellSize * 0.5f, -safeCellSize * 0.5f, 0f);
            Vector3 end = start + new Vector3(0f, height * safeCellSize, 0f);
            Gizmos.DrawLine(start, end);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 start = originWorldPosition + new Vector3(-safeCellSize * 0.5f, y * safeCellSize - safeCellSize * 0.5f, 0f);
            Vector3 end = start + new Vector3(width * safeCellSize, 0f, 0f);
            Gizmos.DrawLine(start, end);
        }

        Gizmos.color = blockedColor;
        foreach (GridCellState cellState in cellByPosition.Values)
        {
            if (cellState.IsBlocked)
            {
                Gizmos.DrawCube(GridToWorld(cellState.Position), Vector3.one * safeCellSize * 0.85f);
            }
        }

        Gizmos.color = occupiedColor;
        foreach (GridCellState cellState in cellByPosition.Values)
        {
            if (cellState.IsOccupied)
            {
                Gizmos.DrawCube(GridToWorld(cellState.Position), Vector3.one * safeCellSize * 0.75f);
            }
        }
    }

    /// <summary>
    /// 보드의 칸 상태를 다시 만들고 벽·낮은 장애물·경계 타일을 이동불가 상태로 반영한다.
    /// </summary>
    private void RebuildCellStates()
    {
        Dictionary<GridPosition, GridActor> previousActorByPosition = new();
        foreach (GridCellState cellState in cellByPosition.Values)
        {
            if (cellState.OccupiedActor != null)
            {
                previousActorByPosition[cellState.Position] = cellState.OccupiedActor;
            }
        }

        cellByPosition.Clear();

        int safeWidth = Mathf.Max(0, width);
        int safeHeight = Mathf.Max(0, height);

        for (int x = 0; x < safeWidth; x++)
        {
            for (int y = 0; y < safeHeight; y++)
            {
                GridPosition position = new(x, y);
                GridCellState cellState = new(position);

                if (HasTileAt(wallLogicTilemap, position) ||
                    HasTileAt(lowObstacleLogicTilemap, position) ||
                    HasTileAt(boundaryLogicTilemap, position))
                {
                    cellState.SetBlocked(true);
                }

                if (previousActorByPosition.TryGetValue(position, out GridActor actor) && actor != null)
                {
                    cellState.SetOccupiedActor(actor);
                }

                cellByPosition[position] = cellState;
            }
        }
    }

    /// <summary>
    /// 지정한 논리 그리드 칸의 월드 위치와 겹치는 논리 타일맵 셀에 타일이 있는지 확인한다.
    /// </summary>
    private bool HasTileAt(Tilemap tilemap, GridPosition position)
    {
        if (tilemap == null)
        {
            return false;
        }

        Vector3Int tilePosition = tilemap.WorldToCell(GridToWorld(position));
        return tilemap.HasTile(tilePosition);
    }

    /// <summary>
    /// 이동과 시야를 차단하는 필수 벽 논리 타일맵 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (wallLogicTilemap == null)
        {
            Debug.LogError($"{nameof(GridManager)} on {name}에는 이동과 시야를 차단할 벽 {nameof(Tilemap)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 그리드 보드 크기와 셀 크기가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (width <= 0 || height <= 0)
        {
            Debug.LogError($"{nameof(GridManager)} on {name}의 보드 가로와 세로 칸 수는 0보다 커야 합니다.", this);
            return false;
        }

        if (cellSize <= 0f)
        {
            Debug.LogError($"{nameof(GridManager)} on {name}의 셀 크기는 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
