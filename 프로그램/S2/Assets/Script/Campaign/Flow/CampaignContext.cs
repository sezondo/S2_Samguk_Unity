using UnityEngine;

/// <summary>
/// 영속 AppRoot에서 캠페인 데이터와 핵심 런타임 컴포넌트 참조를 모아 제공한다.
/// </summary>
public class CampaignContext : MonoBehaviour
{
    [Header("Data")]
    // 선형 스테이지 순서와 공통 씬 정보를 제공하는 캠페인 데이터다.
    [SerializeField] private CampaignData campaignData;

    [Header("Runtime")]
    // 캠페인 단계와 씬 진입 순서를 관리하는 흐름 컨트롤러다.
    [SerializeField] private CampaignFlowController flowController;
    // 로컬 캠페인 진행 파일을 읽고 쓰는 저장 매니저다.
    [SerializeField] private CampaignSaveManager saveManager;
    // 로딩 화면을 유지하며 비동기 씬 전환을 실행하는 컨트롤러다.
    [SerializeField] private SceneTransitionController sceneTransitionController;

    public CampaignData CampaignData => campaignData;
    public CampaignFlowController FlowController => flowController;
    public CampaignSaveManager SaveManager => saveManager;
    public SceneTransitionController SceneTransitionController => sceneTransitionController;

    /// <summary>
    /// 캠페인 실행에 필요한 모든 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (campaignData == null)
        {
            Debug.LogError($"{nameof(CampaignContext)} on {name}에는 {nameof(CampaignData)} 참조가 필요합니다.", this);
            return false;
        }

        if (flowController == null)
        {
            Debug.LogError($"{nameof(CampaignContext)} on {name}에는 {nameof(CampaignFlowController)} 참조가 필요합니다.", this);
            return false;
        }

        if (saveManager == null)
        {
            Debug.LogError($"{nameof(CampaignContext)} on {name}에는 {nameof(CampaignSaveManager)} 참조가 필요합니다.", this);
            return false;
        }

        if (sceneTransitionController == null)
        {
            Debug.LogError($"{nameof(CampaignContext)} on {name}에는 {nameof(SceneTransitionController)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
