using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>메뉴·수첩·종료 확인과 전투 입력 잠금을 한곳에서 관리한다.</summary>
[DefaultExecutionOrder(-200)]
public sealed class BattleMenuController : MonoBehaviour
{
    // 같은 루트에 명시적으로 연결한 화면 참조다.
    [SerializeField] private BattleMenuContext context;
    // 창·책을 펼치거나 덮을 때의 조작 효과음이다. 정지 중에도 재생하도록 데이터를 지정한다.
    [SerializeField] private SoundData menuOpenSound, menuCloseSound, manualOpenSound, manualCloseSound;
    // 이 메뉴 담당이 재생한 조작 소리의 중단 권한이다.
    private readonly PresentationAudioScope sounds = new();
    // 현재 창과 수첩을 덮었을 때 돌아갈 창이다.
    public enum Window { None, Menu, Manual, Quit }
    private Window window, returnWindow;
    // 창을 열기 전 시간 배율과 닫은 프레임의 입력 차단 상태다.
    private float previousTimeScale;
    private int consumedFrame = -1;
    // 튜토리얼에서 직접 연 안내를 닫을 때만 실행할 확인 이벤트다.
    private Action guideConfirmed;
    public static BattleMenuController Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.window != Window.None;
    public static bool BlocksInput => Instance != null && (IsOpen || Instance.consumedFrame == Time.frameCount);
    public Window CurrentWindow => window;
    public bool IsTutorialGuide => guideConfirmed != null;

