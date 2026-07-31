using System.Collections;
using UnityEngine;

/// <summary>
/// 영속 캠페인 흐름이 요청한 Story를 공통 StoryRunner에 연결하고 완료 결과를 돌려준다.
/// </summary>
public class StoryCampaignBridge : MonoBehaviour
{
    [Header("Reference")]
    // 캠페인 Story 재생을 담당하는 공통 Runner다.
    [SerializeField] private StoryRunner runner;

    // 현재 연결한 영속 캠페인 흐름이다.
    private CampaignFlowController flowController;

    /// <summary>
    /// Story 씬이 활성화되면 캠페인이 요청한 시퀀스를 스킵 가능 상태로 재생한다.
    /// </summary>
    private IEnumerator Start()
    {
        CampaignBootstrap bootstrap = CampaignBootstrap.Instance;
        if (bootstrap == null)
        {
            // StoryTest 씬 단독 실행은 기존 StoryTestLauncher가 담당한다.
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

        if (flowController.CurrentPhase != CampaignFlowPhase.Story ||
            flowController.ActiveStorySequence == null ||
            flowController.ActiveStoryPurpose == CampaignStoryPurpose.None)
        {
            Debug.LogError(
                $"{nameof(StoryCampaignBridge)}: 캠페인 Story 요청 상태가 올바르지 않습니다. " +
                $"Phase={flowController.CurrentPhase}, " +
                $"Sequence={flowController.ActiveStorySequence}, " +
                $"Purpose={flowController.ActiveStoryPurpose}",
                this);
            enabled = false;
            yield break;
        }

        runner.StoryCompleted += HandleStoryCompleted;
        if (!runner.Play(flowController.ActiveStorySequence, true))
        {
            Debug.LogError($"{nameof(StoryCampaignBridge)}: 캠페인 Story 재생을 시작하지 못했습니다.", this);
            runner.StoryCompleted -= HandleStoryCompleted;
            enabled = false;
        }
    }

    /// <summary>
    /// Story 완료 이벤트 구독을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (runner != null)
        {
            runner.StoryCompleted -= HandleStoryCompleted;
        }
    }

    /// <summary>
    /// 정상 완료와 스킵을 같은 캠페인 완료 흐름으로 전달한다.
    /// </summary>
    private void HandleStoryCompleted(StorySequenceData sequence, StoryCompletionReason _)
    {
        runner.StoryCompleted -= HandleStoryCompleted;
        if (!flowController.TryCompleteActiveStory(sequence))
        {
            Debug.LogError($"{nameof(StoryCampaignBridge)}: Story 완료 뒤 캠페인 다음 단계 진입에 실패했습니다.", this);
        }
    }

    /// <summary>
    /// 캠페인 Story 연결에 필요한 Runner 참조가 준비되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (runner == null)
        {
            Debug.LogError($"{nameof(StoryCampaignBridge)} on {name}에는 {nameof(StoryRunner)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
