using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자 보드의 크기, 좌표 변환, 칸 점유 상태를 관리한다.
/// 턴제 규칙은 Transform 위치 대신 GridPosition을 기준으로 판단한다.
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

    [Header("Blocked Cells")]
    // 벽, 장애물, 낭떠러지처럼 어떤 말도 들어갈 수 없는 고정 이동불가 칸 목록이다.
    [SerializeField] private List<GridPosition> blockedPositions = new();

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

    // 씬에서 사용하는 단일 그리드 매니저 인스턴스다.
    public static GridManager Instance { get; private set; }

    // 외부에서 읽는 보드 가로 칸 수다.
    public int Width => width;
    // 외부에서 읽는 보드 세로 칸 수다.
    public int Height => height;
    // 외부에서 읽는 한 칸의 월드 크기다.
    public float CellSize => cellSize;

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

        Instance = this;
        RebuildCellStates();
    }

    /// <summary>
    /// 인스펙터에서 보드 크기나 이동불가 칸 목록이 바뀌면 런타임 칸 상태를 갱신한다.
    /// </summary>
    private void OnValidate()
    {
        RebuildCellStates();
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
    /// 지정한 칸을 런타임에 이동불가 칸으로 설정하거나 해제한다.
    /// </summary>
    public void SetBlocked(GridPosition position, bool blocked)
    {
        if (!IsInside(position))
        {
            Debug.LogWarning($"{nameof(GridManager)}: {position} 칸은 보드 범위 밖이라 이동불가 설정을 바꿀 수 없습니다.", this);
            return;
        }

        if (!TryGetCellState(position, out GridCellState cellState))
        {
            Debug.LogWarning($"{nameof(GridManager)}: {position} 칸 상태를 찾지 못해 이동불가 설정을 바꿀 수 없습니다.", this);
            return;
        }

        if (blocked)
        {
            if (!cellState.IsBlocked)
            {
                cellState.SetBlocked(true);
                blockedPositions.Add(position);
            }

            return;
        }

        if (cellState.IsBlocked)
        {
            cellState.SetBlocked(false);
            blockedPositions.RemoveAll(blockedPosition => blockedPosition == position);
        }
    }

    /// <summary>
    /// 지정한 칸이 다른 GridActor에게 점유되어 있는지 확인한다.
    /// </summary>
    public bool IsOccupied(GridPosition position)
    {
        return TryGetCellState(position, out GridCellState cellState) && cellState.IsOccupied;
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
        RebuildCellStates();

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
    /// 보드 범위 안의 모든 칸 상태를 다시 만들고 인스펙터 이동불가 칸 목록을 반영한다.
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

                if (previousActorByPosition.TryGetValue(position, out GridActor actor) && actor != null)
                {
                    cellState.SetOccupiedActor(actor);
                }

                cellByPosition[position] = cellState;
            }
        }

        for (int i = 0; i < blockedPositions.Count; i++)
        {
            if (cellByPosition.TryGetValue(blockedPositions[i], out GridCellState cellState))
            {
                cellState.SetBlocked(true);
            }
        }
    }
}
