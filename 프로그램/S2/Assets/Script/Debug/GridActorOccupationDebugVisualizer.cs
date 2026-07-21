using UnityEngine;

/// <summary>
/// 씬의 활성 GridActor 중 칸을 점유하는 액터의 직렬화 좌표를 Scene 뷰에 표시한다.
/// 플레이하지 않은 상태에서도 문과 장치 같은 논리 점유자의 위치를 조정할 수 있게 돕는 디버그 전용 컴포넌트다.
/// </summary>
public class GridActorOccupationDebugVisualizer : MonoBehaviour
{
    [Header("Reference")]
    // GridActor의 논리 좌표를 월드 위치로 변환할 씬의 그리드 매니저다.
    [SerializeField] private GridManager gridManager;

    [Header("Display")]
    // true면 플레이 모드에서도 점유 칸 디버그 표시를 유지한다.
    [SerializeField] private bool drawInPlayMode;
    // 점유 칸에 표시할 반투명 회색이다.
    [SerializeField] private Color occupiedColor = new(0.35f, 0.35f, 0.35f, 0.65f);
    // 회색 표시가 그리드 한 칸에서 차지할 비율이다.
    [SerializeField] private float cellScaleRatio = 0.8f;
    // 다른 표시와 겹칠 때 앞뒤 위치를 조정할 월드 Z 오프셋이다.
    [SerializeField] private float zOffset = -0.1f;

    /// <summary>
    /// 플레이 모드에서 필수 참조와 표시 데이터가 유효한지 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 활성 GridActor가 직렬화한 점유 칸을 Scene 뷰에 회색 사각형으로 표시한다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if ((Application.isPlaying && !drawInPlayMode) ||
            gridManager == null ||
            cellScaleRatio <= 0f)
        {
            return;
        }

        GridActor[] actors = FindObjectsByType<GridActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        float cellScale = Mathf.Max(0.01f, gridManager.CellSize * cellScaleRatio);
        Gizmos.color = occupiedColor;

        for (int i = 0; i < actors.Length; i++)
        {
            GridActor actor = actors[i];
            if (actor == null || !actor.OccupyCell)
            {
                continue;
            }

            Vector3 worldPosition = gridManager.GridToWorld(actor.GridPosition);
            worldPosition.z += zOffset;
            Gizmos.DrawCube(worldPosition, new Vector3(cellScale, cellScale, 0.05f));
        }
    }

    /// <summary>
    /// 점유 칸 좌표를 월드 위치로 변환할 필수 GridManager 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(GridActorOccupationDebugVisualizer)} on {name}에는 {nameof(GridManager)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 점유 칸 표시 크기가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (cellScaleRatio <= 0f)
        {
            Debug.LogError($"{nameof(GridActorOccupationDebugVisualizer)} on {name}의 셀 표시 비율은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
