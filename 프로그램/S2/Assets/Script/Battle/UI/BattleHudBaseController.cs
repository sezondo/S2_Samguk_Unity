using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목표·턴 배너·공통 행동 정보·턴 종료처럼 항상 존재하는 Base HUD를 관리한다.
/// </summary>
public sealed class BattleHudBaseController : MonoBehaviour
{
    [Header("View")]
    // 현재 스테이지 목표를 표시하는 텍스트다.
    [SerializeField] private TMP_Text objectiveTextView;
    // 현재 턴을 표시하는 텍스트다.
    [SerializeField] private TMP_Text turnText;
    // 플레이어·적 턴 상태 Sprite를 표시하는 이미지다.
    [SerializeField] private Image turnGlowImage;
    // Hover 또는 선택된 행동 이름을 표시하는 텍스트다.
    [SerializeField] private TMP_Text actionNameText;
    // 행동 비용·피해·사거리 설명을 표시하는 텍스트다.
    [SerializeField] private TMP_Text actionDetailText;
    // 현재 플레이어 턴을 끝내는 버튼이다.
    [SerializeField] private Button endTurnButton;

    // HUD 상태 Sprite를 제공하는 공통 에셋이다.
    private BattleHudAssetSet assetSet;
    // 현재 턴 상태와 턴 종료 기능을 제공한다.
    private TurnManager turnManager;
    // 스테이지 진행 상태를 제공한다.
    private StageStateManager stageStateManager;
    // 현재 선택 유닛을 제공한다.
    private PlayerUnitControlManager unitControlManager;
    // 행동 입력을 막아야 하는 연출 상태를 제공한다.
    private ActionPresentationQueue presentationQueue;
    // 현재 선택된 행동을 조회하는 함수다.
    private Func<TacticalUnitContext, BattleHudActionType?> getSelectedAction;
    // 포인터가 올라간 행동이다. 없으면 선택된 행동을 표시한다.
    private BattleHudActionType? hoveredAction;

    /// <summary>
    /// Base HUD를 전투 상태와 연결하고 턴 종료 버튼 입력을 등록한다.
    /// </summary>
    public void Initialize(
        BattleHudAssetSet assets,
        string objectiveText,
        TurnManager battleTurnManager,
        StageStateManager battleStageStateManager,
        PlayerUnitControlManager battleUnitControlManager,
        ActionPresentationQueue battlePresentationQueue,
        Func<TacticalUnitContext, BattleHudActionType?> selectedActionResolver)
    {
        if (!HasValidReference() || assets == null || string.IsNullOrWhiteSpace(objectiveText) ||
            battleTurnManager == null || battleStageStateManager == null || battleUnitControlManager == null ||
            battlePresentationQueue == null || selectedActionResolver == null)
        {
            Debug.LogError($"{nameof(BattleHudBaseController)} on {name}의 초기화 참조가 올바르지 않습니다.", this);
            enabled = false;
            return;
        }

        assetSet = assets;
        turnManager = battleTurnManager;
        stageStateManager = battleStageStateManager;
        unitControlManager = battleUnitControlManager;
        presentationQueue = battlePresentationQueue;
        getSelectedAction = selectedActionResolver;
        objectiveTextView.text = objectiveText;
        endTurnButton.onClick.RemoveAllListeners();
        endTurnButton.onClick.AddListener(RequestEndTurn);
    }

    /// <summary>
    /// 턴·행동 가능 상태와 현재 행동 설명을 Base HUD에 반영한다.
    /// </summary>
    public void Refresh(TacticalUnitContext activeUnit, bool isPlayerTurn)
    {
        turnText.text = isPlayerTurn ? "아군 턴" : "적군 턴";
        turnGlowImage.sprite = isPlayerTurn ? assetSet.TurnGlowPlayer : assetSet.TurnGlowEnemy;
        endTurnButton.interactable = isPlayerTurn && stageStateManager.IsPlaying && !presentationQueue.IsBusy;
        RefreshActionInfo(activeUnit);
    }

    /// <summary>
    /// 행동 버튼 Hover 상태를 공통 행동 정보 표시 기준에 반영한다.
    /// </summary>
    public void SetHoveredAction(BattleHudActionType actionType, bool hovered)
    {
        hoveredAction = hovered ? actionType : null;
    }

    /// <summary>
    /// 선택 유닛 변경 시 이전 유닛에서 남은 Hover 표시를 제거한다.
    /// </summary>
    public void ClearHoveredAction()
    {
        hoveredAction = null;
    }

    /// <summary>
    /// Base HUD Prefab의 필수 View 참조가 연결되어 있는지 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool valid = objectiveTextView != null && turnText != null && turnGlowImage != null &&
                     actionNameText != null && actionDetailText != null && endTurnButton != null;
        if (!valid)
        {
            Debug.LogError($"{nameof(BattleHudBaseController)} on {name}에는 Base HUD View 참조가 필요합니다.", this);
        }

