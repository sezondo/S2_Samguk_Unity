using UnityEngine;
using System.Collections;

/// <summary>
/// 연출 큐에 샘플 이벤트를 넣어 순차 실행 흐름을 확인하는 임시 테스트 컴포넌트다.
/// 실제 게임 규칙에는 사용하지 않는다.
/// </summary>
public class DebugPresentationQueueTester : MonoBehaviour
{
    [Header("Debug")]
    // true면 Start 시점에 샘플 이벤트를 큐에 넣는다.
    [SerializeField] private bool enqueueOnStart;
    // true면 샘플 이벤트를 넣은 뒤 바로 큐 실행을 요청한다.
    [SerializeField] private bool playAfterEnqueue = true;

    [Header("Sample Move")]
    // 샘플 이동 이벤트의 대상 Actor다. 연결하면 GridActorMovePresenter 테스트에 사용할 수 있다.
    [SerializeField] private GridActor sampleMoveActor;
    // 샘플 이동 이벤트의 시작 그리드 좌표다.
    [SerializeField] private GridPosition sampleMoveFromPosition = GridPosition.Zero;
    // 샘플 이동 이벤트의 목표 그리드 좌표다.
    [SerializeField] private GridPosition sampleMoveToPosition = new(1, 0);

    /// <summary>
    /// 설정에 따라 시작 시점에 샘플 이벤트를 큐에 넣는다.
    /// </summary>
    private IEnumerator Start()
    {
        yield return null;

        if (enqueueOnStart)
        {
            EnqueueSampleEvents();
        }
    }

    /// <summary>
    /// 큐 검증용 샘플 이벤트 3개를 추가한다.
    /// </summary>
    public void EnqueueSampleEvents()
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            Debug.LogError($"{nameof(DebugPresentationQueueTester)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
            return;
        }

        queue.Enqueue(PresentationEvent.MoveActor(sampleMoveActor, sampleMoveFromPosition, sampleMoveToPosition, "디버그 이동 연출"));
        queue.Enqueue(PresentationEvent.AlertDetected(new GridPosition(1, 0), null, "디버그 발각 연출"));
        queue.Enqueue(PresentationEvent.StageCleared("디버그 클리어 연출"));

        if (playAfterEnqueue)
        {
            queue.PlayQueuedEvents();
        }
    }
}
