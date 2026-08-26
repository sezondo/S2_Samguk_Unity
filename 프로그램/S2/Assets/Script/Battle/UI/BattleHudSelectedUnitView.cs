using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터별 선택 유닛 HUD Prefab의 자원과 실제 보유 행동 버튼을 표시한다.
/// </summary>
public sealed class BattleHudSelectedUnitView : MonoBehaviour
{
    [Header("Profile")]
    // 이 Prefab이 담당하는 유닛의 정확한 능력 구성이다.
    [SerializeField] private UnitAbilityType abilityProfile;

    [Header("View")]
    // 현재 선택 유닛 이름을 표시한다.
    [SerializeField] private TMP_Text unitNameText;
    // 현재 선택 유닛 AP 세그먼트가 배치될 루트다.
    [SerializeField] private RectTransform actionPointRoot;
    // 총기 능력이 있는 유닛의 탄약 세그먼트가 배치될 루트다.
    [SerializeField] private RectTransform ammoRoot;

    // Prefab에 실제로 배치된 행동 버튼 View 목록이다.
    private BattleHudActionButtonView[] actionButtons;
    // 현재 AP 세그먼트 이미지 목록이다.
    private readonly List<Image> actionPointSegments = new();
    // 현재 탄약 세그먼트 이미지 목록이다.
    private readonly List<Image> ammoSegments = new();
    // 행동 사용 가능 여부를 전투 HUD에 질의하는 함수다.
    private Func<TacticalUnitContext, BattleHudActionType, bool> canUseAction;
    // 현재 행동 선택 여부를 전투 HUD에 질의하는 함수다.
    private Func<TacticalUnitContext, BattleHudActionType, bool> isActionSelected;
    // 행동 버튼 입력을 기존 전투 행동 흐름에 전달하는 함수다.
    private Action<BattleHudActionType> requestAction;
    // 행동 Hover 상태를 공통 행동 정보 패널에 전달한다.
    private Action<BattleHudActionType, bool> hoverChanged;
    // AP 한 칸을 생성할 소형 Prefab이다.
    private Image actionPointSegmentPrefab;
    // 탄약 한 발을 생성할 소형 Prefab이다.
    private Image ammoSegmentPrefab;
    // 이 View가 현재 표시하는 선택 유닛이다.
    private TacticalUnitContext boundUnit;
    // 자원 세그먼트를 마지막으로 구성한 유닛이다.
    private TacticalUnitContext segmentedUnit;

    public UnitAbilityType AbilityProfile => abilityProfile;

    /// <summary>
    /// 미리 배치된 행동 버튼과 동적 세그먼트 Prefab에 런타임 함수를 연결한다.
    /// </summary>
    public void Initialize(
        BattleHudAssetSet assets,
        Image apSegmentPrefab,
        Image bulletSegmentPrefab,
        Func<TacticalUnitContext, BattleHudActionType, bool> canUse,
        Func<TacticalUnitContext, BattleHudActionType, bool> isSelected,
        Action<BattleHudActionType> request,
        Action<BattleHudActionType, bool> hoverCallback)
    {
        if (!HasValidReference() || assets == null || apSegmentPrefab == null || bulletSegmentPrefab == null ||
            canUse == null || isSelected == null || request == null || hoverCallback == null)
        {
            Debug.LogError($"{nameof(BattleHudSelectedUnitView)} on {name}의 초기화 데이터가 올바르지 않습니다.", this);
            enabled = false;
            return;
        }

        actionPointSegmentPrefab = apSegmentPrefab;
        ammoSegmentPrefab = bulletSegmentPrefab;
        canUseAction = canUse;
        isActionSelected = isSelected;
        requestAction = request;
        hoverChanged = hoverCallback;
        actionButtons = GetComponentsInChildren<BattleHudActionButtonView>(true);
        for (int i = 0; i < actionButtons.Length; i++)
        {
            BattleHudActionButtonView buttonView = actionButtons[i];
            BattleHudActionType actionType = buttonView.ActionType;
            buttonView.Bind(assets, () => requestAction(actionType), hoverChanged);
        }
    }

    /// <summary>
    /// 이 View가 표시할 선택 유닛을 변경하고 자원 세그먼트를 다시 구성한다.
    /// </summary>
    public void BindUnit(TacticalUnitContext unit)
    {
        boundUnit = unit;
        segmentedUnit = null;
        Refresh(false);
    }

    /// <summary>
    /// 선택 유닛의 자원과 Prefab에 존재하는 행동 버튼 상태를 갱신한다.
    /// </summary>
    public void Refresh(bool commonActionAvailable)
    {
        if (boundUnit == null || !enabled)
        {
            return;
        }

        unitNameText.text = boundUnit.UnitData != null ? boundUnit.UnitData.DisplayName : boundUnit.name;
        RefreshResourceSegments();
        for (int i = 0; i < actionButtons.Length; i++)
        {
            BattleHudActionButtonView button = actionButtons[i];
            bool available = commonActionAvailable && canUseAction(boundUnit, button.ActionType);
            bool selected = isActionSelected(boundUnit, button.ActionType);
            button.SetState(available, selected, false);
        }
    }

    /// <summary>
    /// Prefab에 필요한 공통 UI 참조가 모두 연결되어 있는지 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool valid = unitNameText != null && actionPointRoot != null && ammoRoot != null;
        if (!valid)
        {
            Debug.LogError($"{nameof(BattleHudSelectedUnitView)} on {name}에는 이름·AP·탄약 루트 참조가 필요합니다.", this);
        }

        return valid;
    }

    /// <summary>
    /// 선택 유닛의 최대 AP·탄약 수만큼 소형 Prefab을 만들고 현재 보유량을 표시한다.
    /// </summary>
    private void RefreshResourceSegments()
    {
        if (segmentedUnit != boundUnit)
        {
            segmentedUnit = boundUnit;
            RebuildSegments(actionPointRoot, actionPointSegments, boundUnit.ActionPoint?.Max ?? 0, actionPointSegmentPrefab, false);
            RebuildSegments(ammoRoot, ammoSegments, boundUnit.GunAmmo?.MaxAmmo ?? 0, ammoSegmentPrefab, true);
        }

        int currentAp = boundUnit.ActionPoint?.Current ?? 0;
        for (int i = 0; i < actionPointSegments.Count; i++)
        {
            actionPointSegments[i].color = i < currentAp ? Color.white : new Color(0.18f, 0.23f, 0.25f, 0.45f);
        }

        int currentAmmo = boundUnit.GunAmmo?.CurrentAmmo ?? 0;
        for (int i = 0; i < ammoSegments.Count; i++)
        {
            ammoSegments[i].color = i < currentAmmo ? Color.white : new Color(0.22f, 0.2f, 0.16f, 0.35f);
        }
    }

    /// <summary>
    /// 지정한 최대값만큼 AP 또는 탄약 소형 Prefab을 다시 배치한다.
    /// </summary>
    private static void RebuildSegments(RectTransform root, List<Image> segments, int count, Image segmentPrefab, bool vertical)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Destroy(root.GetChild(i).gameObject);
        }

        segments.Clear();
        for (int i = 0; i < count; i++)
        {
            Image segment = Instantiate(segmentPrefab, root);
            segment.name = $"Segment_{i + 1}";
            RectTransform rect = segment.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = vertical ? new Vector2(0f, i * 23f) : new Vector2(i * 68f, 0f);
            segments.Add(segment);
        }
    }
}
