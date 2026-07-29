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

    public CampaignFlowPhase CurrentPhase => currentPhase;
    public bool IsInitialized => isInitialized;
    public StageDefinitionData ActiveStage => activeStage;

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

        StageDefinitionData previousStage = activeStage;
        activeStage = stage;
        ActiveStageChanged?.Invoke(previousStage, activeStage);
        return true;
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
    /// 최초 진입 로비 씬이 Build Settings에서 불러올 수 있는지 확인한다.
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

        return true;
    }
}
