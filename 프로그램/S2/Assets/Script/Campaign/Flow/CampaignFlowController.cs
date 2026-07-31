using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 캠페인의 현재 단계와 공통 씬 진입 순서를 관리한다.
/// </summary>
public class CampaignFlowController : MonoBehaviour
{
    [Header("Reference")]
    // 캠페인 데이터, 저장과 씬 전환 기능을 제공하는 영속 Context다.
    [SerializeField] private CampaignContext context;

    [Header("State")]
    // 현재 캠페인 흐름이 머무는 런타임 단계다.
    [SerializeField] private CampaignFlowPhase currentPhase = CampaignFlowPhase.Bootstrapping;

    // 캠페인 흐름 초기화가 정상적으로 끝났는지 나타낸다.
    private bool isInitialized;
    // 로비에서 선택되어 이후 Story·Battle 흐름이 사용할 현재 스테이지다.
    private StageDefinitionData activeStage;
    // 공통 Story 씬에서 현재 재생해야 할 Story 데이터다.
    private StorySequenceData activeStorySequence;
    // 현재 Story가 전투 전 이야기인지 전투 후 후일담인지 나타낸다.
    private CampaignStoryPurpose activeStoryPurpose;

    public CampaignFlowPhase CurrentPhase => currentPhase;
    public bool IsInitialized => isInitialized;
    public StageDefinitionData ActiveStage => activeStage;
    public StorySequenceData ActiveStorySequence => activeStorySequence;
    public CampaignStoryPurpose ActiveStoryPurpose => activeStoryPurpose;

    // 캠페인 흐름 단계가 바뀔 때 이전 단계와 새 단계를 전달한다.
    public event Action<CampaignFlowPhase, CampaignFlowPhase> PhaseChanged;
    // 로비에서 선택한 활성 스테이지가 바뀔 때 이전 값과 새 값을 전달한다.
    public event Action<StageDefinitionData, StageDefinitionData> ActiveStageChanged;

    /// <summary>
    /// 저장 시스템 상태를 확인하고 최초 로비 진입을 시작한다.
    /// </summary>
    public bool Initialize()
    {
        if (isInitialized)
        {
            return true;
        }

        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return false;
        }

