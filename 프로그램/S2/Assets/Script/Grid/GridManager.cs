using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자 보드의 크기, 좌표 변환, 칸 점유 상태를 관리한다.
/// 턴제 규칙은 Transform 위치 대신 GridPosition을 기준으로 판단한다.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector3 originWorldPosition;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gridColor = new(0.25f, 0.75f, 1f, 0.35f);
    [SerializeField] private Color occupiedColor = new(1f, 0.35f, 0.25f, 0.45f);

    private readonly Dictionary<GridPosition, GridActor> actorByPosition = new();

    public static GridManager Instance { get; private set; }

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(GridManager)} instance already exists. Disabling duplicate on {name}.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsInside(GridPosition position)
    {
        return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
    }

    public Vector3 GridToWorld(GridPosition position)
    {
        return originWorldPosition + new Vector3(position.x * cellSize, position.y * cellSize, 0f);
    }

    public GridPosition WorldToGrid(Vector3 worldPosition)
    {
        Vector3 localPosition = worldPosition - originWorldPosition;
        int x = Mathf.RoundToInt(localPosition.x / cellSize);
        int y = Mathf.RoundToInt(localPosition.y / cellSize);
        return new GridPosition(x, y);
    }

    public bool CanEnter(GridPosition position)
    {
        return IsInside(position) && !IsOccupied(position);
    }

    public bool IsOccupied(GridPosition position)
    {
        return actorByPosition.TryGetValue(position, out GridActor actor) && actor != null;
    }

    public bool TryGetActorAt(GridPosition position, out GridActor actor)
    {
        return actorByPosition.TryGetValue(position, out actor) && actor != null;
    }

    public bool RegisterActor(GridActor actor, GridPosition position)
    {
        if (actor == null || !IsInside(position))
        {
            return false;
        }

        if (TryGetActorAt(position, out GridActor existingActor) && existingActor != actor)
        {
            Debug.LogWarning($"Grid cell {position} is already occupied by {existingActor.name}.", this);
            return false;
        }

        actorByPosition[position] = actor;
        return true;
    }

    public void UnregisterActor(GridActor actor, GridPosition position)
    {
        if (actor == null)
        {
            return;
        }

        if (TryGetActorAt(position, out GridActor registeredActor) && registeredActor == actor)
        {
            actorByPosition.Remove(position);
        }
    }

    public bool TryMoveActor(GridActor actor, GridPosition from, GridPosition to)
    {
        if (actor == null || !CanEnter(to))
        {
            return false;
        }

        UnregisterActor(actor, from);
        actorByPosition[to] = actor;
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
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

        Gizmos.color = occupiedColor;
        foreach (GridPosition position in actorByPosition.Keys)
        {
            Gizmos.DrawCube(GridToWorld(position), Vector3.one * safeCellSize * 0.75f);
        }
    }
}
