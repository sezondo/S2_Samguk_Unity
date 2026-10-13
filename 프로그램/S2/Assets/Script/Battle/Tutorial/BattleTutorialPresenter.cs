using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>수첩 안내·대사 입력·목표 칸 강조를 담당한다. 진행 판단과 게임 상태는 변경하지 않는다.</summary>
public sealed class BattleTutorialPresenter : MonoBehaviour
{
    // 같은 튜토리얼의 플레이어와 HUD·가림막 참조 주머니다.
    [SerializeField] private BattleTutorialContext context;
    // 화면과 보드 좌표 변환에 필요한 명시적 참조다.
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private GridManager grid;
    // 목표 칸을 둘러싸는 네 변의 화면 표시다.
    [SerializeField] private RectTransform marker;
    [SerializeField] private CanvasGroup markerGroup;
    // 수첩 안내를 표시하는 메뉴 루트의 명시적 참조다.
    [SerializeField] private BattleMenuContext menuContext;
    // 현재 강조할 칸과 확인 입력 활성 상태다.
    private GridPosition target;
    private bool showingMarker, canContinue;
    public event Action ContinueRequested;
    // 요청된 안내 종류와 지금 실제로 노출한 영역을 구분한다.
    public enum FocusKind { None, ActionButton, TargetCell, EndTurnButton }
    private bool guidingAction, guidingEndTurn;
    private BattleHudActionType guidedAction;
    private GridPosition guidedTarget;
    private readonly Vector3[] buttonCorners = new Vector3[4];
    public FocusKind CurrentFocus { get; private set; }


    /// <summary>필수 화면 연결을 검사하고 초기 표시를 숨긴다.</summary>
    private void Awake()
    {
        if (!HasValidReference()) { enabled = false; return; }
        HideAll();
    }

    /// <summary>별도 진행 버튼 없이 화면 클릭 또는 Enter·Space로 대사를 넘긴다.</summary>
    private void Update()
    {
        if (BattleMenuController.BlocksInput || !canContinue) return;
        bool keyboard = Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        bool pointer = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUi();
        if (keyboard || pointer) RequestContinue();
    }

