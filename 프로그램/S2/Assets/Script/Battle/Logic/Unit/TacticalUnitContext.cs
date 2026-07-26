using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어가 조작할 수 있는 전술 유닛 루트의 데이터와 기능 컴포넌트 참조를 제공한다.
/// 데이터에 선언된 능력 조합과 실제 컴포넌트 구성이 일치하는지도 검사한다.
/// </summary>
public class TacticalUnitContext : MonoBehaviour, ITacticalUnit
{
    [Header("Unit")]
    // 이 전술 유닛이 소속된 진영이다.
    [SerializeField] private UnitFaction faction = UnitFaction.Player;
    // 이 전술 유닛의 행동 결정을 담당하는 주체다.
    [SerializeField] private UnitControlType controlType = UnitControlType.Player;

    [Header("Data")]
    // 전술 유닛의 필수 능력 구성과 AP, 이동, 공격 수치를 보관하는 데이터다.
    [FormerlySerializedAs("turnData")]
    [SerializeField] private ControllableUnitData unitData;

    [Header("Core Components")]
    // 전술 유닛이 보드에서 차지하는 칸과 실제 격자 이동을 관리하는 공용 말 컴포넌트다.
    [SerializeField] private GridActor gridActor;
    // 전술 유닛의 HP와 전투불능 상태를 관리하는 컴포넌트다.
    [SerializeField] private ActorHealth health;
    // 전술 유닛 행동에서 소비하는 AP 컴포넌트다.
    [SerializeField] private ActionPoint actionPoint;
    // 플레이어의 마우스 기반 그리드 이동 행동 컴포넌트다.
    [SerializeField] private PlayerGridMoveAction gridMoveAction;
    // 플레이어 이동 경로의 적 시야 위험을 평가하고 경고 표시를 담당하는 컴포넌트다.
    [SerializeField] private GridMoveRiskEvaluator gridMoveRiskEvaluator;
    // 플레이어 이동 가능 범위의 AP 구간별 표시를 담당하는 컴포넌트다.
    [SerializeField] private GridMoveRangeHighlighter gridMoveRangeHighlighter;
    // 플레이어의 해킹 행동 판정과 실행을 담당하는 컴포넌트다. 해킹 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerHackAction hackAction;
    // 도깨비 환도의 현재 기준 칸과 회수 상태를 보관하는 선택 컴포넌트다. 검 행동을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerSwordState swordState;
    // 도깨비 환도 투척 행동 판정과 실행을 담당하는 선택 컴포넌트다. 검 투척 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerSwordThrowAction swordThrowAction;
    // 도깨비 환도 회수 행동 판정과 실행을 담당하는 선택 컴포넌트다. 검 회수 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerSwordRecallAction swordRecallAction;
    // 플레이어 근접 공격 행동 판정과 실행을 담당하는 선택 컴포넌트다. 근접 공격 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerMeleeAttackAction meleeAttackAction;
    // 플레이어 총 공격의 현재 총알 수를 보관하는 선택 컴포넌트다. 총 공격 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerGunAmmo gunAmmo;
    // 플레이어 총 공격 행동 판정과 실행을 담당하는 선택 컴포넌트다. 총 공격 기능을 쓰는 씬에서 연결한다.
    [SerializeField] private PlayerGunAttackAction gunAttackAction;

    // 현재 전술 유닛 등록소에 등록되어 있는지 나타낸다.
    private bool registeredToRegistry;

    public UnitFaction Faction => faction;
    public UnitControlType ControlType => controlType;
    public ControllableUnitData UnitData => unitData;
    public GridActor GridActor => gridActor;
    public ActorHealth Health => health;
    public bool IsAlive => health != null && !health.IsDead;
    public ActionPoint ActionPoint => actionPoint;
    public PlayerGridMoveAction GridMoveAction => gridMoveAction;
    public GridMoveRiskEvaluator GridMoveRiskEvaluator => gridMoveRiskEvaluator;
    public GridMoveRangeHighlighter GridMoveRangeHighlighter => gridMoveRangeHighlighter;
    public PlayerHackAction HackAction => hackAction;
    public PlayerSwordState SwordState => swordState;
    public PlayerSwordThrowAction SwordThrowAction => swordThrowAction;
    public PlayerSwordRecallAction SwordRecallAction => swordRecallAction;
    public PlayerMeleeAttackAction MeleeAttackAction => meleeAttackAction;
    public PlayerGunAmmo GunAmmo => gunAmmo;
    public PlayerGunAttackAction GunAttackAction => gunAttackAction;

