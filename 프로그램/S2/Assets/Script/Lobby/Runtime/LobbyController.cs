using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캠페인 데이터와 저장 상태를 읽어 Lobby 목록·선택·상세 표시를 관리한다.
/// </summary>
public class LobbyController : MonoBehaviour
{
    [Header("Reference")]
    // Lobby 씬의 목록과 상세 View 참조를 제공하는 Context다.
    [SerializeField] private LobbyContext context;

    // 영속 AppRoot에서 제공받은 캠페인 데이터·저장·흐름 참조 주머니다.
    private CampaignContext campaignContext;
    // 현재 Lobby 상세 화면에서 선택된 스테이지 항목이다.
    private LobbyStageEntry selectedEntry;
    // 현재 Lobby 화면 구성이 정상적으로 끝났는지 나타낸다.
    private bool isInitialized;

    public LobbyStageEntry SelectedEntry => selectedEntry;
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// 로비 화면의 필수 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 영속 캠페인 시스템을 확인하고 현재 저장 상태로 스테이지 목록을 구성한다.
    /// </summary>
    private void Start()
    {
        if (!Initialize())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 캠페인 데이터와 저장 상태를 읽어 Lobby 화면을 처음 구성한다.
    /// </summary>
    public bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!HasValidReference())
        {
            return false;
        }

        CampaignBootstrap bootstrap = CampaignBootstrap.Instance;
        if (bootstrap == null)
        {
            Debug.LogError(
                $"{nameof(LobbyController)}: 영속 {nameof(CampaignBootstrap)}이 없습니다. 게임은 BootstrapTest 씬부터 실행해야 합니다.",
                this);
            return false;
        }

        campaignContext = bootstrap.Context;
        if (campaignContext == null || !campaignContext.HasValidReference())
        {
            Debug.LogError($"{nameof(LobbyController)}: 영속 캠페인 Context가 올바르게 준비되지 않았습니다.", this);
            return false;
        }

        if (!campaignContext.SaveManager.IsInitialized)
        {
            Debug.LogError($"{nameof(LobbyController)}: 캠페인 저장 시스템 초기화가 끝나지 않았습니다.", this);
            return false;
        }

        if (!RebuildStageEntries())
        {
            return false;
        }

        isInitialized = true;
        return true;
    }

    /// <summary>
    /// 현재 CampaignData 순서와 저장 상태로 Lobby 목록을 다시 만든다.
    /// </summary>
    public bool Refresh()
    {
        if (!isInitialized)
        {
            Debug.LogError($"{nameof(LobbyController)}가 초기화되지 않아 Lobby 목록을 갱신할 수 없습니다.", this);
            return false;
        }

        return RebuildStageEntries();
    }

    /// <summary>
    /// 현재 캠페인 스테이지를 표시 항목으로 변환하고 첫 선택 가능 항목을 상세 화면에 표시한다.
    /// </summary>
    private bool RebuildStageEntries()
    {
        StageDefinitionData[] stages = campaignContext.CampaignData.Stages;
        if (stages == null || stages.Length == 0)
        {
            Debug.LogError($"{nameof(LobbyController)}: Lobby에 표시할 캠페인 스테이지가 없습니다.", this);
            return false;
        }

        List<LobbyStageEntry> entries = new(stages.Length);
        LobbyStageEntry firstSelectableEntry = null;

        for (int i = 0; i < stages.Length; i++)
        {
            StageDefinitionData stage = stages[i];
            if (stage == null)
            {
                Debug.LogError($"{nameof(LobbyController)}: 캠페인 데이터의 {i}번 스테이지 참조가 비어 있습니다.", this);
                return false;
            }

            if (!campaignContext.SaveManager.TryGetStageProgress(stage.StageId, out StageProgressState progressState))
            {
                return false;
            }

            LobbyStageEntry entry = new(stage, progressState);
            entries.Add(entry);
            if (firstSelectableEntry == null && progressState != StageProgressState.Locked)
            {
                firstSelectableEntry = entry;
            }
        }

        if (!context.StageListView.Rebuild(entries, HandleStageSelected))
        {
            return false;
        }

        selectedEntry = null;
        if (firstSelectableEntry != null)
        {
            HandleStageSelected(firstSelectableEntry);
        }
        else
        {
            context.StageDetailView.ClearSelection();
        }

        return true;
    }

    /// <summary>
    /// 목록에서 고른 선택 가능 스테이지를 현재 상세 표시 대상으로 바꾼다.
    /// </summary>
    private void HandleStageSelected(LobbyStageEntry entry)
    {
        if (entry?.Stage == null || entry.ProgressState == StageProgressState.Locked)
        {
            return;
        }

        selectedEntry = entry;
        context.StageListView.SetSelected(entry.Stage.StageId);
        context.StageDetailView.Show(entry, HandleStartRequested);
    }

    /// <summary>
    /// 상세 화면의 선택 확정 요청을 캠페인 흐름에 전달한다.
    /// </summary>
    private void HandleStartRequested()
    {
        if (selectedEntry?.Stage == null)
        {
            Debug.LogError($"{nameof(LobbyController)}: 선택을 확정할 스테이지가 없습니다.", this);
            return;
        }

        if (!campaignContext.SaveManager.TryGetStageProgress(
                selectedEntry.Stage.StageId,
                out StageProgressState currentProgressState))
        {
            return;
        }

        if (currentProgressState == StageProgressState.Locked)
        {
            Debug.LogWarning(
                $"{nameof(LobbyController)}: 스테이지 '{selectedEntry.Stage.DisplayName}'이 잠겨 선택 요청을 취소합니다.",
                this);
            Refresh();
            return;
        }

        if (!campaignContext.FlowController.TrySelectStage(selectedEntry.Stage.StageId))
        {
            context.StageDetailView.ShowRequestResult("스테이지 선택 요청에 실패했습니다.", false);
            return;
        }

        context.StageDetailView.ShowRequestResult($"'{selectedEntry.Stage.DisplayName}' 진입 중…", true);
        if (!campaignContext.FlowController.TryStartSelectedStage())
        {
            context.StageDetailView.ShowRequestResult("스테이지 진입 요청에 실패했습니다.", false);
        }
    }

    /// <summary>
    /// Lobby Controller에 필요한 Context와 View 내부 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(LobbyController)} on {name}에는 {nameof(LobbyContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference() &&
            context.StageListView.HasValidReference() &&
            context.StageDetailView.HasValidReference();
    }
}
