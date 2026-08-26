using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택·행동 목표 브래킷, 적 상태 아이콘과 총격 명중 미리보기를 월드 위치에 표시한다.
/// </summary>
public sealed class BattleHudTacticalOverlayController : MonoBehaviour
{
    [Header("View")]
    // Screen Space Overlay 좌표 변환 기준 Canvas다.
    [SerializeField] private Canvas hudCanvas;
    // 선택 유닛을 따라가는 브래킷이다.
    [SerializeField] private Image selectedUnitBracket;
    // 현재 포인터 목표를 따라가는 행동 브래킷이다.
    [SerializeField] private Image targetBracket;
    // 명중률과 엄폐 보정을 표시하는 패널이다.
    [SerializeField] private RectTransform hitPreviewRoot;
    // 적 대상 최종 명중률 텍스트다.
    [SerializeField] private TMP_Text hitChanceText;
    // 적 대상 엄폐 보정 텍스트다.
    [SerializeField] private TMP_Text coverModifierText;
    // 적 인식 상태 아이콘을 배치할 루트다.
    [SerializeField] private RectTransform enemyStateIconRoot;
    // 적 수에 따라 생성할 상태 아이콘 Prefab이다.
    [SerializeField] private Image enemyStateIconPrefab;
    // 적 머리 위에 아이콘을 띄울 화면 픽셀 오프셋이다.
    [SerializeField] private Vector2 worldIconScreenOffset = new(0f, 58f);

    // 전술 오버레이 Sprite를 제공하는 HUD 에셋이다.
    private BattleHudAssetSet assetSet;
    // 현재 선택 유닛을 제공한다.
    private PlayerUnitControlManager unitControlManager;
    // 현재 적 목록을 제공한다.
    private EnemyRegistry enemyRegistry;
    // 실제 판정과 같은 명중 미리보기를 제공한다.
    private AttackResolutionCoordinator attackResolutionCoordinator;
    // 월드 위치를 화면 좌표로 변환할 카메라다.
    private Camera worldCamera;
    // 선택 유닛의 현재 행동을 조회하는 함수다.
    private Func<TacticalUnitContext, BattleHudActionType?> getSelectedAction;
    // 적별로 생성해 캐싱한 인식 상태 아이콘이다.
    private readonly Dictionary<EnemyContext, Image> enemyStateIcons = new();

    private RectTransform CanvasRect => (RectTransform)hudCanvas.transform;

    /// <summary>
    /// 전술 오버레이를 전투 상태·카메라와 연결하고 초기 표시를 숨긴다.
    /// </summary>
    public void Initialize(
        BattleHudAssetSet assets,
        PlayerUnitControlManager battleUnitControlManager,
        EnemyRegistry battleEnemyRegistry,
        AttackResolutionCoordinator battleAttackResolutionCoordinator,
        Camera battleWorldCamera,
        Func<TacticalUnitContext, BattleHudActionType?> selectedActionResolver)
    {
        if (!HasValidReference() || assets == null || battleUnitControlManager == null ||
            battleEnemyRegistry == null || battleAttackResolutionCoordinator == null || battleWorldCamera == null ||
            selectedActionResolver == null)
        {
            Debug.LogError($"{nameof(BattleHudTacticalOverlayController)} on {name}의 초기화 참조가 올바르지 않습니다.", this);
            enabled = false;
            return;
        }

        assetSet = assets;
        unitControlManager = battleUnitControlManager;
        enemyRegistry = battleEnemyRegistry;
        attackResolutionCoordinator = battleAttackResolutionCoordinator;
        worldCamera = battleWorldCamera;
        getSelectedAction = selectedActionResolver;
        selectedUnitBracket.enabled = false;
        ClearPointerPreview();
    }

