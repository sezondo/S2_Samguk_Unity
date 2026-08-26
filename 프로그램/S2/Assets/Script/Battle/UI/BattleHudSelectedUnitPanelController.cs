using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleHud Prefab에 미리 배치된 선택 유닛 HUD View를 능력 구성에 따라 활성 전환한다.
/// </summary>
public sealed class BattleHudSelectedUnitPanelController : MonoBehaviour
{
    [Header("Prebuilt Unit HUD")]
    // BattleHud Prefab에 미리 배치된 능력 구성별 HUD View 목록이다.
    [SerializeField] private BattleHudSelectedUnitView[] unitHudViews;

    // 능력 구성별로 미리 배치된 View를 찾기 위한 표다.
    private readonly Dictionary<UnitAbilityType, BattleHudSelectedUnitView> views = new();
    // 현재 화면에 활성화된 선택 유닛 HUD View다.
    private BattleHudSelectedUnitView activeView;

    /// <summary>
    /// 미리 배치된 모든 View에 전투 상태 질의와 행동 요청 함수를 연결한다.
    /// </summary>
    public void Initialize(
        BattleHudAssetSet assets,
        Image actionPointSegmentPrefab,
        Image ammoSegmentPrefab,
        Func<TacticalUnitContext, BattleHudActionType, bool> canUseAction,
        Func<TacticalUnitContext, BattleHudActionType, bool> isActionSelected,
        Action<BattleHudActionType> requestAction,
        Action<BattleHudActionType, bool> hoverChanged)
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        views.Clear();
        for (int i = 0; i < unitHudViews.Length; i++)
        {
            if (!RegisterView(unitHudViews[i], assets, actionPointSegmentPrefab, ammoSegmentPrefab,
                    canUseAction, isActionSelected, requestAction, hoverChanged))
            {
                enabled = false;
                return;
            }
        }
    }

    /// <summary>
    /// 선택 유닛 능력과 일치하는 미리 배치된 View만 활성화한다.
    /// </summary>
    public void ShowUnit(TacticalUnitContext unit)
    {
        if (activeView != null)
        {
            activeView.gameObject.SetActive(false);
            activeView = null;
        }

        if (unit == null)
        {
            return;
        }

        if (unit.UnitData == null || !views.TryGetValue(unit.UnitData.RequiredAbilities, out BattleHudSelectedUnitView nextView))
        {
            Debug.LogError($"{nameof(BattleHudSelectedUnitPanelController)}: {unit.name}의 능력 구성 {unit.UnitData?.RequiredAbilities}에 대응하는 HUD가 없습니다.", this);
            return;
        }

        activeView = nextView;
        activeView.gameObject.SetActive(true);
        activeView.BindUnit(unit);
    }

    /// <summary>
    /// 현재 활성화된 캐릭터 HUD의 자원과 버튼 상태를 갱신한다.
    /// </summary>
    public void Refresh(bool commonActionAvailable)
    {
        activeView?.Refresh(commonActionAvailable);
    }

    /// <summary>
    /// 능력 구성별 HUD View 목록과 각 View의 필수 참조를 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (unitHudViews == null || unitHudViews.Length == 0)
        {
            Debug.LogError($"{nameof(BattleHudSelectedUnitPanelController)} on {name}에는 하나 이상의 선택 유닛 HUD View가 필요합니다.", this);
            return false;
        }

        for (int i = 0; i < unitHudViews.Length; i++)
        {
            BattleHudSelectedUnitView view = unitHudViews[i];
            if (view == null)
            {
                Debug.LogError($"{nameof(BattleHudSelectedUnitPanelController)} on {name}의 HUD View 목록 {i}번 항목이 비어 있습니다.", this);
                return false;
            }

            if (!view.HasValidReference())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 미리 배치된 View 하나를 능력 프로필 표에 등록하고 초기 비활성화한다.
    /// </summary>
    private bool RegisterView(
        BattleHudSelectedUnitView view,
        BattleHudAssetSet assets,
        Image actionPointSegmentPrefab,
        Image ammoSegmentPrefab,
        Func<TacticalUnitContext, BattleHudActionType, bool> canUseAction,
        Func<TacticalUnitContext, BattleHudActionType, bool> isActionSelected,
        Action<BattleHudActionType> requestAction,
        Action<BattleHudActionType, bool> hoverChanged)
    {
        if (views.ContainsKey(view.AbilityProfile))
        {
            Debug.LogError($"{nameof(BattleHudSelectedUnitPanelController)}: {view.AbilityProfile} HUD가 중복 배치됐습니다.", this);
            return false;
        }

        view.Initialize(assets, actionPointSegmentPrefab, ammoSegmentPrefab,
            canUseAction, isActionSelected, requestAction, hoverChanged);
        views.Add(view.AbilityProfile, view);
        view.gameObject.SetActive(false);
        return true;
    }
}
