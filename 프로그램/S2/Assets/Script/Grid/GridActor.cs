using UnityEngine;

/// <summary>
/// 보드 위에 올라가는 말의 최소 단위다.
/// 플레이어, 적, 장치처럼 칸을 차지하는 대상은 이 컴포넌트로 격자 위치를 가진다.
/// </summary>
public class GridActor : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private GridPosition gridPosition = GridPosition.Zero;
    [SerializeField] private bool occupyCell = true;
    [SerializeField] private bool snapToGridOnEnable = true;
    [SerializeField] private bool deactivateWhenRegisterFailed = true;

    private GridManager gridManager;
    private bool registeredOnGrid;

    public GridPosition GridPosition => gridPosition;
    public bool OccupyCell => occupyCell;
    public bool IsRegisteredOnGrid => registeredOnGrid;

    private void OnEnable()
    {
        TryInitializeOnGrid();
    }

    private void Start()
    {
        // 다른 오브젝트의 Awake 순서 때문에 OnEnable에서 GridManager를 못 잡은 경우를 한 번 더 보정한다.
        TryInitializeOnGrid();
    }

    private void OnDisable()
    {
        if (gridManager != null && occupyCell && registeredOnGrid)
        {
            gridManager.UnregisterActor(this, gridPosition);
        }

        registeredOnGrid = false;
    }

    public void SnapToGrid()
    {
        if (!EnsureGridManager())
        {
            return;
        }

        transform.position = gridManager.GridToWorld(gridPosition);
    }

    public bool TryMoveTo(GridPosition targetPosition)
    {
        if (!EnsureGridManager())
        {
            return false;
        }

        if (occupyCell && !registeredOnGrid && !TryRegisterOnGrid())
        {
            return false;
        }

        if (occupyCell && !gridManager.TryMoveActor(this, gridPosition, targetPosition))
        {
            return false;
        }

        gridPosition = targetPosition;
        registeredOnGrid = occupyCell || registeredOnGrid;
        SnapToGrid();
        return true;
    }

    public bool TryMoveBy(GridPosition offset)
    {
        return TryMoveTo(gridPosition + offset);
    }

    private bool TryInitializeOnGrid()
    {
        if (!EnsureGridManager())
        {
            return false;
        }

        if (occupyCell && !registeredOnGrid && !TryRegisterOnGrid())
        {
            return false;
        }

        if (snapToGridOnEnable)
        {
            SnapToGrid();
        }

        return true;
    }

    private bool EnsureGridManager()
    {
        if (gridManager != null)
        {
            return true;
        }

        gridManager = GridManager.Instance;
        if (gridManager != null)
        {
            return true;
        }

        Debug.LogWarning($"{nameof(GridActor)} on {name} requires a {nameof(GridManager)} in the scene.", this);
        return false;
    }

    private bool TryRegisterOnGrid()
    {
        registeredOnGrid = gridManager.RegisterActor(this, gridPosition);
        if (registeredOnGrid)
        {
            return true;
        }

        Debug.LogError($"{nameof(GridActor)} on {name} failed to register at {gridPosition}. Check duplicated start cells or board bounds.", this);

        if (deactivateWhenRegisterFailed)
        {
            gameObject.SetActive(false);
        }

        return false;
    }
}
