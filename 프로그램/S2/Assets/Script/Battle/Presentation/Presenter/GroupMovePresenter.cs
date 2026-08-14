using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 적 편대의 실제 확정 경로를 받아 모든 구성원의 Visual을 한 칸씩 동시에 이동시킨다.
/// </summary>
public class GroupMovePresenter : MonoBehaviour, IPresentationEventHandler
{
    private sealed class MemberPresentationState
    {
        // 구성원의 논리 이동 결과를 고정한 경로 스냅샷이다.
        public GroupMovePresentationMemberSnapshot Snapshot { get; }
        // 현재 구성원의 스프라이트와 애니메이션을 제어할 시각 컴포넌트다.
        public ActorVisualController VisualController { get; }
        // 이동 전에 시야 방향을 회전할 선택적 방향 표시 Presenter다.
        public EnemyFacingIndicatorPresenter FacingIndicator { get; }
        // 실제 월드 위치를 보간할 구성원의 표시 루트다.
        public Transform VisualRoot => VisualController.transform;

        /// <summary>
        /// 구성원 한 명의 이동 경로와 화면 표시 컴포넌트를 묶는다.
        /// </summary>
        public MemberPresentationState(
            GroupMovePresentationMemberSnapshot snapshot,
            ActorVisualController visualController,
            EnemyFacingIndicatorPresenter facingIndicator)
        {
            Snapshot = snapshot;
            VisualController = visualController;
            FacingIndicator = facingIndicator;
        }
    }

    [Header("Target")]
    // 이 Presenter가 그룹 이동 연출을 처리할 적 편대다.
    [SerializeField] private EnemyPatrolGroup targetGroup;

    [Header("Data")]
    // 편대 전체가 공유할 한 칸 이동 시간·곡선·애니메이션 설정이다.
    [SerializeField] private GroupMovePresentationData presentationData;

    [Header("Debug")]
    // true면 편대 이동 연출의 시작과 종료를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logMoveFlow;

    // 현재 이벤트의 구성원별 Visual과 경로를 보관하는 버퍼다.
    private readonly List<MemberPresentationState> memberStates = new();
    // 현재 편대 방향 전환 완료를 함께 기다릴 방향 표시 Presenter 버퍼다.
    private readonly List<EnemyFacingIndicatorPresenter> facingIndicators = new();
    // 현재 실행 중인 편대 이동 코루틴이다.
    private Coroutine moveCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;

