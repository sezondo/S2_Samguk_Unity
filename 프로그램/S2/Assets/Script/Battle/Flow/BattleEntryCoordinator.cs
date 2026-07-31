using System.Collections;
using UnityEngine;

/// <summary>
/// 전투 씬 초기화가 끝난 뒤 입장 연출을 전역 연출 큐의 첫 이벤트로 실행한다.
/// </summary>
public class BattleEntryCoordinator : MonoBehaviour
{
    /// <summary>
    /// 모든 씬 컴포넌트의 Start 등록이 끝난 다음 프레임에 입장 연출을 시작한다.
    /// </summary>
    private IEnumerator Start()
    {
        yield return null;

        CampaignBootstrap bootstrap = CampaignBootstrap.Instance;
        while (bootstrap != null && bootstrap.Context.SceneTransitionController.IsLoading)
        {
            yield return null;
        }

        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            Debug.LogError($"{nameof(BattleEntryCoordinator)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            enabled = false;
            yield break;
        }

        queue.Enqueue(PresentationEvent.BattleIntro("전투 입장 연출"));
        if (!queue.PlayQueuedEvents())
        {
            Debug.LogError($"{nameof(BattleEntryCoordinator)}: 전투 입장 연출 큐를 시작하지 못했습니다.", this);
        }
    }
}
