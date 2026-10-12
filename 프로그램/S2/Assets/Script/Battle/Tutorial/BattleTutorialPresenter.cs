using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>수첩 안내·대사 입력·목표 칸 강조를 담당한다. 진행 판단과 게임 상태는 변경하지 않는다.</summary>
public sealed class BattleTutorialPresenter : MonoBehaviour
{
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
    public void HideAll() { DisableDialogueInput(); HideMarker(); }

    /// <summary>활성화된 안내의 확인 입력만 전달한다.</summary>
    private void RequestContinue() { if (!BattleMenuController.BlocksInput && canContinue) ContinueRequested?.Invoke(); }

    /// <summary>필수 UI를 누락 시 임의로 생성하지 않고 오류로 알린다.</summary>
    public bool HasValidReference()
    {
        bool valid = menuContext != null && menuContext.controller != null && canvas != null && worldCamera != null && grid != null && marker != null && markerGroup != null;
        if (!valid) Debug.LogError("튜토리얼 안내 화면의 필수 참조가 없습니다.", this);
        return valid;
    }
}
