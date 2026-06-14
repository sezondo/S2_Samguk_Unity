using UnityEngine;

/// <summary>
/// 적 루트에 붙은 핵심 컴포넌트와 데이터를 모아 제공하는 참조 주머니다.
/// 정책 계산이나 상태 변경은 하지 않고, 다른 적 컴포넌트가 필요한 참조를 꺼내 쓰게 한다.
/// </summary>
public class EnemyContext : MonoBehaviour
{
    [Header("Data")]
    // 적 시야와 행동에 사용하는 튜닝 데이터다.
    [SerializeField] private EnemyData enemyData;

    [Header("Core Components")]
    // 적이 보드에서 차지하는 칸과 실제 격자 이동을 관리하는 공용 말 컴포넌트다.
    [SerializeField] private GridActor gridActor;
    // 적의 그리드 시야 칸 계산을 담당하는 컴포넌트다.
    [SerializeField] private EnemyGridSight gridSight;
    // 적의 현재 경계 상태를 보관하고 전환 요청을 처리하는 컴포넌트다.
    [SerializeField] private EnemyAlertState alertState;
    // 적 경계 상태를 화면에 표시하는 시각 피드백 컴포넌트다.
    [SerializeField] private EnemyAlertVisual alertVisual;

    // 현재 EnemyRegistry에 등록되어 있는지 나타낸다.
    private bool registeredToRegistry;

    public EnemyData EnemyData => enemyData;
    public GridActor GridActor => gridActor;
    public EnemyGridSight GridSight => gridSight;
    public EnemyAlertState AlertState => alertState;
    public EnemyAlertVisual AlertVisual => alertVisual;

    /// <summary>
    /// 적 Context에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// Context가 활성화될 때 적 등록소에 자기 자신을 등록한다.
    /// </summary>
    private void OnEnable()
    {
        TryRegisterToRegistry(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 등록소를 못 잡은 경우 시작 시점에 한 번 더 등록한다.
    /// </summary>
    private void Start()
    {
        TryRegisterToRegistry(true);
    }

    /// <summary>
    /// Context가 비활성화될 때 적 등록소에서 자기 자신을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (!registeredToRegistry || EnemyRegistry.Instance == null)
        {
            registeredToRegistry = false;
            return;
        }

        EnemyRegistry.Instance.UnregisterEnemy(this);
        registeredToRegistry = false;
    }

    /// <summary>
    /// 현재 씬의 EnemyRegistry에 이 적 Context를 등록한다.
    /// </summary>
    private void TryRegisterToRegistry(bool logMissingRegistry)
    {
        if (registeredToRegistry)
        {
            return;
        }

        EnemyRegistry registry = EnemyRegistry.Instance;
        if (registry == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(EnemyContext)} on {name}에는 씬의 {nameof(EnemyRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        registry.RegisterEnemy(this);
        registeredToRegistry = true;
    }

    /// <summary>
    /// 적 Context에 필수 참조가 모두 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (enemyData == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(EnemyData)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridActor == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridSight == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(EnemyGridSight)} 참조가 필요합니다.", this);
            return false;
        }

        if (alertState == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(EnemyAlertState)} 참조가 필요합니다.", this);
            return false;
        }

        if (alertVisual == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(EnemyAlertVisual)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