    /// <summary>
    /// 전술 유닛 Context의 필수 참조와 능력 구성을 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidAbilityComposition())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 활성화될 때 전술 유닛 등록소 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        TryRegisterToRegistry(false);
    }

    /// <summary>
    /// 초기화 순서 때문에 놓친 전술 유닛 등록을 시작 시점에 다시 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterToRegistry(true);
    }

    /// <summary>
    /// 비활성화될 때 전술 유닛 등록소에서 등록을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (registeredToRegistry && TacticalUnitRegistry.Instance != null)
        {
            TacticalUnitRegistry.Instance.Unregister(this);
        }

        registeredToRegistry = false;
        CancelAllActionSelections();
    }

    /// <summary>
    /// 현재 유닛의 모든 행동 선택 모드와 이동 미리보기를 해제한다.
    /// </summary>
    public void CancelAllActionSelections()
    {
        gridMoveAction?.CancelMoveAction();
        gridMoveAction?.ClearMovePathPreview();
        hackAction?.CancelHackAction();
        swordThrowAction?.CancelSwordThrowAction();
        meleeAttackAction?.CancelMeleeAttackAction();
        gunAttackAction?.CancelGunAttackAction();
    }

    /// <summary>
    /// 지정한 능력이 이 유닛 데이터에 선언되어 있는지 확인한다.
    /// </summary>
    public bool HasAbility(UnitAbilityType ability)
    {
        return unitData != null && unitData.RequiresAbility(ability);
    }

    /// <summary>
    /// 전술 유닛 Context에 필수 참조가 모두 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (unitData == null)
        {
            Debug.LogError($"{nameof(TacticalUnitContext)} on {name}에는 {nameof(ControllableUnitData)} 참조가 필요합니다.", this);
            return false;
        }

        if (gridActor == null)
        {
            Debug.LogError($"{nameof(TacticalUnitContext)} on {name}에는 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (health == null)
        {
            Debug.LogError($"{nameof(TacticalUnitContext)} on {name}에는 {nameof(ActorHealth)} 참조가 필요합니다.", this);
            return false;
        }

        if (actionPoint == null)
        {
            Debug.LogError($"{nameof(TacticalUnitContext)} on {name}에는 {nameof(ActionPoint)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 데이터에 선언된 필수 능력과 실제 행동 컴포넌트 참조가 일치하는지 확인한다.
    /// </summary>
    public bool HasValidAbilityComposition()
    {
        bool valid = true;
        valid &= ValidateAbility(UnitAbilityType.Move, gridMoveAction != null && gridMoveRiskEvaluator != null && gridMoveRangeHighlighter != null);
        valid &= ValidateAbility(UnitAbilityType.Gun, gunAmmo != null && gunAttackAction != null);
        valid &= ValidateAbility(UnitAbilityType.Hack, hackAction != null);
        valid &= ValidateAbility(UnitAbilityType.Sword, swordState != null && swordThrowAction != null && swordRecallAction != null);
        valid &= ValidateAbility(UnitAbilityType.Melee, meleeAttackAction != null);
        valid &= ValidateAbility(UnitAbilityType.HeavyGun, false);
        return valid;
    }

    /// <summary>
    /// 능력 선언 여부와 관련 컴포넌트 구성 여부가 같은지 검사한다.
    /// </summary>
    private bool ValidateAbility(UnitAbilityType ability, bool hasComponents)
    {
        bool required = unitData != null && unitData.RequiresAbility(ability);
        if (required == hasComponents)
        {
            return true;
        }

        string reason = required ? "필수 컴포넌트가 누락되었습니다" : "데이터에 선언되지 않은 컴포넌트가 연결되었습니다";
        Debug.LogError($"{nameof(TacticalUnitContext)} on {name}의 {ability} 능력 구성이 올바르지 않습니다. {reason}.", this);
        return false;
    }

    /// <summary>
    /// 현재 씬의 전술 유닛 등록소에 이 유닛을 등록한다.
    /// </summary>
    private void TryRegisterToRegistry(bool logMissingRegistry)
    {
        if (registeredToRegistry)
        {
            return;
        }

        TacticalUnitRegistry registry = TacticalUnitRegistry.Instance;
        if (registry == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(TacticalUnitContext)} on {name}에는 씬의 {nameof(TacticalUnitRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        registry.Register(this);
        registeredToRegistry = true;
    }
}
