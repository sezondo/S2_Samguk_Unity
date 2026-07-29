using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby 목록에서 스테이지 번호·이름·진행 상태와 선택 가능 여부를 표시한다.
/// </summary>
public class LobbyStageButtonView : MonoBehaviour
{
    [Header("Reference")]
    // 스테이지 선택 입력을 받는 버튼이다.
    [SerializeField] private Button button;
    // 진행 상태에 따른 기본 배경색을 표시하는 Image다.
    [SerializeField] private Image backgroundImage;
    // 현재 선택된 항목의 테두리를 표시하는 Image다.
    [SerializeField] private Image selectionBorderImage;
    // 챕터와 스테이지 번호를 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text stageNumberText;
    // 스테이지 표시 이름을 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text displayNameText;
    // 잠김·진행 가능·후일담 대기·완료 상태를 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text progressStateText;
    // 잠긴 스테이지 위에 표시하는 잠금 배지다.
    [SerializeField] private GameObject lockBadge;

    [Header("Color")]
    // 잠긴 스테이지의 배경색이다.
    [SerializeField] private Color lockedColor = new(0.12f, 0.14f, 0.18f, 0.95f);
    // 진행 가능한 스테이지의 배경색이다.
    [SerializeField] private Color availableColor = new(0.12f, 0.27f, 0.38f, 0.95f);
    // 전투를 완료하고 후일담을 기다리는 스테이지의 배경색이다.
    [SerializeField] private Color battleClearedColor = new(0.35f, 0.22f, 0.09f, 0.95f);
    // 최종 완료한 스테이지의 배경색이다.
    [SerializeField] private Color completedColor = new(0.09f, 0.31f, 0.23f, 0.95f);

    // 이 버튼이 현재 표시하는 Lobby 스테이지 항목이다.
    private LobbyStageEntry entry;
    // 버튼 클릭 시 목록 Controller로 전달할 선택 콜백이다.
    private Action<LobbyStageEntry> selected;

    public string StageId => entry?.Stage != null ? entry.Stage.StageId : string.Empty;

    /// <summary>
    /// 버튼 입력 콜백을 연결하기 전에 필수 UI 참조를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        button.onClick.AddListener(HandleClicked);
    }

    /// <summary>
    /// 버튼 입력 콜백을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }
    }

    /// <summary>
    /// 지정한 스테이지 정보와 저장 상태를 버튼 화면과 입력 가능 여부에 반영한다.
    /// </summary>
    public bool Bind(LobbyStageEntry stageEntry, Action<LobbyStageEntry> selectedCallback)
    {
        if (!isActiveAndEnabled || !HasValidReference() || stageEntry?.Stage == null || selectedCallback == null)
        {
            Debug.LogError($"{nameof(LobbyStageButtonView)} on {name}이 올바르지 않은 Lobby 항목 또는 선택 콜백을 받았습니다.", this);
            return false;
        }

        entry = stageEntry;
        selected = selectedCallback;

        StageDefinitionData stage = entry.Stage;
        stageNumberText.text = $"{stage.ChapterNumber}-{stage.StageNumber}";
        displayNameText.text = stage.DisplayName;
        progressStateText.text = GetProgressStateLabel(entry.ProgressState);
        backgroundImage.color = GetProgressStateColor(entry.ProgressState);

        bool isLocked = entry.ProgressState == StageProgressState.Locked;
        button.interactable = !isLocked;
        lockBadge.SetActive(isLocked);
        SetSelected(false);
        return true;
    }

    /// <summary>
    /// 이 버튼이 현재 선택된 스테이지인지 테두리 표시로 나타낸다.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        if (selectionBorderImage != null)
        {
            selectionBorderImage.enabled = isSelected;
        }
    }

    /// <summary>
    /// 현재 버튼 항목을 목록 Controller에 선택 요청으로 전달한다.
    /// </summary>
    private void HandleClicked()
    {
        if (entry?.Stage != null && entry.ProgressState != StageProgressState.Locked)
        {
            selected?.Invoke(entry);
        }
    }

    /// <summary>
    /// 저장 진행 상태를 Lobby 화면용 한국어 문구로 변환한다.
    /// </summary>
    private static string GetProgressStateLabel(StageProgressState progressState)
    {
        return progressState switch
        {
            StageProgressState.Locked => "잠김",
            StageProgressState.Available => "진행 가능",
            StageProgressState.BattleCleared => "후일담 대기",
            StageProgressState.Completed => "완료",
            _ => "알 수 없음",
        };
    }

    /// <summary>
    /// 저장 진행 상태에 대응하는 버튼 배경색을 반환한다.
    /// </summary>
    private Color GetProgressStateColor(StageProgressState progressState)
    {
        return progressState switch
        {
            StageProgressState.Locked => lockedColor,
            StageProgressState.Available => availableColor,
            StageProgressState.BattleCleared => battleClearedColor,
            StageProgressState.Completed => completedColor,
            _ => lockedColor,
        };
    }

    /// <summary>
    /// 스테이지 버튼 표시에 필요한 모든 UI 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid =
            button != null &&
            backgroundImage != null &&
            selectionBorderImage != null &&
            stageNumberText != null &&
            displayNameText != null &&
            progressStateText != null &&
            lockBadge != null;

        if (isValid)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(LobbyStageButtonView)} on {name}에는 Button, 배경·선택 Image, TMP 텍스트와 잠금 배지 참조가 필요합니다.",
            this);
        return false;
    }
}