    /// <summary>모든 버튼을 연결하고 설정은 준비 중 상태로 둔다.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Debug.LogError("전투 메뉴가 중복 연결됐습니다.", this); enabled = false; return; }
        if (!HasValidReference() || !HasValidData()) { enabled = false; return; }
        Instance = this;
        context.settings.interactable = false;
        context.menuOpen.onClick.AddListener(OpenMenu);
        context.manualOpen.onClick.AddListener(OpenManual);
        context.menuClose.onClick.AddListener(Close);
        context.menuManual.onClick.AddListener(OpenManual);
        context.menuQuit.onClick.AddListener(OpenQuit);
        context.quitClose.onClick.AddListener(CancelQuit);
        context.quitCancel.onClick.AddListener(CancelQuit);
        context.quitAccept.onClick.AddListener(QuitGame);
        context.manualClose.onClick.AddListener(Close);
        context.previous.onClick.AddListener(() => context.manualView.TurnTo(context.manualView.Page - 1));
        context.next.onClick.AddListener(() => context.manualView.TurnTo(context.manualView.Page + 1));
        int[] indices = {0, 1, 2, 3, 4};
        for (int i = 0; i < indices.Length; i++) { int target = indices[i]; context.tabs[i].onClick.AddListener(() => context.manualView.TurnTo(target)); }
        RefreshWindow();
    }

    /// <summary>Escape는 창 닫기·취소를 우선하고 닫힌 상태에서 메뉴를 연다.</summary>
    private void Update()
    {
        var keys = Keyboard.current;
        if (keys == null) return;
        if (keys.escapeKey.wasPressedThisFrame)
        {
            if (window == Window.None) OpenMenu();
            else if (window == Window.Quit) CancelQuit();
            else Close();
            return;
        }
        if (window == Window.Manual)
        {
            if (keys.leftArrowKey.wasPressedThisFrame) context.manualView.TurnTo(context.manualView.Page - 1);
            if (keys.rightArrowKey.wasPressedThisFrame) context.manualView.TurnTo(context.manualView.Page + 1);
        }
    }

    /// <summary>새 창을 열 때만 기존 시간 배율을 저장하고 게임 진행을 정지한다.</summary>
    private void SetWindow(Window next)
    {
        if (window == next) return;
        // 클릭 소리와 창 전환 소리는 별개이며 실제 전환이 승인됐을 때만 재생한다.
        SoundData sound = next == Window.Manual ? manualOpenSound : window == Window.Manual ? manualCloseSound :
            next == Window.None ? menuCloseSound : menuOpenSound;
        sounds.Play(context.presentation, sound, Vector3.zero);
        if (window == Window.None && next != Window.None) { previousTimeScale = Time.timeScale; Time.timeScale = 0; }
        if (window != Window.None && next == Window.None) { context.manualView.ResetTurn(); Time.timeScale = previousTimeScale; }
        consumedFrame = Time.frameCount;
        window = next;
        RefreshWindow();
    }

    /// <summary>창과 HUD 입력 상태를 맞추고 종료 확인에서는 취소에 기본 초점을 둔다.</summary>
    private void RefreshWindow()
    {
        context.overlay.SetActive(window != Window.None);
        context.menu.SetActive(window == Window.Menu);
        context.manual.SetActive(window == Window.Manual);
        context.quit.SetActive(window == Window.Quit);
        context.menuOpen.interactable = context.manualOpen.interactable = window == Window.None;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(window == Window.Quit ? context.quitCancel.gameObject : null);
    }

    /// <summary>HUD 또는 Escape에서 메뉴를 연다.</summary>
    public void OpenMenu() { if (window == Window.None) SetWindow(Window.Menu); }

    /// <summary>마지막 읽은 페이지를 다시 열며 메뉴에서 왔다면 덮을 때 메뉴로 복귀한다.</summary>
    public void OpenManual()
    {
        if (window != Window.None && window != Window.Menu) return;
        returnWindow = window;
        context.manualView.Open(context.manualView.Page, "색인 또는 좌우 버튼으로 내용을 확인하세요.");
        SetWindow(Window.Manual);
    }

    /// <summary>진행 단계의 수첩을 열고 덮을 때 한 번만 다음 단계에 확인을 전달한다.</summary>
    public void OpenGuide(int page, string text, Action confirmed)
    {
        if (window != Window.None || confirmed == null) { Debug.LogError("수첩 안내를 열 수 없는 상태입니다.", this); return; }
        guideConfirmed = confirmed;
        returnWindow = Window.None;
        context.manualView.Open(page, text);
        SetWindow(Window.Manual);
    }

    /// <summary>종료 버튼에서는 바로 종료하지 않고 확인창을 표시한다.</summary>
    public void OpenQuit() { if (window == Window.Menu) SetWindow(Window.Quit); }

    /// <summary>종료 확인을 취소하고 메뉴로 돌아간다.</summary>
    public void CancelQuit() { if (window == Window.Quit) SetWindow(Window.Menu); }

    /// <summary>수첩을 덮거나 메뉴를 닫으며 확인 대기만 정확히 해제한다.</summary>
    public void Close()
    {
        if (window == Window.None) return;
        // 넘김 중에는 덮기나 Escape로 튜토리얼 확인이 소비되지 않게 한다.
        if (window == Window.Manual && context.manualView.IsTurning) return;
        if (window == Window.Quit) { CancelQuit(); return; }
        var callback = window == Window.Manual ? guideConfirmed : null;
        guideConfirmed = null;
        SetWindow(window == Window.Manual ? returnWindow : Window.None);
        callback?.Invoke();
    }

    /// <summary>확인창의 종료만 실제 종료하며 편집기에서는 재생을 중단한다.</summary>
    public void QuitGame()
    {
        if (window != Window.Quit) return;
        guideConfirmed = null;
        SetWindow(Window.None);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>씬을 나갈 때 정지 배율과 전역 참조를 남기지 않는다.</summary>
    private void OnDestroy()
    {
        sounds.StopAll();
        if (Instance != this) return;
        if (window != Window.None) Time.timeScale = previousTimeScale;
        Instance = null;
    }

    /// <summary>필수 화면과 모든 버튼이 연결돼 있는지 검사한다.</summary>
    public bool HasValidReference()
    {
        if (context == null) { Debug.LogError("전투 메뉴 Context가 필요합니다.", this); return false; }
        if (context.presentation == null || context.presentation.Audio == null)
        { Debug.LogError("전투 메뉴에는 오디오 담당 Context가 필요합니다.", this); return false; }
        UnityEngine.Object[] refs = {context.overlay, context.menu, context.manual, context.quit, context.menuOpen, context.manualOpen,
            context.menuClose, context.settings, context.menuManual, context.menuQuit, context.quitClose, context.quitCancel, context.quitAccept,
            context.manualClose, context.previous, context.next, context.manualView};
        bool valid = context.tabs?.Length == 5;
        foreach (var item in refs) valid &= item != null;
        if (valid) foreach (var tab in context.tabs) valid &= tab is BattleUiButton;
        if (!valid) Debug.LogError("전투 메뉴의 창·버튼 참조가 누락됐습니다.", this);
        return valid;
    }

    /// <summary>시간 정지 중에도 들려야 하는 창 전환 데이터의 연결과 재생 정책을 검사한다.</summary>
    public bool HasValidData()
    {
        if (!HasValidReference()) return false;
        foreach (var data in new[] {menuOpenSound, menuCloseSound, manualOpenSound, manualCloseSound})
            if (!context.presentation.Audio.HasValidData(data) || !data.PlayDuringPause)
            { Debug.LogError("메뉴 효과음은 유효하며 정지 중 재생이 켜져 있어야 합니다.", this); return false; }
        return true;
    }
}
