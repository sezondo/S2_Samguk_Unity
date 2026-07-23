using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 스테이지 클리어 목표 칸을 나타낸다.
/// 판정 자체는 StageGoalManager가 처리하고, 이 컴포넌트는 목표 좌표와 표시 기준만 제공한다.
/// </summary>
public class StageGoal : MonoBehaviour
{
    [Header("Reference")]
    // 목표 그리드 좌표를 월드 위치로 변환할 씬의 그리드 매니저다.
    [SerializeField] private GridManager gridManager;

    [Header("Goal")]
    // 플레이어가 도착해야 하는 목표 그리드 칸 좌표다.
    [SerializeField] private GridPosition goalPosition = GridPosition.Zero;

    [Header("Scene Debug Gizmo")]
    // true면 플레이하지 않는 Scene 뷰에 목표 칸 디버그 표시를 그린다.
    [FormerlySerializedAs("drawGizmos")]
    [SerializeField] private bool drawSceneDebugGizmo = true;
    // 편집 모드 목표 칸 디버그 표시 색이다.
    [FormerlySerializedAs("gizmoColor")]
    [SerializeField] private Color sceneDebugGizmoColor = new(0.2f, 1f, 0.45f, 0.65f);
    // 편집 모드 디버그 표시가 그리드 한 칸에서 차지할 비율이다.
    [FormerlySerializedAs("gizmoCellScaleRatio")]
    [SerializeField] private float sceneDebugGizmoCellScaleRatio = 0.7f;

    [Header("Play Mode Goal Gizmo")]
    // true면 플레이 중 Game 뷰의 Gizmos가 켜졌을 때 임시 목표 위치를 표시한다.
    [SerializeField] private bool drawPlayModeGoalGizmo = true;
    // 프리팹 비주얼을 연결하기 전까지 사용할 플레이 모드 임시 목표 표시 색이다.
    [SerializeField] private Color playModeGoalGizmoColor = new(0.2f, 1f, 0.45f, 0.9f);
    // 플레이 모드 임시 목표 표시가 그리드 한 칸에서 차지할 비율이다.
    [SerializeField] private float playModeGoalGizmoCellScaleRatio = 0.55f;
    // 목표 표시가 타일과 겹칠 때 앞쪽에 보이도록 적용할 월드 Z 오프셋이다.
    [SerializeField] private float gizmoZOffset = -0.1f;

    public GridPosition GoalPosition => goalPosition;

    /// <summary>
    /// 목표 판정과 표시 설정에 필요한 참조와 데이터가 유효한지 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 지정한 칸이 이 목표 칸인지 확인한다.
    /// </summary>
    public bool IsGoalPosition(GridPosition position)
    {
        return goalPosition == position;
    }

    /// <summary>
    /// 편집 모드와 플레이 모드 설정에 맞춰 목표 칸 위치를 Gizmos로 표시한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (gridManager == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            if (!drawPlayModeGoalGizmo || playModeGoalGizmoCellScaleRatio <= 0f)
            {
                return;
            }

            DrawGoalGizmo(playModeGoalGizmoColor, playModeGoalGizmoCellScaleRatio);
            return;
        }

        if (!drawSceneDebugGizmo || sceneDebugGizmoCellScaleRatio <= 0f)
        {
            return;
        }

        DrawGoalGizmo(sceneDebugGizmoColor, sceneDebugGizmoCellScaleRatio);
    }

    /// <summary>
    /// 목표 그리드 좌표를 월드 위치로 변환해 지정한 색과 크기의 사각형을 그린다.
    /// </summary>
    private void DrawGoalGizmo(Color color, float cellScaleRatio)
    {
        Vector3 worldPosition = gridManager.GridToWorld(goalPosition);
        worldPosition.z += gizmoZOffset;
        float safeScale = Mathf.Max(0.01f, gridManager.CellSize * cellScaleRatio);

        Gizmos.color = color;
        Gizmos.DrawCube(worldPosition, new Vector3(safeScale, safeScale, 0.05f));
    }

    /// <summary>
    /// 목표 좌표를 월드 위치로 변환할 필수 GridManager 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(StageGoal)} on {name}에는 목표 위치를 변환할 {nameof(GridManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 목표 좌표와 편집·플레이 모드 표시 크기가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (gridManager != null && !gridManager.IsInside(goalPosition))
        {
            Debug.LogError($"{nameof(StageGoal)} on {name}의 목표 좌표 {goalPosition}은 그리드 범위 밖입니다.", this);
            return false;
        }

        if (drawSceneDebugGizmo && sceneDebugGizmoCellScaleRatio <= 0f)
        {
            Debug.LogError($"{nameof(StageGoal)} on {name}의 Scene 디버그 표시 크기 비율은 0보다 커야 합니다.", this);
            return false;
        }

        if (drawPlayModeGoalGizmo && playModeGoalGizmoCellScaleRatio <= 0f)
        {
            Debug.LogError($"{nameof(StageGoal)} on {name}의 플레이 모드 목표 표시 크기 비율은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
