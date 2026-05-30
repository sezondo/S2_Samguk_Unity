using UnityEngine;

/// <summary>
/// 보드 위에 올라가는 말의 최소 단위다.
/// 플레이어, 적, 장치처럼 칸을 차지하는 대상은 이 컴포넌트로 격자 위치를 가진다.
/// </summary>
public class GridActor : MonoBehaviour
{
    [Header("Grid")]
    // 이 말이 현재 서 있는 보드 칸 좌표다.
    [SerializeField] private GridPosition gridPosition = GridPosition.Zero;
    // true면 이 말이 현재 칸을 점유해서 다른 말의 진입을 막는다.
    [SerializeField] private bool occupyCell = true;
    // 활성화될 때 Transform 위치를 gridPosition 기준 월드 위치로 맞출지 정한다.
    [SerializeField] private bool snapToGridOnEnable = true;
    // 시작 칸 등록에 실패했을 때 오브젝트를 비활성화할지 정한다.
    [SerializeField] private bool deactivateWhenRegisterFailed = true;

    // 이 말이 등록된 그리드 매니저 참조다.
    private GridManager gridManager;
    // 현재 GridManager의 점유 테이블에 등록되어 있는지 나타낸다.
    private bool registeredOnGrid;

    // 외부에서 읽는 현재 보드 칸 좌표다.
    public GridPosition GridPosition => gridPosition;
    // 외부에서 이 말이 칸을 점유하는 타입인지 확인할 때 사용한다.
    public bool OccupyCell => occupyCell;
    // 외부에서 이 말이 점유 테이블에 등록되었는지 확인할 때 사용한다.
    public bool IsRegisteredOnGrid => registeredOnGrid;

    /// <summary>
    /// 오브젝트가 활성화될 때 그리드 등록과 위치 보정을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TryInitializeOnGrid();
    }

    /// <summary>
    /// Awake 순서 문제로 실패한 그리드 등록을 시작 시점에 한 번 더 보정한다.
    /// </summary>
    private void Start()
    {
        // 다른 오브젝트의 Awake 순서 때문에 OnEnable에서 GridManager를 못 잡은 경우를 한 번 더 보정한다.
        TryInitializeOnGrid();
    }

    /// <summary>
    /// 오브젝트가 비활성화될 때 그리드 점유 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (gridManager != null && occupyCell && registeredOnGrid)
        {
            gridManager.UnregisterActor(this, gridPosition);
        }

        registeredOnGrid = false;
    }

    /// <summary>
    /// 현재 gridPosition에 맞춰 Transform 위치를 보드 월드 좌표로 이동시킨다.
    /// </summary>
    public void SnapToGrid()
    {
        if (!EnsureGridManager())
        {
            return;
        }

        transform.position = gridManager.GridToWorld(gridPosition);
    }

    /// <summary>
    /// 지정한 목표 칸으로 이동을 요청하고 성공하면 위치를 스냅한다.
    /// </summary>
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

    /// <summary>
    /// 현재 칸에서 지정한 오프셋만큼 떨어진 칸으로 이동을 요청한다.
    /// </summary>
    public bool TryMoveBy(GridPosition offset)
    {
        return TryMoveTo(gridPosition + offset);
    }

    /// <summary>
    /// GridManager 참조 확보, 점유 등록, 위치 스냅을 순서대로 시도한다.
    /// </summary>
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

    /// <summary>
    /// 이 말이 사용할 GridManager 참조를 확보한다.
    /// </summary>
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

        Debug.LogWarning($"{nameof(GridActor)}: {name} 오브젝트가 사용할 {nameof(GridManager)}를 씬에서 찾지 못했습니다.", this);
        return false;
    }

    /// <summary>
    /// 현재 gridPosition 기준으로 GridManager 점유 테이블에 등록한다.
    /// </summary>
    private bool TryRegisterOnGrid()
    {
        registeredOnGrid = gridManager.RegisterActor(this, gridPosition);
        if (registeredOnGrid)
        {
            return true;
        }

        Debug.LogError($"{nameof(GridActor)}: {name} 오브젝트를 {gridPosition} 칸에 등록하지 못했습니다. 시작 칸 중복 또는 보드 범위를 확인하세요.", this);

        if (deactivateWhenRegisterFailed)
        {
            gameObject.SetActive(false);
        }

        return false;
    }
}