    /// <summary>
    /// 필수 편대와 연출 데이터가 연결돼 있는지 확인하고 연출 큐 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        TryRegisterQueue(false);
    }

    /// <summary>
    /// 초기화 순서로 OnEnable에서 놓친 연출 큐 등록을 다시 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 큐 등록을 해제하고 진행 중인 편대 이동이 있으면 큐 정지를 막도록 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }

        memberStates.Clear();
        facingIndicators.Clear();
        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
        }

        activeHandle = null;
    }

    /// <summary>
    /// 현재 씬의 단일 연출 큐에 이 Presenter를 등록한다.
    /// </summary>
    private void TryRegisterQueue(bool logMissingQueue)
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            if (logMissingQueue)
            {
                Debug.LogError($"{nameof(GroupMovePresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 지정한 편대의 그룹 이동과 공통 방향 전환 이벤트만 처리 대상으로 선택한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type switch
        {
            PresentationEventType.GroupMove => presentationEvent.GroupMoveSnapshot?.Group == targetGroup,
            PresentationEventType.GroupFacingTurn => presentationEvent.GroupFacingTurnSnapshot?.Group == targetGroup,
            _ => false,
        };
    }

    /// <summary>
    /// 구성원별 Visual 연결을 확인하고 편대 동시 이동 연출을 시작한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (moveCoroutine != null)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}은 이미 편대 이동 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다.", this);
            handle.Complete();
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            handle.Complete();
            return;
        }

        if (presentationEvent.Type == PresentationEventType.GroupFacingTurn)
        {
            moveCoroutine = StartCoroutine(PlayGroupFacingTurn(
                presentationEvent.GroupFacingTurnSnapshot,
                handle));
            return;
        }

        GroupMovePresentationSnapshot snapshot = presentationEvent.GroupMoveSnapshot;
        if (!TryBuildMemberStates(snapshot))
        {
            handle.Complete();
            return;
        }

        moveCoroutine = StartCoroutine(PlayGroupMove(snapshot, handle));
    }

    /// <summary>
    /// 편대 등록 순서의 각 Actor에 연결된 Visual을 찾아 현재 이벤트 실행 상태를 준비한다.
    /// </summary>
    private bool TryBuildMemberStates(GroupMovePresentationSnapshot snapshot)
    {
        memberStates.Clear();
        if (snapshot == null || snapshot.Group != targetGroup || snapshot.StepCount <= 0 || snapshot.Members.Count == 0)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}이 올바르지 않은 편대 이동 스냅샷을 받았습니다.", this);
            return false;
        }

        ActorPresentationRegistry registry = ActorPresentationRegistry.Instance;
        if (registry == null)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}에는 구성원 Visual을 찾을 {nameof(ActorPresentationRegistry)}가 필요합니다.", this);
            return false;
        }

        for (int i = 0; i < snapshot.Members.Count; i++)
        {
            GroupMovePresentationMemberSnapshot member = snapshot.Members[i];
            if (member?.Actor == null || member.Path == null || member.Path.Count == 0)
            {
                Debug.LogError($"{nameof(GroupMovePresenter)}: {targetGroup.name} 편대의 {i}번 이동 스냅샷이 올바르지 않습니다.", this);
                memberStates.Clear();
                return false;
            }

            if (!registry.TryGetVisual(member.Actor, out ActorVisualController visualController) ||
                !visualController.HasValidAnimationReference())
            {
                Debug.LogError($"{nameof(GroupMovePresenter)}: {member.Actor.name} Actor의 유효한 Visual 연결을 찾지 못했습니다.", this);
                memberStates.Clear();
                return false;
            }

            registry.TryGetFacingIndicator(
                member.Actor,
                out EnemyFacingIndicatorPresenter facingIndicator);
            memberStates.Add(new MemberPresentationState(member, visualController, facingIndicator));
        }

        return memberStates.Count > 0;
    }

    /// <summary>
    /// 실제 확정된 최대 단계까지 구성원별 경로를 한 칸씩 동시에 보간한다.
    /// </summary>
    private IEnumerator PlayGroupMove(GroupMovePresentationSnapshot snapshot, PresentationEventHandle handle)
    {
        activeHandle = handle;
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}에는 좌표 변환에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            CompleteActiveMove(handle);
            yield break;
        }

        for (int i = 0; i < memberStates.Count; i++)
        {
            MemberPresentationState state = memberStates[i];
            state.VisualRoot.position = gridManager.GridToWorld(state.Snapshot.StartPosition);
            if (presentationData.UseMoveAnimation)
            {
                state.VisualController.TryPlayAnimationState(
                    presentationData.MoveAnimationStateName,
                    presentationData.MoveAnimationCrossFadeDuration,
                    false);
            }
        }

        if (logMoveFlow)
        {
            Debug.Log($"{nameof(GroupMovePresenter)}: {targetGroup.name} 편대의 {snapshot.StepCount}칸 동시 이동 연출을 시작합니다.", this);
        }

        for (int stepIndex = 0; stepIndex < snapshot.StepCount; stepIndex++)
        {
            yield return PlayStep(gridManager, stepIndex);
        }

        if (presentationData.PlayIdleAnimationOnComplete)
        {
            for (int i = 0; i < memberStates.Count; i++)
            {
                memberStates[i].VisualController.TryPlayAnimationState(
                    presentationData.IdleAnimationStateName,
                    presentationData.IdleAnimationCrossFadeDuration,
                    false);
            }
        }

        if (logMoveFlow)
        {
            Debug.Log($"{nameof(GroupMovePresenter)}: {targetGroup.name} 편대 동시 이동 연출을 완료했습니다.", this);
        }

        CompleteActiveMove(handle);
    }

    /// <summary>
    /// 현재 단계에 경로가 남은 모든 구성원을 같은 시간과 곡선으로 동시에 한 칸 이동시킨다.
    /// </summary>
    private IEnumerator PlayStep(GridManager gridManager, int stepIndex)
    {
        PlayerVisionPresenter visionPresenter = PlayerVisionPresenter.Instance;
        facingIndicators.Clear();
        for (int i = 0; i < memberStates.Count; i++)
        {
            MemberPresentationState state = memberStates[i];
            if (stepIndex >= state.Snapshot.Path.Count)
            {
                continue;
            }

            GridPosition from = stepIndex == 0
                ? state.Snapshot.StartPosition
                : state.Snapshot.Path[stepIndex - 1];
            GridPosition to = state.Snapshot.Path[stepIndex];
            state.VisualRoot.position = gridManager.GridToWorld(from);
            ApplyVisionAlpha(state, visionPresenter, from, false);

            if (state.FacingIndicator != null)
            {
                GridDirection movementDirection = EnemyMovementUtility.GetDirectionFromMovement(from, to);
                if (!state.FacingIndicator.TryStartPresentationTurn(movementDirection))
                {
                    Debug.LogError($"{nameof(GroupMovePresenter)}: {state.Snapshot.Actor.name} 적의 이동 전 시야 방향 회전을 시작하지 못했습니다.", this);
                }

                AddFacingIndicatorToWait(state.FacingIndicator);
            }
        }

        // 모든 구성원의 제자리 방향 회전이 완전히 끝난 뒤에만 이번 한 칸 이동을 시작한다.
        yield return WaitForFacingTurns();

        for (int i = 0; i < memberStates.Count; i++)
        {
            MemberPresentationState state = memberStates[i];
            if (stepIndex >= state.Snapshot.Path.Count)
            {
                continue;
            }

            GridPosition from = stepIndex == 0
                ? state.Snapshot.StartPosition
                : state.Snapshot.Path[stepIndex - 1];
            GridPosition to = state.Snapshot.Path[stepIndex];
            state.VisualController.FaceFromTo(from, to);
        }

        float elapsed = 0f;
        while (elapsed < presentationData.MoveDurationPerCell)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / presentationData.MoveDurationPerCell);
            float curveTime = presentationData.MoveCurve.Evaluate(normalizedTime);
            for (int i = 0; i < memberStates.Count; i++)
            {
                MemberPresentationState state = memberStates[i];
                if (stepIndex >= state.Snapshot.Path.Count)
                {
                    continue;
                }

                GridPosition from = stepIndex == 0
                    ? state.Snapshot.StartPosition
                    : state.Snapshot.Path[stepIndex - 1];
                GridPosition to = state.Snapshot.Path[stepIndex];
                state.VisualRoot.position = Vector3.LerpUnclamped(
                    gridManager.GridToWorld(from),
                    gridManager.GridToWorld(to),
                    curveTime);
                ApplyVisionAlpha(state, visionPresenter, from, to, curveTime);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < memberStates.Count; i++)
        {
            MemberPresentationState state = memberStates[i];
            if (stepIndex >= state.Snapshot.Path.Count)
            {
                continue;
            }

            GridPosition to = state.Snapshot.Path[stepIndex];
            state.VisualRoot.position = gridManager.GridToWorld(to);
            ApplyVisionAlpha(state, visionPresenter, to, true);
        }
    }

    /// <summary>
    /// PatrolPoint 도착처럼 이동과 분리된 편대 공통 시야 방향 전환을 동시에 재생한다.
    /// </summary>
    private IEnumerator PlayGroupFacingTurn(
        GroupFacingTurnPresentationSnapshot snapshot,
        PresentationEventHandle handle)
    {
        activeHandle = handle;
        facingIndicators.Clear();
        ActorPresentationRegistry registry = ActorPresentationRegistry.Instance;
        if (snapshot == null || snapshot.Group != targetGroup || snapshot.Members.Count == 0 || registry == null)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}이 올바르지 않은 편대 방향 전환 스냅샷 또는 연출 등록소를 받았습니다.", this);
            CompleteActiveMove(handle);
            yield break;
        }

        for (int i = 0; i < snapshot.Members.Count; i++)
        {
            GroupFacingTurnPresentationMemberSnapshot member = snapshot.Members[i];
            if (member?.Actor == null ||
                !registry.TryGetFacingIndicator(member.Actor, out EnemyFacingIndicatorPresenter indicator))
            {
                continue;
            }

            if (!indicator.TryStartPresentationTurn(member.ToDirection))
            {
                Debug.LogError($"{nameof(GroupMovePresenter)}: {member.Actor.name} 적의 편대 시야 방향 회전을 시작하지 못했습니다.", this);
            }

            AddFacingIndicatorToWait(indicator);
        }

        yield return WaitForFacingTurns();
        CompleteActiveMove(handle);
    }

    /// <summary>
    /// 중복 없이 현재 완료를 기다릴 방향 표시 Presenter를 추가한다.
    /// </summary>
    private void AddFacingIndicatorToWait(EnemyFacingIndicatorPresenter indicator)
    {
        if (indicator != null && !facingIndicators.Contains(indicator))
        {
            facingIndicators.Add(indicator);
        }
    }

    /// <summary>
    /// 등록된 모든 방향 표시 Presenter의 회전 연출이 끝날 때까지 기다린다.
    /// </summary>
    private IEnumerator WaitForFacingTurns()
    {
        bool isAnyTurning = true;
        while (isAnyTurning)
        {
            isAnyTurning = false;
            for (int i = 0; i < facingIndicators.Count; i++)
            {
                if (facingIndicators[i] != null && facingIndicators[i].IsTurning)
                {
                    isAnyTurning = true;
                    break;
                }
            }

            if (isAnyTurning)
            {
                yield return null;
            }
        }

        facingIndicators.Clear();
    }

    /// <summary>
    /// 적 구성원의 시작·목표 칸 공개 상태를 이동 진행률에 맞춰 보간한다.
    /// </summary>
    private static void ApplyVisionAlpha(
        MemberPresentationState state,
        PlayerVisionPresenter visionPresenter,
        GridPosition from,
        GridPosition to,
        float normalizedTime)
    {
        if (!CanApplyVisionTransition(state, visionPresenter))
        {
            return;
        }

        float fromAlpha = visionPresenter.ShouldShowEnemyActorAt(state.Snapshot.Actor, from) ? 1f : 0f;
        float toAlpha = visionPresenter.ShouldShowEnemyActorAt(state.Snapshot.Actor, to) ? 1f : 0f;
        state.VisualController.SetVisionAlpha(Mathf.Lerp(fromAlpha, toAlpha, normalizedTime));
    }

    /// <summary>
    /// 현재 칸의 공개 상태를 즉시 적용하고 마지막 단계라면 Presenter의 이동 결과도 기록한다.
    /// </summary>
    private static void ApplyVisionAlpha(
        MemberPresentationState state,
        PlayerVisionPresenter visionPresenter,
        GridPosition position,
        bool recordMovedVisibility)
    {
        if (!CanApplyVisionTransition(state, visionPresenter))
        {
            return;
        }

        float alpha = visionPresenter.ShouldShowEnemyActorAt(state.Snapshot.Actor, position) ? 1f : 0f;
        state.VisualController.SetVisionAlpha(alpha);
        if (recordMovedVisibility)
        {
            visionPresenter.SetMovedEnemyVisibility(state.Snapshot.Actor, alpha > 0f);
        }
    }

    /// <summary>
    /// 현재 구성원에게 플레이어 시야 기반 알파 전환을 적용할 수 있는지 확인한다.
    /// </summary>
    private static bool CanApplyVisionTransition(
        MemberPresentationState state,
        PlayerVisionPresenter visionPresenter)
    {
        return visionPresenter != null &&
            visionPresenter.IsEnemyActor(state.Snapshot.Actor) &&
            !visionPresenter.IsActorForcedVisible(state.Snapshot.Actor);
    }

    /// <summary>
    /// 현재 편대 이동 상태를 정리하고 큐에 완료를 한 번 알린다.
    /// </summary>
    private void CompleteActiveMove(PresentationEventHandle handle)
    {
        moveCoroutine = null;
        activeHandle = null;
        memberStates.Clear();
        facingIndicators.Clear();
        handle.Complete();
    }

    /// <summary>
    /// 처리 대상 편대와 그룹 연출 데이터 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetGroup == null)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}에는 처리할 {nameof(EnemyPatrolGroup)} 참조가 필요합니다.", this);
            return false;
        }

        if (presentationData == null)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}에는 {nameof(GroupMovePresentationData)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 그룹 공통 이동 시간·곡선과 선택한 애니메이션 설정이 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (presentationData == null)
        {
            return false;
        }

        if (presentationData.MoveDurationPerCell <= 0f)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}의 편대 한 칸 이동 시간은 0보다 커야 합니다.", this);
            return false;
        }

        if (presentationData.MoveCurve == null || presentationData.MoveCurve.length == 0)
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}의 편대 이동 곡선이 비어 있습니다.", this);
            return false;
        }

        if (presentationData.UseMoveAnimation && string.IsNullOrWhiteSpace(presentationData.MoveAnimationStateName))
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}의 편대 이동 애니메이션 상태 이름이 필요합니다.", this);
            return false;
        }

        if (presentationData.PlayIdleAnimationOnComplete && string.IsNullOrWhiteSpace(presentationData.IdleAnimationStateName))
        {
            Debug.LogError($"{nameof(GroupMovePresenter)} on {name}의 편대 대기 애니메이션 상태 이름이 필요합니다.", this);
            return false;
        }

        return true;
    }
}
