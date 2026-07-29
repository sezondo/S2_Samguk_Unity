using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// CampaignData 순서대로 Lobby 스테이지 버튼을 생성하고 현재 선택 표시를 관리한다.
/// </summary>
public class LobbyStageListView : MonoBehaviour
{
    [Header("Reference")]
    // 생성한 스테이지 버튼이 배치될 세로 목록 루트다.
    [SerializeField] private RectTransform contentRoot;
    // 각 스테이지 표시와 선택 입력에 사용할 버튼 프리팹이다.
    [SerializeField] private LobbyStageButtonView stageButtonPrefab;
    // 표시할 스테이지가 없을 때 안내할 TMP 텍스트다.
    [SerializeField] private TMP_Text emptyMessageText;

    // 현재 목록에 생성되어 선택 상태를 관리하는 버튼 View 목록이다.
    private readonly List<LobbyStageButtonView> activeButtons = new();

    /// <summary>
    /// Lobby 목록을 처음 활성화할 때 필수 UI 참조를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 기존 버튼을 정리하고 전달받은 캠페인 순서대로 새 버튼 목록을 만든다.
    /// </summary>
    public bool Rebuild(IReadOnlyList<LobbyStageEntry> entries, Action<LobbyStageEntry> selected)
    {
        if (!HasValidReference())
        {
            return false;
        }

        ClearButtons();
        bool hasEntry = entries != null && entries.Count > 0;
        emptyMessageText.gameObject.SetActive(!hasEntry);
        if (!hasEntry)
        {
            emptyMessageText.text = "표시할 스테이지가 없습니다.";
            return true;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            LobbyStageEntry entry = entries[i];
            if (entry?.Stage == null)
            {
                Debug.LogError($"{nameof(LobbyStageListView)}: {i}번 Lobby 스테이지 항목이 비어 있습니다.", this);
                ClearButtons();
                return false;
            }

            LobbyStageButtonView buttonView = Instantiate(stageButtonPrefab, contentRoot);
            buttonView.name = $"StageButton_{entry.Stage.StageId}";
            buttonView.gameObject.SetActive(true);
            if (!buttonView.Bind(entry, selected))
            {
                Destroy(buttonView.gameObject);
                ClearButtons();
                return false;
            }

            activeButtons.Add(buttonView);
        }

        return true;
    }

    /// <summary>
    /// 지정한 스테이지 ID와 일치하는 버튼 하나만 선택 상태로 표시한다.
    /// </summary>
    public void SetSelected(string stageId)
    {
        for (int i = 0; i < activeButtons.Count; i++)
        {
            LobbyStageButtonView button = activeButtons[i];
            if (button != null)
            {
                button.SetSelected(button.StageId == stageId);
            }
        }
    }

    /// <summary>
    /// 현재 동적으로 생성된 모든 스테이지 버튼을 제거한다.
    /// </summary>
    private void ClearButtons()
    {
        for (int i = 0; i < activeButtons.Count; i++)
        {
            LobbyStageButtonView button = activeButtons[i];
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        activeButtons.Clear();
    }

    /// <summary>
    /// 동적 스테이지 목록 생성에 필요한 루트·프리팹·안내 문구 참조를 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        bool isValid =
            contentRoot != null &&
            stageButtonPrefab != null &&
            emptyMessageText != null;

        if (isValid)
        {
            return true;
        }

        Debug.LogError(
            $"{nameof(LobbyStageListView)} on {name}에는 Content Root, 스테이지 버튼 프리팹과 빈 목록 TMP 참조가 필요합니다.",
            this);
        return false;
    }
}
