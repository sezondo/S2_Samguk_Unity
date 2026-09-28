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
    // 머리 기준점과 상태 아이콘 아래쪽 사이의 화면 픽셀 여백이다.
    [SerializeField] private float enemyIconGapPixels = 8f;

    // 총격 사거리·조준선을 그리는 필수 머티리얼과 런타임 표시기다.
    [SerializeField] private Material gunLineMaterial;
    private GunRangeOverlay gunOverlay;
    // 검 투척·근접·해킹은 같은 선 머티리얼로 별도 행동 범위를 표시한다.
    private ActionRangeOverlay actionOverlay;

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
        gunOverlay?.Dispose();
        gunOverlay = new GunRangeOverlay(gunLineMaterial);
        actionOverlay?.Dispose();
        actionOverlay = new ActionRangeOverlay(gunLineMaterial);
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
        EnsureEnemyStateIcons();
    }

    /// <summary>HUD 초기화 뒤 등록된 적도 아이콘을 한 번만 생성한다. 사망 연출 중인 기존 아이콘은 유지한다.</summary>
    private void EnsureEnemyStateIcons()
    {
        IReadOnlyList<EnemyContext> enemies = enemyRegistry.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyContext enemy = enemies[i];
            if (enemy == null || enemyStateIcons.ContainsKey(enemy)) continue;
            Image icon = Instantiate(enemyStateIconPrefab, enemyStateIconRoot);
            // 첫 시야 갱신 전에는 프리팹의 기본 표시 상태를 노출하지 않는다.
            icon.enabled = false;
            icon.name = $"EnemyState_{enemy.name}";
            enemyStateIcons.Add(enemy, icon);
        }
    }

    /// <summary>
    /// 선택 유닛·포인터 목표·적 인식 상태와 명중 미리보기를 최신 월드 위치에 표시한다.
    /// </summary>
    public void Refresh()
    {
        EnsureEnemyStateIcons();
        TacticalUnitContext activeUnit = unitControlManager.ActiveUnit;
        var registry = ActorPresentationRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError($"{name}: 전술 표시용 ActorPresentationRegistry가 없습니다.", this);
            enabled = false;
            return;
        }
        ActorVisualController selectedVisual = null;
        selectedUnitBracket.enabled = activeUnit != null &&
            registry.TryGetVisual(activeUnit.GridActor, out selectedVisual) && !selectedVisual.IsDeathPresentation;
        if (selectedUnitBracket.enabled)
        {
            SetGridBracket(selectedUnitBracket.rectTransform, selectedVisual.transform.position - selectedVisual.CoverWorldOffset);
        }

        PlayerVisionPresenter visionPresenter = PlayerVisionPresenter.Instance;
        foreach (KeyValuePair<EnemyContext, Image> pair in enemyStateIcons)
        {
            EnemyContext enemy = pair.Key;
            Image icon = pair.Value;
            ActorVisualController enemyVisual = null;
            bool visible = enemy != null && registry.TryGetVisual(enemy.GridActor, out enemyVisual) &&
                !enemyVisual.IsDeathPresentation && enemyVisual.VisionAlpha > 0.01f &&
                visionPresenter != null && visionPresenter.isActiveAndEnabled &&
                visionPresenter.ShouldShowEnemyActorInSnapshot(enemy.GridActor);
            if (visible)
            {
                var state = registry.GetPresentedEnemyState(enemy);
                // 점멸 중에는 수색 눈도 꺼짐 구간을 반영한다. 종료 후 수색 눈은 계속 표시한다.
                visible = registry.IsAlertIconFlashing(enemy.GridActor)
                    ? registry.IsAlertIconVisible(enemy.GridActor)
                    : state.awareness != EnemyAwarenessState.Alerted;
            }
            icon.enabled = visible;
            if (!visible)
            {
                continue;
            }

            icon.sprite = GetEnemyStateSprite(enemy);
            Vector3 headPosition = enemyVisual.HeadWorldPosition;
            float halfIconHeight = icon.rectTransform.rect.height * hudCanvas.scaleFactor * 0.5f;
            SetWorldOverlayPosition(icon.rectTransform, headPosition, new Vector2(0f, halfIconHeight + enemyIconGapPixels));
        }

        GridPosition? gunPointer = null;
        if (PlayerInputReader.Instance != null && PlayerInputReader.Instance.TryGetPointerGridPosition(out GridPosition cell)) gunPointer = cell;
        gunOverlay?.Refresh(activeUnit, enemyRegistry, gunPointer);
        actionOverlay?.Refresh(activeUnit, enemyRegistry, gunPointer);
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
        bool valid = gunLineMaterial != null && hudCanvas != null && selectedUnitBracket != null && targetBracket != null &&
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
        if (ActionPresentationQueue.Instance != null && ActionPresentationQueue.Instance.IsBusy) return;
        BattleHudActionType? selectedAction = activeUnit != null ? getSelectedAction(activeUnit) : null;
        if (selectedAction == null || PlayerInputReader.Instance == null ||
            !PlayerInputReader.Instance.TryGetPointerGridPosition(out GridPosition pointerPosition) ||
            GridManager.Instance == null || !GridManager.Instance.IsInside(pointerPosition))
        {
            return;
        }

        if (selectedAction == BattleHudActionType.Move || selectedAction == BattleHudActionType.Gun ||
            selectedAction == BattleHudActionType.Melee || selectedAction == BattleHudActionType.SwordThrow || selectedAction == BattleHudActionType.Hack)
        {
            // 이동·공격·해킹은 월드 표시와 하단 행동 정보만 사용한다.
            return;
        }

        targetBracket.sprite = GetTargetBracketSprite(selectedAction.Value);
        targetBracket.enabled = targetBracket.sprite != null;
        SetGridBracket(targetBracket.rectTransform, GridManager.Instance.GridToWorld(pointerPosition));

    }

    /// <summary>HUD가 비활성화되면 행동 범위 표시도 숨긴다.</summary>
    private void OnDisable() { gunOverlay?.Hide(); actionOverlay?.Hide(); }

    /// <summary>HUD 제거 시 행동 표시기가 소유한 메시를 해제한다.</summary>
    private void OnDestroy() { gunOverlay?.Dispose(); actionOverlay?.Dispose(); }

    /// <summary>
    /// 적 인식 상태와 조사 단계에 대응하는 상태 아이콘을 반환한다.
    /// </summary>
    private Sprite GetEnemyStateSprite(EnemyContext enemy)
    {
        var state = ActorPresentationRegistry.Instance.GetPresentedEnemyState(enemy);
        if (state.awareness == EnemyAwarenessState.Alerted)
        {
            return assetSet.EnemyAlerted;
        }

        if (state.awareness == EnemyAwarenessState.Suspicious)
        {
            return state.phase == SuspiciousBehaviorPhase.Searching
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

    /// <summary>현재 줌과 Canvas 배율을 반영해 한 칸의 테두리를 감싸도록 브래킷을 배치한다.</summary>
    private void SetGridBracket(RectTransform target, Vector3 center)
    {
        GridManager grid = GridManager.Instance;
        if (grid == null)
        {
            Debug.LogError($"{name}: 선택 표시 크기를 계산할 GridManager가 없습니다.", this);
            enabled = false;
            return;
        }
        float halfCell = grid.CellSize * 0.5f;
        Vector2 lower = worldCamera.WorldToScreenPoint(center - new Vector3(halfCell, halfCell));
        Vector2 upper = worldCamera.WorldToScreenPoint(center + new Vector3(halfCell, halfCell));
        Vector2 size = (upper - lower) / hudCanvas.scaleFactor;
        target.sizeDelta = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
        SetWorldOverlayPosition(target, center, Vector2.zero);
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
