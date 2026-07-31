using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 현재 조작할 전술 유닛의 선택과 AP 소진 시 자동 전환을 관리한다.
/// 연출 중에도 선택은 허용하지만 실제 행동 실행 가능 여부는 행동 흐름 조정자가 판단한다.
/// </summary>
public class PlayerUnitControlManager : MonoBehaviour
{
    public static PlayerUnitControlManager Instance { get; private set; }

    [Header("Log")]
    // true면 유닛 선택과 자동 제어권 전환 결과를 출력한다.
    [SerializeField] private bool logSelection = true;

    // AP 변경 이벤트를 구독한 조작 가능 유닛 목록이다.
    private readonly List<TacticalUnitContext> subscribedUnits = new();
    // 이번 프레임에 포인터 선택 입력을 이미 판정했는지 나타낸다.
    private int processedSelectionFrame = -1;
    // 이번 프레임의 포인터 입력이 유닛 선택에 사용됐는지 나타낸다.
    private bool selectionConsumedThisFrame;

    public TacticalUnitContext ActiveUnit { get; private set; }

    // 현재 조작 유닛이 바뀔 때 이전 유닛과 새 유닛을 전달한다.
    public event Action<TacticalUnitContext, TacticalUnitContext> ActiveUnitChanged;

    /// <summary>
    /// 씬의 단일 플레이어 유닛 제어 매니저 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerUnitControlManager)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 등록소 이벤트와 기존 유닛을 연결하고 최초 조작 유닛을 선택한다.
    /// </summary>
    private void Start()
    {
        TacticalUnitRegistry registry = TacticalUnitRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError($"{nameof(PlayerUnitControlManager)} on {name}에는 씬의 {nameof(TacticalUnitRegistry)}가 필요합니다.", this);
            enabled = false;
            return;
        }

        registry.PlayerUnitRegistered += HandlePlayerUnitRegistered;
        registry.PlayerUnitUnregistered += HandlePlayerUnitUnregistered;
        IReadOnlyList<TacticalUnitContext> units = registry.PlayerControllableUnits;
        for (int i = 0; i < units.Count; i++)
        {
            SubscribeUnit(units[i]);
        }