    /// <summary>
    /// 현재 적 수만큼 상태 아이콘 Prefab을 다시 배치한다.
    /// </summary>
    public void RebuildEnemyStateIcons()
    {
        for (int i = enemyStateIconRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(enemyStateIconRoot.GetChild(i).gameObject);
        }

        enemyStateIcons.Clear();
        IReadOnlyList<EnemyContext> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            Image icon = Instantiate(enemyStateIconPrefab, enemyStateIconRoot);
            icon.name = $"EnemyState_{enemy.name}";
            enemyStateIcons.Add(enemy, icon);
        }
    }

    /// <summary>
    /// 선택 유닛·포인터 목표·적 인식 상태와 명중 미리보기를 최신 월드 위치에 표시한다.
    /// </summary>
    public void Refresh()
    {
        TacticalUnitContext activeUnit = unitControlManager.ActiveUnit;
        selectedUnitBracket.enabled = activeUnit != null && activeUnit.IsAlive;
        if (selectedUnitBracket.enabled)
        {
            SetWorldOverlayPosition(selectedUnitBracket.rectTransform, activeUnit.GridActor.transform.position, Vector2.zero);
        }

        foreach (KeyValuePair<EnemyContext, Image> pair in enemyStateIcons)
        {
            EnemyContext enemy = pair.Key;
            Image icon = pair.Value;
            bool visible = enemy != null && enemy.IsAlive &&
                           (PlayerVisionManager.Instance == null || PlayerVisionManager.Instance.IsVisible(enemy.GridActor.GridPosition));
            icon.enabled = visible;
            if (!visible)
            {
                continue;
            }

            icon.sprite = GetEnemyStateSprite(enemy);
            SetWorldOverlayPosition(icon.rectTransform, enemy.GridActor.transform.position, worldIconScreenOffset);
        }

        RefreshPointerTarget(activeUnit);
    }

    /// <summary>
    /// 행동 목표 브래킷과 명중 미리보기 표시를 즉시 숨긴다.
    /// </summary>
    public void ClearPointerPreview()
    {
        targetBracket.enabled = false;
        hitPreviewRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 전술 오버레이 Prefab의 필수 View 참조가 연결되어 있는지 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool valid = hudCanvas != null && selectedUnitBracket != null && targetBracket != null &&
                     hitPreviewRoot != null && hitChanceText != null && coverModifierText != null &&
                     enemyStateIconRoot != null && enemyStateIconPrefab != null;
        if (!valid)
        {
            Debug.LogError($"{nameof(BattleHudTacticalOverlayController)} on {name}에는 전술 오버레이 View 참조가 필요합니다.", this);
        }

        return valid;
    }

    /// <summary>
    /// 현재 선택 행동과 포인터 칸에 맞는 대상 브래킷·명중률 패널을 표시한다.
    /// </summary>
    private void RefreshPointerTarget(TacticalUnitContext activeUnit)
    {
        ClearPointerPreview();
        BattleHudActionType? selectedAction = activeUnit != null ? getSelectedAction(activeUnit) : null;
        if (selectedAction == null || PlayerInputReader.Instance == null ||
            !PlayerInputReader.Instance.TryGetPointerGridPosition(out GridPosition pointerPosition) ||
            GridManager.Instance == null || !GridManager.Instance.IsInside(pointerPosition))
        {
            return;
        }

        targetBracket.sprite = GetTargetBracketSprite(selectedAction.Value);
        targetBracket.enabled = targetBracket.sprite != null;
        SetWorldOverlayPosition(targetBracket.rectTransform, GridManager.Instance.GridToWorld(pointerPosition), Vector2.zero);

        if (selectedAction != BattleHudActionType.Gun ||
            !GridManager.Instance.TryGetActorAt(pointerPosition, out GridActor targetActor) ||
            targetActor == activeUnit.GridActor ||
            targetActor.GetComponent<IDamageable>() == null ||
            activeUnit.GridActor.GridPosition.ManhattanDistanceTo(pointerPosition) > activeUnit.UnitData.GunAttackRange ||
            (PlayerVisionManager.Instance != null && !PlayerVisionManager.Instance.IsVisible(pointerPosition)) ||
            (targetActor.GetComponent<ActorHealth>() is ActorHealth targetHealth && targetHealth.IsDead) ||
            !attackResolutionCoordinator.TryPreviewAttack(
                activeUnit.GridActor.GridPosition,
                pointerPosition,
                activeUnit.UnitData.GunAttackAccuracy,
                out AttackPreviewResult preview))
        {
            return;
        }

        hitChanceText.text = $"{preview.FinalHitChance}%";
        coverModifierText.text = preview.CoverResult.HasCover
            ? $"낮은 엄폐 {preview.CoverResult.HitChanceModifier}%"
            : "엄폐 없음";
        hitPreviewRoot.gameObject.SetActive(true);
        SetWorldOverlayPosition(hitPreviewRoot, targetActor.transform.position, new Vector2(185f, 55f));
    }

    /// <summary>
    /// 적 인식 상태와 조사 단계에 대응하는 상태 아이콘을 반환한다.
    /// </summary>
    private Sprite GetEnemyStateSprite(EnemyContext enemy)
    {
        if (enemy.AlertState.IsAlerted)
        {
            return assetSet.EnemyAlerted;
        }

        if (enemy.AlertState.IsSuspicious)
        {
            return enemy.AlertState.SuspiciousPhase == SuspiciousBehaviorPhase.Searching
                ? assetSet.EnemyInvestigating
                : assetSet.EnemySuspicious;
        }

        return assetSet.EnemyUnaware;
    }

    /// <summary>
    /// 지정한 행동 종류에 대응하는 포인터 목표 브래킷 Sprite를 반환한다.
    /// </summary>
    private Sprite GetTargetBracketSprite(BattleHudActionType actionType)
    {
        return actionType switch
        {
            BattleHudActionType.Move => assetSet.MoveTargetBracket,
            BattleHudActionType.Gun => assetSet.AttackTargetBracket,
            BattleHudActionType.Melee => assetSet.MeleeTargetBracket,
            BattleHudActionType.SwordThrow => assetSet.SwordTargetBracket,
            BattleHudActionType.Hack => assetSet.HackTargetBracket,
            _ => null,
        };
    }

    /// <summary>
    /// 월드 위치를 Screen Space Overlay Canvas 로컬 좌표로 변환해 UI를 배치한다.
    /// </summary>
    private void SetWorldOverlayPosition(RectTransform target, Vector3 worldPosition, Vector2 screenOffset)
    {
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPosition) + screenOffset;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, screenPosition, null, out Vector2 localPosition))
        {
            target.anchoredPosition = localPosition;
        }
    }
}
