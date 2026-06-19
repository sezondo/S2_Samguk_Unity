using System.Collections;
using UnityEngine;

/// <summary>
/// GridActor 이동 연출 이벤트를 받아 VisualRoot를 화면상 목표 위치로 이동시키는 Presenter다.
/// 큐 이벤트 처리 완료 신호는 이 컴포넌트가 최종 호출한다.
/// </summary>
public class GridActorMovePresenter : MonoBehaviour
{
    [Header("Target")]
    // 이 Presenter가 이동 연출을 처리할 논리 Actor다.
    [SerializeField] private GridActor targetActor;
    // 실제 화면에서 이동시킬 표시 루트 Transform이다.
    [SerializeField] private Transform visualRoot;
    // VisualRoot의 스프라이트/애니메이션 제어 컴포넌트다.
    [SerializeField] private ActorVisualController visualController;

    [Header("Data")]
    // 이동 시간, 보간 곡선, 애니메이션 요청값을 가진 이동 연출 데이터다.
    [SerializeField] private MovePresentationData moveData;

    [Header("Event")]
    // true면 적 AI 반응 이동 이벤트도 같은 이동 연출로 처리한다.
    [SerializeField] private bool handleEnemyReactionMove = true;

    [Header("Debug")]
    // true면 이동 연출 시작과 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logMoveFlow;

    // 현재 실행 중인 이동 연출 코루틴이다.
    private Coroutine moveCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;

    /// <summary>
    /// 큐 이벤트 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        TrySubscribeQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeQueue(true);
    }

    /// <summary>
    /// 큐 이벤트 구독을 해제하고 진행 중인 이벤트가 있으면 큐 정지를 막기 위해 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.PresentationEventStarted -= HandlePresentationEventStarted;
        }

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
            activeHandle = null;
        }
    }



    /// <summary>
    /// 현재 씬의 연출 큐 이벤트를 구독한다.
    /// </summary>
    private void TrySubscribeQueue(bool logMissingQueue)
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            if (logMissingQueue)
            {
                Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.PresentationEventStarted -= HandlePresentationEventStarted;
        queue.PresentationEventStarted += HandlePresentationEventStarted;
    }

    /// <summary>
    /// 이동 연출 이벤트 중 자기 Actor 대상 이벤트만 처리한다.
    /// </summary>
    private bool HandlePresentationEventStarted(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (!CanHandle(presentationEvent))
        {
            return false;
        }

        if (moveCoroutine != null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}은 이미 이동 연출을 처리 중입니다. 새 이동 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return true;
        }

        if (!HasValidReference() || !HasValidData())
        {
            handle.Complete();
            return true;
        }

        moveCoroutine = StartCoroutine(PlayMove(presentationEvent, handle));
        return true;
    }

    /// <summary>
    /// 이 Presenter가 지정한 이벤트를 처리할 수 있는지 확인한다.
    /// </summary>
    private bool CanHandle(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Type == PresentationEventType.MoveActor)
        {
            return presentationEvent.Actor == targetActor;
        }

        if (handleEnemyReactionMove && presentationEvent.Type == PresentationEventType.EnemyReactionMove)
        {
            return presentationEvent.Actor == targetActor;
        }

        return false;
    }

    /// <summary>
    /// VisualRoot 위치를 FromPosition에서 ToPosition까지 보간하고 완료 신호를 보낸다.
    /// </summary>
    private IEnumerator PlayMove(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        activeHandle = handle;

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}에는 이동 연출 좌표 변환에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            CompleteActiveMove(handle);
            yield break;
        }

        if (!IsAdjacentMove(presentationEvent))
        {
            Debug.LogWarning($"{nameof(GridActorMovePresenter)}: {presentationEvent.Type} 이벤트의 시작 칸과 목표 칸이 인접하지 않습니다. 이동 경로는 1칸 단위 이벤트로 나누는 것을 권장합니다. 이벤트: {presentationEvent}", this);
        }

        Vector3 fromWorldPosition = gridManager.GridToWorld(presentationEvent.FromPosition);
        Vector3 toWorldPosition = gridManager.GridToWorld(presentationEvent.ToPosition);

        visualRoot.position = fromWorldPosition;
        visualController.TryPlayMoveAnimation(moveData);

        if (logMoveFlow)
        {
            Debug.Log($"{nameof(GridActorMovePresenter)}: 이동 연출을 시작합니다. 이벤트: {presentationEvent}", this);
        }

        float elapsed = 0f;
        while (elapsed < moveData.MoveDuration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / moveData.MoveDuration);
            float curveTime = moveData.MoveCurve.Evaluate(normalizedTime);
            visualRoot.position = Vector3.LerpUnclamped(fromWorldPosition, toWorldPosition, curveTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        visualRoot.position = toWorldPosition;
        visualController.TryCompleteMoveAnimation(moveData);

        if (logMoveFlow)
        {
            Debug.Log($"{nameof(GridActorMovePresenter)}: 이동 연출을 완료했습니다. 이벤트: {presentationEvent}", this);
        }

        CompleteActiveMove(handle);
    }

    /// <summary>
    /// 현재 이동 연출 상태를 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveMove(PresentationEventHandle handle)
    {
        moveCoroutine = null;
        activeHandle = null;
        handle.Complete();
    }

    /// <summary>
    /// 이동 연출 이벤트가 상하좌우 인접 칸 1회 이동인지 확인한다.
    /// </summary>
    private bool IsAdjacentMove(PresentationEvent presentationEvent)
    {
        return presentationEvent.FromPosition.ManhattanDistanceTo(presentationEvent.ToPosition) == 1;
    }

    /// <summary>
    /// 필수 컴포넌트 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetActor == null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}에는 이동 연출 대상 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (visualRoot == null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}에는 이동시킬 VisualRoot Transform 참조가 필요합니다.", this);
            return false;
        }

        if (visualController == null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}에는 {nameof(ActorVisualController)} 참조가 필요합니다.", this);
            return false;
        }

        if (!visualController.HasValidReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 이동 연출 데이터가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (moveData == null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}에는 {nameof(MovePresentationData)} 참조가 필요합니다.", this);
            return false;
        }

        if (moveData.MoveDuration <= 0f)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}의 {nameof(MovePresentationData)} 이동 시간은 0보다 커야 합니다.", this);
            return false;
        }

        if (moveData.MoveCurve == null)
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}의 {nameof(MovePresentationData)} 이동 곡선이 비어 있습니다.", this);
            return false;
        }

        if (moveData.UseMoveAnimation && string.IsNullOrWhiteSpace(moveData.MoveAnimationStateName))
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}의 이동 애니메이션 상태 이름이 비어 있습니다.", this);
            return false;
        }

        if (moveData.PlayIdleAnimationOnComplete && string.IsNullOrWhiteSpace(moveData.IdleAnimationStateName))
        {
            Debug.LogError($"{nameof(GridActorMovePresenter)} on {name}의 대기 애니메이션 상태 이름이 비어 있습니다.", this);
            return false;
        }

        return true;
    }
}