        TrySelectNextAvailableUnit(null);
    }

    /// <summary>
    /// 좌클릭한 플레이어 진영 유닛을 선택한다.
    /// </summary>
    private void Update()
    {
        TryProcessSelectionInput();
    }

    /// <summary>
    /// 이벤트 구독과 전역 인스턴스 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        TacticalUnitRegistry registry = TacticalUnitRegistry.Instance;
        if (registry != null)
        {
            registry.PlayerUnitRegistered -= HandlePlayerUnitRegistered;
            registry.PlayerUnitUnregistered -= HandlePlayerUnitUnregistered;
        }

        while (subscribedUnits.Count > 0)
        {
            UnsubscribeUnit(subscribedUnits[subscribedUnits.Count - 1]);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 이번 프레임의 포인터 입력을 유닛 선택에 사용할지 한 번만 판정한다.
    /// 입력 컨트롤러도 호출할 수 있어 Update 실행 순서와 무관하게 같은 결과를 반환한다.
    /// </summary>
    public bool TryProcessSelectionInput()
    {
        if (processedSelectionFrame == Time.frameCount)
        {
            return selectionConsumedThisFrame;
        }

        processedSelectionFrame = Time.frameCount;
        selectionConsumedThisFrame = false;

        PlayerInputReader inputReader = PlayerInputReader.Instance;
        if (inputReader == null ||
            (ActionPresentationQueue.Instance != null && ActionPresentationQueue.Instance.IsPlaying) ||
            !inputReader.ConfirmPressedThisFrame ||
            !inputReader.TryGetPointerGridPosition(out GridPosition position) ||
            GridManager.Instance == null ||
            !GridManager.Instance.TryGetActorAt(position, out GridActor actor) ||
            TacticalUnitRegistry.Instance == null ||
            !TacticalUnitRegistry.Instance.TryGetPlayerControllableUnit(actor, out TacticalUnitContext unit))
        {
            return false;
        }

        selectionConsumedThisFrame = true;
        TrySelectUnit(unit);
        return true;
    }

    /// <summary>
    /// 지정한 유닛이 현재 선택 가능한 상태면 제어권을 넘긴다.
    /// </summary>
    public bool TrySelectUnit(TacticalUnitContext unit)
    {
        if (!CanSelectUnit(unit))
        {
            if (logSelection && unit != null)
            {
                Debug.Log($"{nameof(PlayerUnitControlManager)}: {unit.name} 유닛은 현재 선택할 수 없습니다. AP 또는 조작 상태를 확인하세요.", this);
            }

            return false;
        }

        if (ActiveUnit == unit)
        {
            return true;
        }

        TacticalUnitContext previous = ActiveUnit;
        previous?.CancelAllActionSelections();
        ActiveUnit = unit;
        ActiveUnit.CancelAllActionSelections();
        ActiveUnitChanged?.Invoke(previous, ActiveUnit);

        if (logSelection)
        {
            Debug.Log($"{nameof(PlayerUnitControlManager)}: 조작 유닛을 {ActiveUnit.name}(으)로 변경했습니다. AP: {ActiveUnit.ActionPoint.Current}", this);
        }

        return true;
    }

    /// <summary>
    /// 현재 유닛 다음 등록 순서부터 AP가 남은 유닛을 찾아 선택한다.
    /// </summary>
    public bool TrySelectNextAvailableUnit(TacticalUnitContext current)
    {
        TacticalUnitRegistry registry = TacticalUnitRegistry.Instance;
        if (registry == null)
        {
            return false;
        }

        IReadOnlyList<TacticalUnitContext> units = registry.PlayerControllableUnits;
        int startIndex = current != null ? IndexOf(units, current) + 1 : 0;
        for (int offset = 0; offset < units.Count; offset++)
        {
            TacticalUnitContext candidate = units[(startIndex + offset) % units.Count];
            if (CanSelectUnit(candidate))
            {
                return TrySelectUnit(candidate);
            }
        }

        TacticalUnitContext previous = ActiveUnit;
        previous?.CancelAllActionSelections();
        ActiveUnit = null;
        if (previous != null)
        {
            ActiveUnitChanged?.Invoke(previous, null);
        }

        return false;
    }

    /// <summary>
    /// 지정한 유닛이 플레이어가 현재 선택할 수 있는 상태인지 확인한다.
    /// </summary>
    public bool CanSelectUnit(TacticalUnitContext unit)
    {
        return unit != null &&
            unit.enabled &&
            unit.Faction == UnitFaction.Player &&
            unit.ControlType == UnitControlType.Player &&
            unit.IsAlive &&
            unit.ActionPoint != null &&
            unit.ActionPoint.Current > 0;
    }

    /// <summary>
    /// 등록된 조작 유닛의 AP 변경 이벤트를 구독한다.
    /// </summary>
    private void SubscribeUnit(TacticalUnitContext unit)
    {
        if (unit == null || subscribedUnits.Contains(unit) || unit.ActionPoint == null)
        {
            return;
        }

        subscribedUnits.Add(unit);
        unit.ActionPoint.ActionPointChanged += HandleActionPointChanged;
    }

    /// <summary>
    /// 등록 해제된 조작 유닛의 AP 변경 이벤트 구독을 해제한다.
    /// </summary>
    private void UnsubscribeUnit(TacticalUnitContext unit)
    {
        if (unit != null && unit.ActionPoint != null)
        {
            unit.ActionPoint.ActionPointChanged -= HandleActionPointChanged;
        }

        subscribedUnits.Remove(unit);
    }

    /// <summary>
    /// 새 플레이어 조작 유닛을 AP 감시에 추가한다.
    /// </summary>
    private void HandlePlayerUnitRegistered(TacticalUnitContext unit)
    {
        SubscribeUnit(unit);
        if (ActiveUnit == null)
        {
            TrySelectUnit(unit);
        }
    }

    /// <summary>
    /// 플레이어 조작 유닛 등록 해제를 처리하고 필요하면 다음 유닛으로 전환한다.
    /// </summary>
    private void HandlePlayerUnitUnregistered(TacticalUnitContext unit)
    {
        UnsubscribeUnit(unit);
        if (ActiveUnit == unit)
        {
            TrySelectNextAvailableUnit(unit);
        }
    }

    /// <summary>
    /// 현재 조작 유닛의 AP가 0이 되면 모든 행동 모드를 해제하고 다음 유닛으로 전환한다.
    /// </summary>
    private void HandleActionPointChanged(int current, int max)
    {
        if (ActiveUnit == null)
        {
            if (current > 0)
            {
                TrySelectNextAvailableUnit(null);
            }

            return;
        }

        if (ActiveUnit.ActionPoint.Current > 0)
        {
            return;
        }

        ActiveUnit.CancelAllActionSelections();
        TrySelectNextAvailableUnit(ActiveUnit);
    }

    /// <summary>
    /// 읽기 전용 유닛 목록에서 지정한 유닛의 인덱스를 찾는다.
    /// </summary>
    private static int IndexOf(IReadOnlyList<TacticalUnitContext> units, TacticalUnitContext target)
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] == target)
            {
                return i;
            }
        }

        return -1;
    }
}
