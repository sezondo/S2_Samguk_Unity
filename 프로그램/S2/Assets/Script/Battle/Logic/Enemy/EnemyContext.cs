using UnityEngine;

/// <summary>
/// 적 루트에 붙은 핵심 컴포넌트와 데이터를 모아 제공하는 참조 주머니다.
/// 정책 계산이나 상태 변경은 하지 않고, 다른 적 컴포넌트가 필요한 참조를 꺼내 쓰게 한다.
/// </summary>
public class EnemyContext : MonoBehaviour, ITacticalUnit
{
    [Header("Unit")]
    // 적 Context가 전술 유닛 등록소에 제공할 진영이다.
    [SerializeField] private UnitFaction faction = UnitFaction.Enemy;

    [Header("Data")]
    // 적 시야와 행동에 사용하는 튜닝 데이터다.
    [SerializeField] private EnemyData enemyData;

    [Header("Core Components")]
    // 적이 보드에서 차지하는 칸과 실제 격자 이동을 관리하는 공용 말 컴포넌트다.
    [SerializeField] private GridActor gridActor;
    // 적의 HP와 전투불능 상태를 관리하는 컴포넌트다.
    [SerializeField] private ActorHealth health;
    // 적의 그리드 시야 칸 계산을 담당하는 컴포넌트다.
    [SerializeField] private EnemyGridSight gridSight;
    // 적의 현재 경계 상태를 보관하고 전환 요청을 처리하는 컴포넌트다.
    [SerializeField] private EnemyAlertState alertState;
    // 적 턴 행동에서 소비하는 AP 컴포넌트다. 적 턴 AI를 쓰는 씬에서 연결한다.
    [SerializeField] private EnemyActionPoint actionPoint;
    // 적 턴에 자기 행동을 결정하고 실행하는 선택 컴포넌트다. 적 턴 AI를 쓰는 씬에서 연결한다.
    [SerializeField] private EnemyTurnAgent turnAgent;
    // 적 근접·원거리 공격 판정과 피해 요청을 담당한다. 적 턴 AI를 쓰는 씬에서 연결한다.
    [SerializeField] private EnemyAttackAction attackAction;
    // 평상 상태의 경비·순찰 행동과 중단된 순찰 진행을 관리하는 선택 컴포넌트다.
    [SerializeField] private EnemyRoutineController routineController;
    // 의심 상태의 즉시 반응, 조사와 평상 복귀를 실행하는 선택 컴포넌트다.
    [SerializeField] private EnemyInvestigationAgent investigationAgent;
    // 현재 EnemyRegistry에 등록되어 있는지 나타낸다.
    private bool registeredToRegistry;
    // 현재 TacticalUnitRegistry에 등록되어 있는지 나타낸다.
    private bool registeredToTacticalRegistry;

    public UnitFaction Faction => faction;
    public EnemyData EnemyData => enemyData;
    public GridActor GridActor => gridActor;
    public ActorHealth Health => health;
    public bool IsAlive => health != null && !health.IsDead;
    public EnemyGridSight GridSight => gridSight;
    public EnemyAlertState AlertState => alertState;
    public EnemyActionPoint ActionPoint => actionPoint;
    public EnemyTurnAgent TurnAgent => turnAgent;
    public EnemyAttackAction AttackAction => attackAction;
    public EnemyRoutineController RoutineController => routineController;
    public EnemyInvestigationAgent InvestigationAgent => investigationAgent;

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
        }
        else
        {
            EnemyRegistry.Instance.UnregisterEnemy(this);
            registeredToRegistry = false;
        }

        if (registeredToTacticalRegistry && TacticalUnitRegistry.Instance != null)
        {
            TacticalUnitRegistry.Instance.Unregister(this);
        }

        registeredToTacticalRegistry = false;
    }

    /// <summary>
    /// 현재 씬의 EnemyRegistry에 이 적 Context를 등록한다.
    /// </summary>
    private void TryRegisterToRegistry(bool logMissingRegistry)
    {
        if (registeredToRegistry && registeredToTacticalRegistry)
        {
            return;
        }

        if (!registeredToRegistry)
        {
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

        TacticalUnitRegistry tacticalRegistry = TacticalUnitRegistry.Instance;
        if (tacticalRegistry == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(EnemyContext)} on {name}에는 씬의 {nameof(TacticalUnitRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        tacticalRegistry.Register(this);
        registeredToTacticalRegistry = true;
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

        if (health == null)
        {
            Debug.LogError($"{nameof(EnemyContext)} on {name}에는 {nameof(ActorHealth)} 참조가 필요합니다.", this);
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

        return true;
    }
}
