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

    public CampaignFlowPhase CurrentPhase => currentPhase;
    public bool IsInitialized => isInitialized;

    // 캠페인 흐름 단계가 바뀔 때 이전 단계와 새 단계를 전달한다.
    public event Action<CampaignFlowPhase, CampaignFlowPhase> PhaseChanged;

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
