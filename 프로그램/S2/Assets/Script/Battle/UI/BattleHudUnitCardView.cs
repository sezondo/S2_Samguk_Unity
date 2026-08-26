using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 유닛 카드 Prefab의 이름·HP·AP와 선택·행동 종료·사망 상태를 표시한다.
/// </summary>
public sealed class BattleHudUnitCardView : MonoBehaviour
{
    [Header("View")]
    // 카드 전체를 클릭해 유닛 선택을 요청하는 버튼이다.
    [SerializeField] private Button button;
    // 유닛 데이터에 연결된 캐릭터 초상화를 표시하는 이미지다.
    [SerializeField] private Image portraitImage;
    // 현재 HP·AP를 표시하는 텍스트다.
    [SerializeField] private TMP_Text stateText;
    // 선택·행동 종료·사망 상태 Sprite를 표시하는 이미지다.
    [SerializeField] private Image stateOverlay;

    // 이 카드가 표시하는 플레이어 전술 유닛이다.
    private TacticalUnitContext unit;
    // 선택된 유닛 카드 상태 Sprite다.
    private Sprite selectedSprite;
    // 행동 가능한 유닛 카드 상태 Sprite다.
    private Sprite availableSprite;
    // AP를 모두 사용한 유닛 카드 상태 Sprite다.
    private Sprite finishedSprite;
    // 사망한 유닛 카드 상태 Sprite다.
    private Sprite deadSprite;

    public TacticalUnitContext Unit => unit;

    /// <summary>
    /// Prefab View를 지정한 유닛과 HUD 상태 Sprite에 연결한다.
    /// </summary>
    public void Initialize(TacticalUnitContext targetUnit, BattleHudAssetSet assets)
    {
        if (!HasValidReference() || targetUnit == null || assets == null)
        {
            Debug.LogError($"{nameof(BattleHudUnitCardView)} on {name}의 초기화 데이터가 올바르지 않습니다.", this);
            enabled = false;
            return;
        }

        if (targetUnit.UnitData == null)
        {
            Debug.LogError($"{nameof(BattleHudUnitCardView)}: {targetUnit.name}에는 {nameof(ControllableUnitData)} 참조가 필요합니다.", targetUnit);
            enabled = false;
            return;
        }

        if (targetUnit.UnitData.Portrait == null)
        {
            Debug.LogError($"{nameof(BattleHudUnitCardView)}: {targetUnit.UnitData.name}에 전투 HUD용 초상화가 필요합니다.", targetUnit.UnitData);
            enabled = false;
            return;
        }

        unit = targetUnit;
        selectedSprite = assets.PortraitSelected;
        availableSprite = assets.PortraitAvailable;
        finishedSprite = assets.PortraitFinished;
        deadSprite = assets.PortraitDead;

        portraitImage.sprite = unit.UnitData.Portrait;
        portraitImage.preserveAspect = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(SelectUnit);
    }

    /// <summary>
    /// 현재 유닛 상태와 선택 여부를 카드 화면에 반영한다.
    /// </summary>
    public void Refresh(bool isSelected, bool isPlayerTurn)
    {
        if (unit == null || !enabled)
        {
            return;
        }

        int hp = unit.Health != null ? unit.Health.CurrentHitPoint : 0;
        int maxHp = unit.Health != null ? unit.Health.MaxHitPoint : 0;
        int ap = unit.ActionPoint != null ? unit.ActionPoint.Current : 0;
        int maxAp = unit.ActionPoint != null ? unit.ActionPoint.Max : 0;
        stateText.text = $"HP {hp}/{maxHp}\nAP {ap}/{maxAp}";

        Sprite nextState;
        if (!unit.IsAlive)
        {
            nextState = deadSprite;
        }
        else if (isSelected)
        {
            nextState = selectedSprite;
        }
        else if (!isPlayerTurn || ap <= 0)
        {
            nextState = finishedSprite;
        }
        else
        {
            nextState = availableSprite;
        }

        stateOverlay.sprite = nextState;
        stateOverlay.enabled = nextState != null;
        button.interactable = isPlayerTurn && unit.IsAlive && ap > 0;
    }

    /// <summary>
    /// 카드 Prefab의 필수 UI 참조가 연결되어 있는지 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool valid = button != null && portraitImage != null && stateText != null && stateOverlay != null;
        if (!valid)
        {
            Debug.LogError($"{nameof(BattleHudUnitCardView)} on {name}에는 버튼·초상화·상태 텍스트·오버레이 참조가 필요합니다.", this);
        }

        return valid;
    }

    /// <summary>
    /// 카드에 대응하는 플레이어 유닛 선택을 제어 매니저에 요청한다.
    /// </summary>
    private void SelectUnit()
    {
        PlayerUnitControlManager.Instance?.TrySelectUnit(unit);
    }
}
