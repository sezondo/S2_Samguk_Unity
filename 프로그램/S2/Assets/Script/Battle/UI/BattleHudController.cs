using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD 하위 컨트롤러를 초기화하고 전투 상태·입력 흐름을 조율한다.
/// </summary>
public sealed class BattleHudController : MonoBehaviour
{
    private static readonly BattleHudActionType[] ActionOrder =
    {
        BattleHudActionType.Move,
        BattleHudActionType.Gun,
        BattleHudActionType.Melee,
        BattleHudActionType.SwordThrow,
        BattleHudActionType.SwordRecall,
        BattleHudActionType.Hack,
    };

    [Header("Data")]
    // HUD에서 사용할 상태 Sprite와 공통 원화 참조 에셋이다.
    [SerializeField] private BattleHudAssetSet assetSet;
    // Base HUD 좌측 상단에 표시할 현재 스테이지 목표 문구다.
    [SerializeField] private string objectiveText = "목표: 지정 지점에 도달";

    [Header("Scene Reference")]
    // 현재 플레이어·적 턴 상태를 제공한다.
    [SerializeField] private TurnManager turnManager;
    // 스테이지 진행 중 여부를 제공한다.
    [SerializeField] private StageStateManager stageStateManager;
    // 현재 조작 유닛 선택 상태를 제공한다.
    [SerializeField] private PlayerUnitControlManager unitControlManager;
    // 플레이어 유닛 목록을 제공한다.
    [SerializeField] private TacticalUnitRegistry tacticalUnitRegistry;
    // 적 유닛 목록과 인식 상태를 제공한다.
    [SerializeField] private EnemyRegistry enemyRegistry;
    // 새 행동을 차단해야 하는 연출 실행 상태를 제공한다.
    [SerializeField] private ActionPresentationQueue presentationQueue;
    // 실제 판정과 동일한 엄폐·명중률 미리보기를 제공한다.
    [SerializeField] private AttackResolutionCoordinator attackResolutionCoordinator;
    // 월드 액터 위치를 HUD 좌표로 바꿀 전투 카메라다.
    [SerializeField] private Camera worldCamera;

    [Header("Prefab Configuration")]
    // 1920×1080 화면 배율을 적용하는 CanvasScaler다.
    [SerializeField] private CanvasScaler canvasScaler;
    // AP 한 칸을 생성할 소형 Prefab이다.
    [SerializeField] private Image actionPointSegmentPrefab;
    // 탄약 한 발을 생성할 소형 Prefab이다.
    [SerializeField] private Image ammoSegmentPrefab;

    [Header("HUD Controller")]
    // 목표·턴·공통 행동 정보와 턴 종료를 관리한다.
    [SerializeField] private BattleHudBaseController baseHudController;
    // 등록 유닛 수에 맞춰 카드 Prefab을 관리한다.
    [SerializeField] private BattleHudUnitCardPanelController unitCardPanelController;
    // 선택 유닛에 맞는 유진·동료 HUD를 전환한다.
    [SerializeField] private BattleHudSelectedUnitPanelController selectedUnitHudController;
    // 월드 브래킷·적 상태·명중 미리보기를 관리한다.
    [SerializeField] private BattleHudTacticalOverlayController tacticalOverlayController;