        return valid;
    }

    /// <summary>
    /// Hover 중인 행동 또는 현재 선택된 행동의 비용·효과를 표시한다.
    /// </summary>
    private void RefreshActionInfo(TacticalUnitContext unit)
    {
        if (unit == null || unit.UnitData == null)
        {
            actionNameText.text = "행동 선택";
            actionDetailText.text = "조작할 유닛을 선택하세요";
            return;
        }

        BattleHudActionType? action = hoveredAction ?? getSelectedAction(unit);
        if (action == null)
        {
            actionNameText.text = "행동 선택";
            actionDetailText.text = "이 유닛이 사용할 행동을 선택하세요";
            return;
        }

        ControllableUnitData data = unit.UnitData;
        switch (action.Value)
        {
            case BattleHudActionType.Move:
                actionNameText.text = "이동";
                actionDetailText.text = $"AP {data.MoveActionPointCost} · AP당 {data.MoveDistancePerActionPoint}칸";
                break;
            case BattleHudActionType.Gun:
                actionNameText.text = "총격";
                actionDetailText.text = $"AP {data.GunAttackActionPointCost} · 탄약 1 · 피해 {data.GunAttackDamage} · 사거리 {data.GunAttackRange}";
                if (hoveredAction == null && unit.GunAttackAction != null && unit.GunAttackAction.IsGunAttackSelected &&
                    ActionPresentationQueue.Instance != null && !ActionPresentationQueue.Instance.IsBusy &&
                    PlayerInputReader.Instance != null && PlayerInputReader.Instance.TryGetPointerGridPosition(out GridPosition target))
                {
                    RefreshGunTargetInfo(unit, target);
                }
                break;
            case BattleHudActionType.Melee:
                actionNameText.text = "근접 공격";
                int damage = unit.SwordState != null && unit.SwordState.IsRecalled
                    ? data.MeleeDamageWithSword
                    : data.MeleeDamageWithoutSword;
                actionDetailText.text = $"AP {data.MeleeAttackActionPointCost} · 피해 {damage} · 주변 8칸";
                break;
            case BattleHudActionType.SwordThrow:
                actionNameText.text = "검 투척";
                actionDetailText.text = $"AP {data.SwordThrowActionPointCost} · 피해 {data.SwordThrowDamage} · 사거리 {data.SwordThrowRange}";
                break;
            case BattleHudActionType.SwordRecall:
                actionNameText.text = "검 회수";
                actionDetailText.text = $"AP {data.SwordRecallActionPointCost} · 거리 제한 없음";
                break;
            case BattleHudActionType.Hack:
                actionNameText.text = "해킹";
                actionDetailText.text = $"AP {data.HackActionPointCost} · 사거리 {data.HackRange}";
                break;
        }
        if (hoveredAction == null && ActionRangeOverlay.TryGetSelection(unit, out var selected, out _, out _) && selected == action.Value &&
            presentationQueue != null && !presentationQueue.IsBusy && PlayerInputReader.Instance != null &&
            PlayerInputReader.Instance.TryGetPointerGridPosition(out GridPosition pointer))
            RefreshAdditionalTargetInfo(unit, selected, pointer);
    }

    /// <summary>검 투척·근접·해킹의 대상 상태를 기존 하단 행동 정보에 표시한다.</summary>
    private void RefreshAdditionalTargetInfo(TacticalUnitContext unit, BattleHudActionType action, GridPosition target)
    {
        if (!ActionRangeOverlay.TryDescribeTarget(unit, action, target, out _, out string reason, out _)) return;
        string label = action == BattleHudActionType.Melee ? "근접 공격" : action == BattleHudActionType.SwordThrow ? "검 투척" : "해킹";
        actionNameText.text = $"{label} · {reason}";
    }

    /// <summary>실제로 공개된 대상만 기존 하단 행동 정보에 표시한다.</summary>
    private void RefreshGunTargetInfo(TacticalUnitContext unit, GridPosition target)
    {
        ControllableUnitData data = unit.UnitData;
        bool valid = unit.GunAttackAction.TryPreviewTarget(target, out GridActor actor, out string reason);
        if (actor == null || PlayerVisionPresenter.Instance == null ||
            !PlayerVisionPresenter.Instance.ShouldShowEnemyActorInSnapshot(actor) ||
            ActorPresentationRegistry.Instance == null ||
            !ActorPresentationRegistry.Instance.TryGetVisual(actor, out var visual) || visual.VisionAlpha <= .01f) return;
        actionNameText.text = valid ? "총격 · 공격 가능" : $"총격 · {reason}";
        if (valid && AttackResolutionCoordinator.Instance.TryPreviewAttack(unit.GridActor.GridPosition, target, data.GunAttackAccuracy, out AttackPreviewResult preview))
            actionDetailText.text = $"명중 {preview.FinalHitChance}% · AP {data.GunAttackActionPointCost} · 탄약 1" +
                (preview.CoverResult.HasCover ? $" · 엄폐 {preview.CoverResult.HitChanceModifier}%" : "");
    }

    /// <summary>
    /// 플레이어 턴이고 연출이 정지된 상태에서만 현재 턴 종료를 요청한다.
    /// </summary>
    private void RequestEndTurn()
    {
        if (turnManager.IsPlayerTurn && stageStateManager.IsPlaying && !presentationQueue.IsBusy)
        {
            unitControlManager.ActiveUnit?.CancelAllActionSelections();
            turnManager.EndCurrentTurn();
        }
    }
}
