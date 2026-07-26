using UnityEngine;

/// <summary>
/// 그리드 위에서 해킹 가능한 대상의 기본 런타임 컴포넌트다.
/// 문, 장치, 감시장치 같은 해킹 대상은 이 컴포넌트를 기준으로 1차 해킹 루프에 연결한다.
/// </summary>
public class HackableObject : MonoBehaviour, IHackable
{
    [Header("Data")]
    // 이 대상의 해킹 시간 같은 튜닝 데이터다.
    [SerializeField] private HackableData hackData;

    [Header("Reference")]
    // 해킹 대상이 차지하거나 대표하는 그리드 위치를 제공하는 말 컴포넌트다.
    [SerializeField] private GridActor gridActor;

    [Header("State")]
    // 이미 해킹 완료된 대상인지 나타낸다.
    [SerializeField] private bool isHacked;

    [Header("Log")]
    // true면 해킹 생명주기 로그를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logHackState = true;

    public HackableData HackData => hackData;
    public GridActor GridActor => gridActor;
    public GridPosition GridPosition => gridActor.GridPosition;
    public bool IsHacked => isHacked;

    /// <summary>
    /// 컴포넌트가 활성화될 때 해킹 대상 등록소에 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        TryRegister(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 등록을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegister(true);
    }

    /// <summary>
    /// 컴포넌트가 비활성화될 때 해킹 대상 등록소에서 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (HackableRegistry.Instance != null)
        {
            HackableRegistry.Instance.Unregister(this);
        }
    }

    /// <summary>
    /// 해킹 대상 등록소에 이 대상을 등록한다.
    /// </summary>
    private void TryRegister(bool logMissingRegistry)
    {
        if (HackableRegistry.Instance == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(HackableObject)} on {name}에는 씬의 {nameof(HackableRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        HackableRegistry.Instance.Register(this);
    }

    /// <summary>
    /// 해킹 대상이 선택 가능 상태가 됐을 때 호출된다.
    /// </summary>
    public void OnHackReady()
    {
        if (logHackState)
        {
            Debug.Log($"{nameof(HackableObject)}: {name} 대상이 해킹 준비 상태가 됐습니다.", this);
        }
    }

    /// <summary>
    /// 해킹 행동이 시작됐을 때 호출된다.
    /// </summary>
    public void OnHackStarted()
    {
        if (logHackState)
        {
            Debug.Log($"{nameof(HackableObject)}: {name} 대상 해킹을 시작합니다.", this);
        }
    }

    /// <summary>
    /// 해킹 행동이 완료됐을 때 호출된다.
    /// </summary>
    public void OnHackCompleted()
    {
        isHacked = true;

        if (logHackState)
        {
            Debug.Log($"{nameof(HackableObject)}: {name} 대상 해킹이 완료됐습니다.", this);
        }
    }

    /// <summary>
    /// 해킹 선택이나 진행이 취소됐을 때 호출된다.
    /// </summary>
    public void OnHackCanceled()
    {
        if (logHackState)
        {
            Debug.Log($"{nameof(HackableObject)}: {name} 대상 해킹이 취소됐습니다.", this);
        }
    }

    /// <summary>
    /// 해킹 대상에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (hackData == null)
        {
            Debug.LogError($"{nameof(HackableObject)} on {name}에는 {nameof(HackableData)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridActor == null)
        {
            Debug.LogError($"{nameof(HackableObject)} on {name}에는 대상 위치를 제공할 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 해킹 대상 데이터가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (hackData == null)
        {
            Debug.LogError($"{nameof(HackableObject)} on {name}에는 {nameof(HackableData)} 참조가 필요합니다.", this);
            return false;
        }

        if (hackData.HackDuration < 0f)
        {
            Debug.LogError($"{nameof(HackableObject)} on {name}의 해킹 시간은 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}