    /// <summary>
    /// Prefab과 전투 참조를 검사하고 각 HUD 영역 Controller를 초기화한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        baseHudController.Initialize(assetSet, objectiveText, turnManager, stageStateManager,
            unitControlManager, presentationQueue, GetSelectedAction);
        unitCardPanelController.Initialize(assetSet, tacticalUnitRegistry);
        selectedUnitHudController.Initialize(assetSet, actionPointSegmentPrefab, ammoSegmentPrefab,
            CanUseAction, IsActionSelected, RequestAction, HandleActionHoverChanged);
        tacticalOverlayController.Initialize(assetSet, unitControlManager, enemyRegistry,
            attackResolutionCoordinator, worldCamera, GetSelectedAction);
    }

    /// <summary>
    /// 런타임 유닛 등록 후 카드·적 아이콘을 만들고 주요 전투 상태 이벤트를 구독한다.
    /// </summary>
    private void Start()
    {
        unitControlManager.ActiveUnitChanged += HandleActiveUnitChanged;
        turnManager.TurnStarted += HandleTurnChanged;
        stageStateManager.StageStateChanged += HandleStageStateChanged;
        tacticalUnitRegistry.PlayerUnitRegistered += HandlePlayerUnitRegistered;
        tacticalUnitRegistry.PlayerUnitUnregistered += HandlePlayerUnitUnregistered;

        unitCardPanelController.Rebuild();
        tacticalOverlayController.RebuildEnemyStateIcons();
        HandleActiveUnitChanged(null, unitControlManager.ActiveUnit);
        RefreshAll();
    }

    /// <summary>
    /// 전투 상태 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (unitControlManager != null)
        {
            unitControlManager.ActiveUnitChanged -= HandleActiveUnitChanged;
        }

        if (turnManager != null)
        {
            turnManager.TurnStarted -= HandleTurnChanged;
        }

        if (stageStateManager != null)
        {
            stageStateManager.StageStateChanged -= HandleStageStateChanged;
        }

        if (tacticalUnitRegistry != null)
        {
            tacticalUnitRegistry.PlayerUnitRegistered -= HandlePlayerUnitRegistered;
            tacticalUnitRegistry.PlayerUnitUnregistered -= HandlePlayerUnitUnregistered;
        }
    }

    /// <summary>
    /// 연출 실행과 포인터 위치처럼 매 프레임 달라지는 HUD 상태를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        RefreshAll();
        tacticalOverlayController.Refresh();
    }

    /// <summary>
    /// Base·선택 유닛·유닛 카드 HUD에 공통 전투 상태를 전달한다.
    /// </summary>
    private void RefreshAll()
    {
        bool playerTurn = turnManager.IsPlayerTurn;
        TacticalUnitContext activeUnit = unitControlManager.ActiveUnit;
        bool commonActionAvailable = activeUnit != null && activeUnit.IsAlive && playerTurn &&
                                     stageStateManager.IsPlaying && !presentationQueue.IsBusy &&
                                     activeUnit.ActionPoint != null && activeUnit.ActionPoint.Current > 0;

        baseHudController.Refresh(activeUnit, playerTurn);
        selectedUnitHudController.Refresh(commonActionAvailable);
        unitCardPanelController.Refresh(activeUnit, playerTurn);
    }

    /// <summary>
    /// 현재 유닛이 지정한 행동을 선택할 최소 자원과 컴포넌트를 가졌는지 확인한다.
    /// </summary>
    private static bool CanUseAction(TacticalUnitContext unit, BattleHudActionType actionType)
    {
        if (unit == null || unit.UnitData == null || unit.ActionPoint == null)
        {
            return false;
        }

        ControllableUnitData data = unit.UnitData;
        return actionType switch
        {
            BattleHudActionType.Move => unit.GridMoveAction != null && unit.ActionPoint.CanSpend(data.MoveActionPointCost),
            BattleHudActionType.Gun => unit.GunAttackAction != null && unit.GunAmmo != null &&
                                       unit.ActionPoint.CanSpend(data.GunAttackActionPointCost) && unit.GunAmmo.CanSpend(1),
            BattleHudActionType.Melee => unit.MeleeAttackAction != null && unit.ActionPoint.CanSpend(data.MeleeAttackActionPointCost),
            BattleHudActionType.SwordThrow => unit.SwordThrowAction != null && unit.ActionPoint.CanSpend(data.SwordThrowActionPointCost),
            BattleHudActionType.SwordRecall => unit.SwordRecallAction != null && unit.SwordState != null &&
                                               !unit.SwordState.IsRecalled && unit.ActionPoint.CanSpend(data.SwordRecallActionPointCost),
            BattleHudActionType.Hack => unit.HackAction != null && unit.ActionPoint.CanSpend(data.HackActionPointCost),
            _ => false,
        };
    }

    /// <summary>
    /// 현재 유닛에서 지정한 행동 모드가 선택돼 있는지 확인한다.
    /// </summary>
    private static bool IsActionSelected(TacticalUnitContext unit, BattleHudActionType actionType)
    {
        return actionType switch
        {
            BattleHudActionType.Move => unit.GridMoveAction != null && unit.GridMoveAction.IsMoveSelected,
            BattleHudActionType.Gun => unit.GunAttackAction != null && unit.GunAttackAction.IsGunAttackSelected,
            BattleHudActionType.Melee => unit.MeleeAttackAction != null && unit.MeleeAttackAction.IsMeleeAttackSelected,
            BattleHudActionType.SwordThrow => unit.SwordThrowAction != null && unit.SwordThrowAction.IsSwordThrowSelected,
            BattleHudActionType.Hack => unit.HackAction != null && unit.HackAction.IsHackSelected,
            _ => false,
        };
    }

    /// <summary>
    /// 현재 선택된 행동 모드를 HUD 행동 종류로 변환한다.
    /// </summary>
    private static BattleHudActionType? GetSelectedAction(TacticalUnitContext unit)
    {
        for (int i = 0; i < ActionOrder.Length; i++)
        {
            if (IsActionSelected(unit, ActionOrder[i]))
            {
                return ActionOrder[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 행동 버튼 입력을 현재 선택 유닛의 기존 행동 선택 또는 실행 요청으로 전달한다.
    /// </summary>
    private void RequestAction(BattleHudActionType actionType)
    {
        TacticalUnitContext unit = unitControlManager.ActiveUnit;
        if (!CanUseAction(unit, actionType))
        {
            return;
        }

        unit.CancelAllActionSelections();
        switch (actionType)
        {
            case BattleHudActionType.Move:
                unit.GridMoveAction.SelectMoveAction();
                break;
            case BattleHudActionType.Gun:
                unit.GunAttackAction.SelectGunAttackAction();
                break;
            case BattleHudActionType.Melee:
                unit.MeleeAttackAction.SelectMeleeAttackAction();
                break;
            case BattleHudActionType.SwordThrow:
                unit.SwordThrowAction.SelectSwordThrowAction();
                break;
            case BattleHudActionType.SwordRecall:
                PlayerUnitActionFlowController.Instance?.TryExecuteSwordRecall();
                break;
            case BattleHudActionType.Hack:
                unit.HackAction.SelectHackAction();
                break;
        }
    }

    /// <summary>
    /// 행동 버튼 Hover 상태를 Base HUD의 공통 행동 정보에 전달한다.
    /// </summary>
    private void HandleActionHoverChanged(BattleHudActionType actionType, bool hovered)
    {
        baseHudController.SetHoveredAction(actionType, hovered);
    }

    /// <summary>
    /// 선택 유닛이 바뀌면 대응하는 미리 배치된 캐릭터 HUD로 전환한다.
    /// </summary>
    private void HandleActiveUnitChanged(TacticalUnitContext _, TacticalUnitContext nextUnit)
    {
        baseHudController.ClearHoveredAction();
        selectedUnitHudController.ShowUnit(nextUnit);
    }

    /// <summary>
    /// 턴 변경을 받으면 현재 화면 상태를 즉시 갱신한다.
    /// </summary>
    private void HandleTurnChanged(TurnSide _)
    {
        RefreshAll();
    }

    /// <summary>
    /// 스테이지 종료 시 행동 선택과 포인터 정보를 정리한다.
    /// </summary>
    private void HandleStageStateChanged(StageState _, StageState nextState)
    {
        if (nextState != StageState.Playing)
        {
            unitControlManager.ActiveUnit?.CancelAllActionSelections();
            tacticalOverlayController.ClearPointerPreview();
        }
    }

    /// <summary>
    /// 플레이어 유닛 등록 변경을 받으면 카드 목록을 다시 만든다.
    /// </summary>
    private void HandlePlayerUnitRegistered(TacticalUnitContext _)
    {
        unitCardPanelController.Rebuild();
    }

    /// <summary>
    /// 플레이어 유닛 등록 해제를 받으면 카드 목록을 다시 만든다.
    /// </summary>
    private void HandlePlayerUnitUnregistered(TacticalUnitContext _)
    {
        unitCardPanelController.Rebuild();
    }

    /// <summary>
    /// HUD Prefab과 전투 씬의 필수 참조가 모두 연결되어 있는지 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool valid = assetSet != null && turnManager != null && stageStateManager != null &&
                     unitControlManager != null && tacticalUnitRegistry != null && enemyRegistry != null &&
                     presentationQueue != null && attackResolutionCoordinator != null && worldCamera != null &&
                     canvasScaler != null && actionPointSegmentPrefab != null && ammoSegmentPrefab != null &&
                     baseHudController != null && unitCardPanelController != null &&
                     selectedUnitHudController != null && tacticalOverlayController != null &&
                     baseHudController.HasValidReference() && unitCardPanelController.HasValidReference() &&
                     selectedUnitHudController.HasValidReference() && tacticalOverlayController.HasValidReference();
        if (!valid)
        {
            Debug.LogError($"{nameof(BattleHudController)} on {name}에는 전투 참조와 하위 HUD Controller 참조가 필요합니다.", this);
        }

        return valid;
    }

    /// <summary>
    /// HUD 에셋·목표 문구와 CanvasScaler 기준값이 유효한지 검사한다.
    /// </summary>
    public bool HasValidData()
    {
        if (!assetSet.HasValidData())
        {
            Debug.LogError($"{nameof(BattleHudController)} on {name}의 HUD 에셋 세트에 필수 폰트 또는 Sprite가 없습니다.", assetSet);
            return false;
        }

        if (string.IsNullOrWhiteSpace(objectiveText))
        {
            Debug.LogError($"{nameof(BattleHudController)} on {name}의 목표 문구가 비어 있습니다.", this);
            return false;
        }

        bool validScaler = canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                           canvasScaler.referenceResolution == new Vector2(1920f, 1080f) &&
                           Mathf.Approximately(canvasScaler.matchWidthOrHeight, 0.5f);
        if (!validScaler)
        {
            Debug.LogError($"{nameof(BattleHudController)} on {name}의 CanvasScaler는 1920×1080 Scale With Screen Size, Match 0.5여야 합니다.", canvasScaler);
        }

        return validScaler;
    }
}
