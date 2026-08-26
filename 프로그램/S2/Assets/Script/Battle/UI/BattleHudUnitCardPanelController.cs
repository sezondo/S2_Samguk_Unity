using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등록된 플레이어 유닛 수에 맞춰 카드 Prefab을 생성·캐싱하고 상태를 갱신한다.
/// </summary>
public sealed class BattleHudUnitCardPanelController : MonoBehaviour
{
    [Header("View")]
    // 유닛 카드 Prefab을 배치할 루트다.
    [SerializeField] private RectTransform unitCardRoot;
    // 등록 유닛 수에 따라 생성할 카드 Prefab이다.
    [SerializeField] private BattleHudUnitCardView unitCardPrefab;

    // 카드 상태 Sprite를 제공하는 HUD 에셋이다.
    private BattleHudAssetSet assetSet;
    // 플레이어 조작 유닛 목록을 제공한다.
    private TacticalUnitRegistry tacticalUnitRegistry;
    // 등록 순서대로 생성한 유닛 카드 View다.
    private readonly List<BattleHudUnitCardView> unitCards = new();

    /// <summary>
    /// 카드 패널을 HUD 에셋과 플레이어 유닛 목록에 연결한다.
    /// </summary>
    public void Initialize(BattleHudAssetSet assets, TacticalUnitRegistry unitRegistry)
    {
        if (!HasValidReference() || assets == null || unitRegistry == null)
        {
            Debug.LogError($"{nameof(BattleHudUnitCardPanelController)} on {name}의 초기화 참조가 올바르지 않습니다.", this);
            enabled = false;
            return;
        }

        assetSet = assets;
        tacticalUnitRegistry = unitRegistry;
    }

    /// <summary>
    /// 등록된 플레이어 조작 유닛 수만큼 카드 Prefab을 다시 배치한다.
    /// </summary>
    public void Rebuild()
    {
        for (int i = unitCardRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(unitCardRoot.GetChild(i).gameObject);
        }

        unitCards.Clear();
        IReadOnlyList<TacticalUnitContext> units = tacticalUnitRegistry.PlayerControllableUnits;
        for (int i = 0; i < units.Count; i++)
        {
            BattleHudUnitCardView view = Instantiate(unitCardPrefab, unitCardRoot);
            view.name = $"UnitCard_{units[i].name}";
            view.Initialize(units[i], assetSet);
            unitCards.Add(view);
        }
    }

    /// <summary>
    /// 모든 카드에 현재 선택·턴·유닛 상태를 반영한다.
    /// </summary>
    public void Refresh(TacticalUnitContext activeUnit, bool isPlayerTurn)
    {
        for (int i = 0; i < unitCards.Count; i++)
        {
            unitCards[i].Refresh(unitCards[i].Unit == activeUnit, isPlayerTurn);
        }
    }

    /// <summary>
    /// 유닛 카드 패널의 필수 Prefab 참조가 연결되어 있는지 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool valid = unitCardRoot != null && unitCardPrefab != null;
        if (!valid)
        {
            Debug.LogError($"{nameof(BattleHudUnitCardPanelController)} on {name}에는 카드 루트와 Prefab 참조가 필요합니다.", this);
        }

        return valid;
    }
}