    /// <summary>메뉴·수첩 등 UI를 누르는 입력이 대사 진행으로 중복 사용되지 않게 한다.</summary>
    private static bool IsPointerOverUi()
    {
        if (EventSystem.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        foreach (var hit in hits) if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    /// <summary>카메라 이동과 화면 크기에 맞춰 한 칸 크기의 강조를 갱신한다.</summary>
    private void LateUpdate()
    {
        RefreshGuidance();
        if (!showingMarker) return;
        var point = grid.GridToWorld(target);
        Vector2 screen = worldCamera.WorldToScreenPoint(point);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out var local);
        marker.anchoredPosition = local;
        float scale = canvas.scaleFactor;
        float size = Mathf.Abs(worldCamera.WorldToScreenPoint(point + Vector3.right * grid.CellSize).x - screen.x) / scale;
        marker.sizeDelta = new Vector2(size, size);
        markerGroup.alpha = 0.65f + Mathf.Sin(Time.unscaledTime * 4) * 0.25f;
    }

    /// <summary>말풍선이 재생되는 단계에서만 대사 진행 입력을 활성화한다.</summary>
    public void EnableDialogueInput() { canContinue = true; }

    /// <summary>기존 큰 안내 팝업 대신 해당 수첩 페이지를 펼쳐 확인을 기다린다.</summary>
    public void ShowGuide(int page, string text)
    {
        DisableDialogueInput();
        menuContext.controller.OpenGuide(page, text, () => ContinueRequested?.Invoke());
    }

    /// <summary>대사가 끝나면 클릭과 키보드 대사 진행 입력을 차단한다.</summary>
    public void DisableDialogueInput() { canContinue = false; }

    /// <summary>지정한 한 칸만 강조한다.</summary>
    public void ShowMarker(GridPosition position) { target = position; showingMarker = true; marker.gameObject.SetActive(true); }

    /// <summary>이전 단계의 목표 강조를 제거한다.</summary>
    public void HideMarker() { showingMarker = false; marker.gameObject.SetActive(false); }

    /// <summary>스테이지 종료와 초기화 시 모든 안내를 감춘다.</summary>
    public void HideAll() { DisableDialogueInput(); HideGuidance(); }

    /// <summary>행동 선택 전에는 버튼만, 선택 후에는 지정 목표 칸만 노출한다.</summary>
    public void ShowActionGuidance(BattleHudActionType action, GridPosition position)
    {
        guidingAction = true; guidingEndTurn = false; guidedAction = action; guidedTarget = position;
        RefreshGuidance();
    }

    /// <summary>수동 턴 종료 단계에서 턴 종료 버튼만 노출한다.</summary>
    public void ShowEndTurnGuidance()
    {
        guidingAction = false; guidingEndTurn = true; RefreshGuidance();
    }

    /// <summary>실행·대사·연출로 넘어갈 때 가림막과 목표 강조를 함께 해제한다.</summary>
    public void HideGuidance()
    {
        guidingAction = guidingEndTurn = false; CurrentFocus = FocusKind.None;
        if (context != null && context.spotlight != null) context.spotlight.Hide();
        if (marker != null) HideMarker();
    }

    /// <summary>행동 선택·취소와 메뉴 상태에 맞춰 구멍을 버튼 또는 월드 칸으로 옮긴다.</summary>
    private void RefreshGuidance()
    {
        if (BattleMenuController.IsOpen || !guidingAction && !guidingEndTurn)
        { CurrentFocus = FocusKind.None; context.spotlight.Hide(); return; }
        HideMarker();
        if (guidingEndTurn)
        { CurrentFocus = FocusKind.EndTurnButton; RevealButton((RectTransform)context.endTurnButton.transform); return; }
        if (IsGuidedActionSelected())
        {
            CurrentFocus = FocusKind.TargetCell;
            Vector3 point = grid.GridToWorld(guidedTarget);
            Vector3 half = new Vector3(grid.CellSize * .5f, grid.CellSize * .5f, 0);
            Vector2 min = ToSpotlightLocal(worldCamera.WorldToScreenPoint(point - half));
            Vector2 max = ToSpotlightLocal(worldCamera.WorldToScreenPoint(point + half));
            context.spotlight.ShowAperture(Rect.MinMaxRect(min.x - 6, min.y - 6, max.x + 6, max.y + 6));
            // 구멍 테두리가 목표를 강조하므로 기존 칸 테두리를 중복 표시하지 않는다.
        }
        else
        {
            if (!context.selectedHud.TryGetActionButtonRect(guidedAction, out var rect))
            {
                Debug.LogError("튜토리얼 가림막에 현재 행동 버튼 참조가 없습니다.", this);
                HideGuidance(); enabled = false; return;
            }
            CurrentFocus = FocusKind.ActionButton; RevealButton(rect);
        }
    }

    /// <summary>기존 행동 컴포넌트의 실제 선택 상태를 읽어 안내 단계를 결정한다.</summary>
    private bool IsGuidedActionSelected()
    {
        var unit = context.player;
        return guidedAction switch
        {
            BattleHudActionType.Move => unit.GridMoveAction.IsMoveSelected,
            BattleHudActionType.Gun => unit.GunAttackAction.IsGunAttackSelected,
            BattleHudActionType.Melee => unit.MeleeAttackAction.IsMeleeAttackSelected,
            BattleHudActionType.SwordThrow => unit.SwordThrowAction.IsSwordThrowSelected,
            BattleHudActionType.Hack => unit.HackAction.IsHackSelected,
            _ => false // 검 회수는 버튼 클릭으로 즉시 실행하므로 목표 칸 단계가 없다.
        };
    }

    /// <summary>현재 화면의 버튼 사각형에 여백을 더해 노출한다.</summary>
    private void RevealButton(RectTransform rect)
    {
        rect.GetWorldCorners(buttonCorners);
        Vector2 min = ToSpotlightLocal(RectTransformUtility.WorldToScreenPoint(null, buttonCorners[0]));
        Vector2 max = ToSpotlightLocal(RectTransformUtility.WorldToScreenPoint(null, buttonCorners[2]));
        context.spotlight.ShowAperture(Rect.MinMaxRect(min.x - 8, min.y - 8, max.x + 8, max.y + 8));
    }

    /// <summary>화면 좌표를 가림막 Canvas의 로컬 좌표로 변환한다.</summary>
    private Vector2 ToSpotlightLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(context.spotlight.rectTransform, screen, null, out var local);
        return local;
    }

    /// <summary>활성화된 안내의 확인 입력만 전달한다.</summary>
    private void RequestContinue() { if (!BattleMenuController.BlocksInput && canContinue) ContinueRequested?.Invoke(); }

    /// <summary>필수 UI를 누락 시 임의로 생성하지 않고 오류로 알린다.</summary>
    public bool HasValidReference()
    {
        bool valid = context != null && context.player != null && context.spotlight != null && context.selectedHud != null && context.endTurnButton != null && menuContext != null && menuContext.controller != null && canvas != null && worldCamera != null && grid != null && marker != null && markerGroup != null;
        if (!valid) Debug.LogError("튜토리얼 안내 화면의 필수 참조가 없습니다.", this);
        return valid;
    }
}
