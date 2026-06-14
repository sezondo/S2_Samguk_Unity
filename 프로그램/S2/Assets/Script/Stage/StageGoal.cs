using UnityEngine;

/// <summary>
/// 스테이지 클리어 목표 칸을 나타낸다.
/// 판정 자체는 StageGoalManager가 처리하고, 이 컴포넌트는 목표 좌표와 표시 기준만 제공한다.
/// </summary>
public class StageGoal : MonoBehaviour
{
    [Header("Goal")]
    // 플레이어가 도착해야 하는 목표 그리드 칸 좌표다.
    [SerializeField] private GridPosition goalPosition = GridPosition.Zero;

    [Header("Gizmos")]
    // Scene 뷰에서 목표 칸을 표시할지 정한다.
    [SerializeField] private bool drawGizmos = true;
    // 목표 칸 표시 색이다.
    [SerializeField] private Color gizmoColor = new(0.2f, 1f, 0.45f, 0.65f);
    // 목표 칸 표시가 그리드 한 칸에서 차지할 비율이다.
    [SerializeField] private float gizmoCellScaleRatio = 0.7f;

    public GridPosition GoalPosition => goalPosition;

    /// <summary>
    /// 지정한 칸이 이 목표 칸인지 확인한다.
    /// </summary>
    public bool IsGoalPosition(GridPosition position)
    {
        return goalPosition == position;
    }

    /// <summary>
    /// Scene 뷰에서 목표 칸 위치를 표시한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        GridManager gridManager = GridManager.Instance;
        Vector3 worldPosition = gridManager != null ? gridManager.GridToWorld(goalPosition) : transform.position;
        float cellSize = gridManager != null ? gridManager.CellSize : 1f;
        float safeScale = Mathf.Max(0.01f, cellSize * gizmoCellScaleRatio);

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(worldPosition, new Vector3(safeScale, safeScale, 0.05f));
    }
}
