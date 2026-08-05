using UnityEngine;

/// <summary>
/// 도깨비 환도의 현재 기준 칸과 회수 상태를 보관한다.
/// 검은 보드 점유 액터가 아니므로 GridManager에 등록하지 않고 위치 값만 관리한다.
/// </summary>
public class PlayerSwordState : MonoBehaviour
{
    [Header("Reference")]
    // 플레이어 위치와 행동 데이터를 제공하는 Context다.
    [SerializeField] private TacticalUnitContext playerContext;

    [Header("Initial State")]
    // true면 시작 시 검 기준 칸을 플레이어 현재 칸으로 맞춘다.
    [SerializeField] private bool initializeAtPlayerPosition = true;
    // initializeAtPlayerPosition이 false일 때 사용할 초기 검 기준 칸이다.
    [SerializeField] private GridPosition initialSwordPosition;

    [Header("Log")]
    // true면 해킹/투척/회수로 검 위치가 바뀔 때 로그를 출력한다.
    [SerializeField] private bool logPositionChanged = true;

    // 현재 검 투척과 해킹 사거리 기준으로 사용하는 칸이다.
    [SerializeField] private GridPosition currentPosition;
    // true면 검이 플레이어에게 회수된 상태다.
    [SerializeField] private bool isRecalled = true;

    public GridPosition CurrentPosition => isRecalled ? playerContext.GridActor.GridPosition : currentPosition;
    public bool IsRecalled => isRecalled;

    /// <summary>
    /// 검 위치 상태에 필요한 참조를 확인하고 초기 위치를 정한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        currentPosition = initializeAtPlayerPosition ? playerContext.GridActor.GridPosition : initialSwordPosition;
        isRecalled = currentPosition == playerContext.GridActor.GridPosition;
    }

    /// <summary>
    /// 검을 지정한 칸에 배치된 상태로 기록한다.
    /// </summary>
    public void SetDeployedPosition(GridPosition position)
    {
        currentPosition = position;
        isRecalled = false;

        if (logPositionChanged)
        {
            Debug.Log($"{nameof(PlayerSwordState)}: 검 기준 칸을 {currentPosition} 위치로 갱신했습니다.", this);
        }
    }

    /// <summary>
    /// 검을 플레이어 현재 칸으로 회수된 상태로 기록한다.
    /// </summary>
    public void RecallToPlayer()
    {
        currentPosition = playerContext.GridActor.GridPosition;
        isRecalled = true;

        if (logPositionChanged)
        {
            Debug.Log($"{nameof(PlayerSwordState)}: 검을 플레이어 위치 {currentPosition} 칸으로 회수했습니다.", this);
        }
    }

    /// <summary>
    /// 검 위치 상태에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerSwordState)} on {name}에는 {nameof(TacticalUnitContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.GridActor == null)
        {
            Debug.LogError($"{nameof(PlayerSwordState)} on {name}에는 {nameof(TacticalUnitContext)}에 연결된 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
