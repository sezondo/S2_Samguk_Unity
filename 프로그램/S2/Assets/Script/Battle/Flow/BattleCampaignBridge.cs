using System.Collections;
using UnityEngine;

/// <summary>
/// 전투 결과 UI 확정을 영속 캠페인 저장·Story·로비 흐름에 연결한다.
/// </summary>
public class BattleCampaignBridge : MonoBehaviour
{
    [Header("Reference")]
    // 클리어·실패 UI 확정 이벤트를 제공하는 결과 Presenter다.
    [SerializeField] private StageResultPresenter resultPresenter;

    // 현재 연결한 영속 캠페인 흐름이다.
    private CampaignFlowController flowController;

    /// <summary>
    /// 씬 전환이 끝난 뒤 캠페인 전투로 진입한 경우에만 결과 완료 이벤트를 연결한다.
    /// </summary>
    private IEnumerator Start()
    {
        CampaignBootstrap bootstrap = CampaignBootstrap.Instance;
        if (bootstrap == null)
        {
            // BattleTest 씬 단독 실행에서는 결과 UI까지만 확인한다.
            enabled = false;
            yield break;
        }

        if (!HasValidReference())
        {
            enabled = false;
            yield break;
        }

        flowController = bootstrap.Context.FlowController;

        // 새 씬의 Start가 씬 전환 완료 콜백보다 먼저 실행될 수 있으므로 로딩 종료를 먼저 기다린다.
        while (bootstrap.Context.SceneTransitionController.IsLoading)
        {
            yield return null;
        }

        if (flowController.CurrentPhase != CampaignFlowPhase.Battle ||
            flowController.ActiveStage == null)
        {
            Debug.LogError(
                $"{nameof(BattleCampaignBridge)}: 캠페인 전투 요청 상태가 올바르지 않습니다. " +
                $"Phase={flowController.CurrentPhase}, " +
                $"Stage={flowController.ActiveStage}",
                this);
            enabled = false;
            yield break;
        }

        resultPresenter.ResultConfirmed += HandleResultConfirmed;
    }

    /// <summary>
    /// 전투 결과 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (resultPresenter != null)
        {
            resultPresenter.ResultConfirmed -= HandleResultConfirmed;
        }
    }

    /// <summary>
    /// 결과 UI에서 확정한 승패를 캠페인 흐름에 한 번 전달한다.
    /// </summary>
    private void HandleResultConfirmed(bool cleared)
    {
        resultPresenter.ResultConfirmed -= HandleResultConfirmed;
        if (!flowController.TryCompleteBattle(cleared))
        {
            Debug.LogError($"{nameof(BattleCampaignBridge)}: 전투 결과 뒤 캠페인 다음 단계 진입에 실패했습니다.", this);
        }
    }

    /// <summary>
    /// 전투 캠페인 연결에 필요한 결과 Presenter 참조가 준비되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (resultPresenter == null)
        {
            Debug.LogError($"{nameof(BattleCampaignBridge)} on {name}에는 {nameof(StageResultPresenter)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
