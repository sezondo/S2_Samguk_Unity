using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 행동 아이콘과 Hover·Pressed·Selected·Disabled 상태를 한 버튼에 표시한다.
/// </summary>
public sealed class BattleHudActionButtonView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    // 이 버튼이 요청하는 행동 종류다. 캐릭터별 HUD 프리팹에서 지정한다.
    [SerializeField] private BattleHudActionType actionType;
    // 클릭 입력을 받는 실제 UGUI 버튼이다.
    [SerializeField] private Button button;
    // 상태별 Sprite를 표시하는 오버레이 이미지다.
    [SerializeField] private Image stateOverlay;
    // 행동 버튼 Hover 상태 Sprite다.
    private Sprite hoverSprite;
    // 행동 버튼 Pressed 상태 Sprite다.
    private Sprite pressedSprite;
    // 행동 버튼 Selected 상태 Sprite다.
    private Sprite selectedSprite;
    // 행동 버튼 Disabled 상태 Sprite다.
    private Sprite disabledSprite;
    // 행동 버튼 Warning 상태 Sprite다.
    private Sprite warningSprite;
    // 현재 행동 선택 여부다.
    private bool selected;
    // 현재 행동 경고 여부다.
    private bool warning;
    // 포인터가 버튼 위에 있는지 나타낸다.
    private bool hovered;
    // 포인터가 버튼을 누르고 있는지 나타낸다.
    private bool pressed;
    // 포인터 진입·이탈을 행동 정보 패널에 전달한다.
    private Action<BattleHudActionType, bool> hoverChanged;

    public BattleHudActionType ActionType => actionType;

    /// <summary>
    /// 프리팹에 직렬화된 행동 종류와 UI 참조에 런타임 요청 함수를 연결한다.
    /// </summary>
    public void Bind(
        BattleHudAssetSet assets,
        Action clicked,
        Action<BattleHudActionType, bool> hoverCallback)
    {
        if (button == null || stateOverlay == null || assets == null)
        {
            Debug.LogError($"{nameof(BattleHudActionButtonView)} on {name}에는 버튼·상태 이미지·HUD 에셋 참조가 필요합니다.", this);
            enabled = false;
            return;
        }

        ApplyRuntimeBinding(assets, clicked, hoverCallback);
    }

    /// <summary>
    /// 런타임에 생성된 버튼의 행동 종류와 UI 참조를 초기화한다.
    /// </summary>
    public void Initialize(
        BattleHudActionType type,
        Button targetButton,
        Image overlay,
        BattleHudAssetSet assets,
        Action clicked,
        Action<BattleHudActionType, bool> hoverCallback)
    {
        actionType = type;
        button = targetButton;
        stateOverlay = overlay;
        ApplyRuntimeBinding(assets, clicked, hoverCallback);
    }

    /// <summary>
    /// 행동 버튼의 상태 Sprite와 런타임 입력 콜백을 적용한다.
    /// </summary>
    private void ApplyRuntimeBinding(
        BattleHudAssetSet assets,
        Action clicked,
        Action<BattleHudActionType, bool> hoverCallback)
    {
        hoverSprite = assets.ActionStateHover;
        pressedSprite = assets.ActionStatePressed;
        selectedSprite = assets.ActionStateSelected;
        disabledSprite = assets.ActionStateDisabled;
        warningSprite = assets.ActionStateWarning;
        hoverChanged = hoverCallback;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => clicked?.Invoke());
        RefreshOverlay();
    }

    /// <summary>
    /// 행동 사용 가능·선택·경고 상태를 버튼 화면에 반영한다.
    /// </summary>
    public void SetState(bool interactable, bool isSelected, bool hasWarning)
    {
        button.interactable = interactable;
        selected = isSelected;
        warning = hasWarning;
        RefreshOverlay();
    }

    /// <summary>
    /// 포인터 진입 상태를 기록하고 행동 설명 표시를 요청한다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        hoverChanged?.Invoke(actionType, true);
        RefreshOverlay();
    }

    /// <summary>
    /// 포인터 이탈 상태를 기록하고 기본 행동 설명 복원을 요청한다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
        hoverChanged?.Invoke(actionType, false);
        RefreshOverlay();
    }

    /// <summary>
    /// 포인터 누름 상태를 표시한다.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        RefreshOverlay();
    }

    /// <summary>
    /// 포인터 누름 상태를 해제한다.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        RefreshOverlay();
    }

    /// <summary>
    /// 현재 우선순위에 맞는 상태 Sprite 하나만 오버레이에 표시한다.
    /// </summary>
    private void RefreshOverlay()
    {
        if (stateOverlay == null || button == null)
        {
            return;
        }

        Sprite nextSprite = null;
        if (!button.interactable)
        {
            nextSprite = disabledSprite;
        }
        else if (selected)
        {
            nextSprite = selectedSprite;
        }
        else if (pressed)
        {
            nextSprite = pressedSprite;
        }
        else if (hovered)
        {
            nextSprite = hoverSprite;
        }
        else if (warning)
        {
            nextSprite = warningSprite;
        }

        stateOverlay.sprite = nextSprite;
        stateOverlay.enabled = nextSprite != null;
    }
}