        if (!context.SaveManager.IsInitialized)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 캠페인 저장 시스템 초기화가 끝나지 않았습니다.", this);
            enabled = false;
            return false;
        }

        isInitialized = true;
        return OpenLobby();
    }

    /// <summary>
    /// 공통 로비 씬으로 비동기 전환을 요청한다.
    /// </summary>
    public bool OpenLobby()
    {
        if (!isInitialized)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}가 초기화되지 않아 로비를 열 수 없습니다.", this);
            return false;
        }

        string lobbySceneName = context.CampaignData.LobbySceneName;
        if (SceneManager.GetActiveScene().name == lobbySceneName)
        {
            SetPhase(CampaignFlowPhase.Lobby);
            return true;
        }

        SetPhase(CampaignFlowPhase.LoadingLobby);
        if (context.SceneTransitionController.TryLoadSceneAsync(
                lobbySceneName,
                () => SetPhase(CampaignFlowPhase.Lobby)))
        {
            return true;
        }

        SetPhase(CampaignFlowPhase.Bootstrapping);
        return false;
    }

    /// <summary>
    /// 로비에서 선택 가능한 스테이지를 이후 캠페인 흐름의 활성 스테이지로 지정한다.
    /// </summary>
    public bool TrySelectStage(string stageId)
    {
        if (!isInitialized)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}가 초기화되지 않아 스테이지를 선택할 수 없습니다.", this);
            return false;
        }

        if (currentPhase != CampaignFlowPhase.Lobby)
        {
            Debug.LogWarning(
                $"{nameof(CampaignFlowController)}: 현재 단계가 {currentPhase}라 로비 스테이지를 선택할 수 없습니다.",
                this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(stageId))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}가 비어 있는 스테이지 ID를 받았습니다.", this);
            return false;
        }

        if (!context.CampaignData.TryGetStage(stageId, out StageDefinitionData stage))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 캠페인 데이터에 스테이지 ID '{stageId}'가 없습니다.", this);
            return false;
        }

        if (!context.SaveManager.TryGetStageProgress(stageId, out StageProgressState progressState))
        {
            return false;
        }

        if (progressState == StageProgressState.Locked)
        {
            Debug.LogWarning($"{nameof(CampaignFlowController)}: 잠긴 스테이지 '{stage.DisplayName}'은 선택할 수 없습니다.", this);
            return false;
        }

        if (activeStage == stage)
        {
            return true;
        }

        SetActiveStage(stage);
        return true;
    }

    /// <summary>
    /// 로비에서 선택한 스테이지의 저장 상태에 맞춰 전투 전 Story 또는 전투 진입을 시작한다.
    /// </summary>
    public bool TryStartSelectedStage()
    {
        if (!CanUseActiveStage(CampaignFlowPhase.Lobby, "스테이지를 시작"))
        {
            return false;
        }

        if (!context.SaveManager.TryGetStageProgress(activeStage.StageId, out StageProgressState progressState))
        {
            return false;
        }

        if (progressState == StageProgressState.Locked)
        {
            Debug.LogWarning($"{nameof(CampaignFlowController)}: 잠긴 스테이지 '{activeStage.DisplayName}'은 시작할 수 없습니다.", this);
            return false;
        }

        // 전투까지 끝내고 후일담만 남은 저장은 전투를 반복하지 않고 후일담부터 복구한다.
        if (progressState == StageProgressState.BattleCleared)
        {
            return activeStage.HasPostBattleStory
                ? OpenStory(activeStage.PostBattleStory, CampaignStoryPurpose.PostBattle)
                : CompleteStageAndReturnLobby();
        }

        return activeStage.PreBattleStory != null
            ? OpenStory(activeStage.PreBattleStory, CampaignStoryPurpose.PreBattle)
            : OpenBattle();
    }

    /// <summary>
    /// Story 씬의 현재 시퀀스 완료를 확인하고 다음 전투 또는 로비 흐름으로 진행한다.
    /// </summary>
    public bool TryCompleteActiveStory(StorySequenceData completedSequence)
    {
        if (!CanUseActiveStage(CampaignFlowPhase.Story, "Story 완료를 처리"))
        {
            return false;
        }

        if (activeStorySequence == null || completedSequence != activeStorySequence)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 현재 캠페인이 요청한 Story와 완료된 Story가 일치하지 않습니다.", this);
            return false;
        }

        CampaignStoryPurpose completedPurpose = activeStoryPurpose;
        ClearActiveStory();
        return completedPurpose == CampaignStoryPurpose.PreBattle
            ? OpenBattle()
            : CompleteStageAndReturnLobby();
    }

    /// <summary>
    /// 전투 결과 UI가 확정한 승패를 저장 흐름에 반영하고 후일담 또는 로비로 이동한다.
    /// </summary>
    public bool TryCompleteBattle(bool cleared)
    {
        if (!CanUseActiveStage(CampaignFlowPhase.Battle, "전투 결과를 처리"))
        {
            return false;
        }

        if (!cleared)
        {
            return ReturnToLobby();
        }

        if (!context.SaveManager.TryMarkBattleCleared(activeStage.StageId))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 전투 승리 상태 저장에 실패해 다음 흐름을 중단합니다.", this);
            return false;
        }

        if (activeStage.HasPostBattleStory)
        {
            return OpenStory(activeStage.PostBattleStory, CampaignStoryPurpose.PostBattle);
        }

        return CompleteStageAndReturnLobby();
    }

    /// <summary>
    /// 지정한 Story를 활성화하고 공통 Story 씬으로 전환한다.
    /// </summary>
    private bool OpenStory(StorySequenceData sequence, CampaignStoryPurpose purpose)
    {
        if (sequence == null || purpose == CampaignStoryPurpose.None)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: Story 데이터 또는 재생 목적이 비어 있어 Story 씬을 열 수 없습니다.", activeStage);
            return false;
        }

        activeStorySequence = sequence;
        activeStoryPurpose = purpose;
        SetPhase(CampaignFlowPhase.LoadingStory);
        if (context.SceneTransitionController.TryLoadSceneAsync(
                context.CampaignData.StorySceneName,
                () => SetPhase(CampaignFlowPhase.Story)))
        {
            return true;
        }

        ClearActiveStory();
        SetPhase(CampaignFlowPhase.Lobby);
        return false;
    }

    /// <summary>
    /// 현재 활성 스테이지의 전투 씬으로 전환한다.
    /// </summary>
    private bool OpenBattle()
    {
        SetPhase(CampaignFlowPhase.LoadingBattle);
        if (context.SceneTransitionController.TryLoadSceneAsync(
                activeStage.BattleSceneName,
                () => SetPhase(CampaignFlowPhase.Battle)))
        {
            return true;
        }

        SetPhase(CampaignFlowPhase.Lobby);
        return false;
    }

    /// <summary>
    /// 활성 스테이지를 최종 완료 상태로 저장하고 로비로 돌아간다.
    /// </summary>
    private bool CompleteStageAndReturnLobby()
    {
        if (!context.SaveManager.TryCompleteStage(activeStage.StageId))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 스테이지 완료 저장에 실패해 로비 복귀를 중단합니다.", this);
            return false;
        }

        return ReturnToLobby();
    }

    /// <summary>
    /// 현재 스테이지와 Story 런타임 상태를 정리하면서 로비 씬으로 돌아간다.
    /// </summary>
    private bool ReturnToLobby()
    {
        CampaignFlowPhase failurePhase = currentPhase;
        SetPhase(CampaignFlowPhase.LoadingLobby);
        if (!context.SceneTransitionController.TryLoadSceneAsync(
                context.CampaignData.LobbySceneName,
                () =>
                {
                    ClearActiveStory();
                    SetActiveStage(null);
                    SetPhase(CampaignFlowPhase.Lobby);
                }))
        {
            SetPhase(failurePhase);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 요청 단계와 활성 스테이지가 캠페인 작업을 시작할 수 있는지 확인한다.
    /// </summary>
    private bool CanUseActiveStage(CampaignFlowPhase requiredPhase, string actionName)
    {
        if (!isInitialized)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}가 초기화되지 않아 {actionName}할 수 없습니다.", this);
            return false;
        }

        if (currentPhase != requiredPhase)
        {
            Debug.LogWarning($"{nameof(CampaignFlowController)}: 현재 단계가 {currentPhase}라 {actionName}할 수 없습니다.", this);
            return false;
        }

        if (activeStage == null)
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 활성 스테이지가 없어 {actionName}할 수 없습니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 활성 스테이지를 변경하고 구독자에게 이전 값과 새 값을 알린다.
    /// </summary>
    private void SetActiveStage(StageDefinitionData nextStage)
    {
        if (activeStage == nextStage)
        {
            return;
        }

        StageDefinitionData previousStage = activeStage;
        activeStage = nextStage;
        ActiveStageChanged?.Invoke(previousStage, activeStage);
    }

    /// <summary>
    /// 현재 Story 요청 데이터와 목적을 비운다.
    /// </summary>
    private void ClearActiveStory()
    {
        activeStorySequence = null;
        activeStoryPurpose = CampaignStoryPurpose.None;
    }

    /// <summary>
    /// 현재 캠페인 단계를 바꾸고 변경 이벤트를 알린다.
    /// </summary>
    private void SetPhase(CampaignFlowPhase nextPhase)
    {
        if (currentPhase == nextPhase)
        {
            return;
        }

        CampaignFlowPhase previousPhase = currentPhase;
        currentPhase = nextPhase;
        PhaseChanged?.Invoke(previousPhase, currentPhase);
    }

    /// <summary>
    /// 캠페인 흐름에 필요한 영속 Context 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(CampaignFlowController)} on {name}에는 {nameof(CampaignContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference();
    }

    /// <summary>
    /// 공통 로비·Story 씬이 Build Settings에서 불러올 수 있는지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        string lobbySceneName = context.CampaignData.LobbySceneName;
        if (string.IsNullOrWhiteSpace(lobbySceneName))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: 캠페인 로비 씬 이름이 비어 있습니다.", context.CampaignData);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: Build Settings에서 활성화된 로비 씬 '{lobbySceneName}'을 찾을 수 없습니다.", context.CampaignData);
            return false;
        }

        string storySceneName = context.CampaignData.StorySceneName;
        if (string.IsNullOrWhiteSpace(storySceneName) ||
            !Application.CanStreamedLevelBeLoaded(storySceneName))
        {
            Debug.LogError($"{nameof(CampaignFlowController)}: Build Settings에서 활성화된 Story 씬 '{storySceneName}'을 찾을 수 없습니다.", context.CampaignData);
            return false;
        }

        return true;
    }
}
