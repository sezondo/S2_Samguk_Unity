using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobby에서 현재 선택한 스테이지의 상태와 다음 진입 정보를 표시한다.
/// </summary>
public class LobbyStageDetailView : MonoBehaviour
{
    [Header("Reference")]
    // 챕터와 스테이지 번호를 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text stageNumberText;
    // 스테이지 표시 이름을 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text displayNameText;
    // 현재 저장 진행 상태를 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text progressStateText;
    // 현재 상태에서 이어질 캠페인 흐름을 설명하는 TMP 텍스트다.
    [SerializeField] private TMP_Text descriptionText;
    // 현재 스테이지 선택을 캠페인 흐름에 확정 요청하는 버튼이다.
    [SerializeField] private Button startButton;
    // 현재 상태에 맞는 선택 확정 버튼 문구를 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text startButtonText;
    // 선택 요청 성공·실패 결과를 표시하는 TMP 텍스트다.
    [SerializeField] private TMP_Text requestResultText;

    // 선택 확정 버튼을 눌렀을 때 Lobby Controller에 전달할 콜백이다.
    private Action startRequested;

    /// <summary>
    /// 필수 상세 UI 참조를 검사하고 선택 확정 버튼 콜백을 연결한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        startButton.onClick.AddListener(HandleStartClicked);
        ClearSelection();
    }

    /// <summary>
    /// 선택 확정 버튼 콜백을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(HandleStartClicked);
        }
    }

    /// <summary>
    /// 현재 선택한 스테이지 정보와 저장 상태를 상세 화면에 표시한다.
    /// </summary>
    public void Show(LobbyStageEntry entry, Action selectedStageStartRequested)
    {
        if (entry?.Stage == null || selectedStageStartRequested == null)
        {
            Debug.LogError($"{nameof(LobbyStageDetailView)} on {name}이 올바르지 않은 상세 항목 또는 요청 콜백을 받았습니다.", this);
            ClearSelection();
            return;
        }

        startRequested = selectedStageStartRequested;
        StageDefinitionData stage = entry.Stage;
        stageNumberText.text = $"CHAPTER {stage.ChapterNumber} · STAGE {stage.StageNumber}";
        displayNameText.text = stage.DisplayName;
        progressStateText.text = GetProgressStateLabel(entry.ProgressState);
        descriptionText.text = GetDescription(entry);
        startButtonText.text = GetStartButtonLabel(entry.ProgressState);
        startButton.interactable = entry.ProgressState != StageProgressState.Locked;
        requestResultText.text = string.Empty;
    }

    /// <summary>
    /// 선택된 스테이지가 없을 때 상세 화면을 기본 안내 상태로 초기화한다.
    /// </summary>
    public void ClearSelection()
    {
        startRequested = null;
        stageNumberText.text = "CAMPAIGN";
        displayNameText.text = "선택 가능한 스테이지가 없습니다.";
        progressStateText.text = string.Empty;
        descriptionText.text = "캠페인 데이터와 저장 상태를 확인하세요.";
        startButtonText.text = "선택 불가";
        startButton.interactable = false;
        requestResultText.text = string.Empty;
    }

    /// <summary>
    /// 캠페인 흐름에 전달한 스테이지 선택 요청 결과를 화면에 표시한다.
    /// </summary>
    public void ShowRequestResult(string message, bool succeeded)
    {
        requestResultText.text = message;
        requestResultText.color = succeeded
            ? new Color(0.45f, 0.95f, 0.72f, 1f)
            : new Color(1f, 0.45f, 0.45f, 1f);
    }

    /// <summary>
    /// 현재 상세 항목의 선택 확정 요청을 Lobby Controller에 전달한다.
    /// </summary>
    private void HandleStartClicked()
    {
        startRequested?.Invoke();
    }

    /// <summary>
    /// 저장 진행 상태를 상세 화면용 한국어 문구로 변환한다.
    /// </summary>
    private static string GetProgressStateLabel(StageProgressState progressState)
    {
        return progressState switch
        {
            StageProgressState.Locked => "잠김",
            StageProgressState.Available => "진행 가능",
            StageProgressState.BattleCleared => "전투 완료 · 후일담 대기",
            StageProgressState.Completed => "완료",
            _ => "알 수 없음",
        };
    }

    /// <summary>
    /// 현재 진행 상태와 후일담 유무에 맞는 다음 흐름 설명을 반환한다.
    /// </summary>
    private static string GetDescription(LobbyStageEntry entry)
    {
        if (entry.ProgressState == StageProgressState.Locked)
        {
            return "앞선 스테이지를 완료하면 개방됩니다.";
        }

        if (entry.ProgressState == StageProgressState.BattleCleared)
        {
            return "전투는 완료되었습니다. 이후 캠페인 연결에서는 남은 후일담부터 재개합니다.";
        }

        if (entry.ProgressState == StageProgressState.Completed)
        {
            return "완료한 스테이지입니다. 진행 상태를 낮추지 않고 다시 선택할 수 있습니다.";
        }

        return entry.Stage.HasPostBattleStory
            ? "전투 전 Story부터 시작하며, 승리 뒤 후일담이 이어지는 스테이지입니다."
            : "전투 전 Story부터 시작하며, 승리하면 바로 완료되는 스테이지입니다.";
    }

    /// <summary>
    /// 현재 저장 진행 상태에 맞는 선택 확정 버튼 문구를 반환한다.
    /// </summary>
    private static string GetStartButtonLabel(StageProgressState progressState)
    {
        return progressState switch
        {
            StageProgressState.Locked => "잠김",
            StageProgressState.Available => "스테이지 선택",
            StageProgressState.BattleCleared => "후일담 선택",
            StageProgressState.Completed => "재플레이 선택",
            _ => "선택 불가",
        };
    }

    /// <summary>
    /// 스테이지 상세 표시에 필요한 TMP 텍스트와 버튼 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid =
            stageNumberText != null &&
            displayNameText != null &&
            progressStateText != null &&
            descriptionText != null &&
            startButton != null &&
            startButtonText != null &&
            requestResultText != null;

        if (isValid)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(LobbyStageDetailView)} on {name}에는 상세 TMP 텍스트와 선택 확정 Button 참조가 모두 필요합니다.",
            this);
        return false;
    }
}
