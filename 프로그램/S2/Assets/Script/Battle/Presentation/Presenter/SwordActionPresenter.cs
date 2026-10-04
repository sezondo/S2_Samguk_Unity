using System.Collections;
using UnityEngine;

/// <summary>
/// 검 이동 연출과 검 소지 근접 공격 자세를 실제 검 Visual에 적용하는 Presenter다.
/// 논리 검 위치는 변경하지 않고 PresentationEvent와 CombatActionPresenter 알림만 화면에 반영한다.
/// </summary>
public class SwordActionPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Target")]
    // 이 Presenter가 처리할 플레이어 논리 Actor다.
    [SerializeField] private GridActor ownerActor;
    // 시작 시 검의 회수 여부와 현재 논리 칸을 제공하는 검 상태다.
    [SerializeField] private PlayerSwordState swordState;

    [Header("Visual Reference")]
    // 화면에서 직접 위치와 회전을 제어할 검 Visual이다.
    [SerializeField] private Transform swordVisual;
    // 플레이어 VisualRoot와 현재 좌우 방향을 제공하는 시각 제어 컴포넌트다.
    [SerializeField] private ActorVisualController ownerVisualController;
    // 검 소지 근접 공격의 시작과 종료를 알릴 통합 전투 Presenter다.
    [SerializeField] private CombatActionPresenter combatActionPresenter;

    [Header("Recalled Pose")]
    // 검 회수 상태에서 플레이어 오른쪽 위에 둘 로컬 오프셋이다. 왼쪽을 볼 때 X 값은 반전한다.
    [SerializeField] private Vector3 recalledOffset = new(0.5f, 0.7f, 0f);
    // 검 회수 상태의 기본 로컬 Z 회전값이다.
    [SerializeField] private float recalledRotation;
    // 검 소지 근접 공격 중 공격 방향으로 기울일 각도다.
    [SerializeField] private float meleeTiltAngle = 70f;

    [Header("Deployed Pose")]
    // 투척 또는 해킹으로 배치된 검의 그리드 칸 기준 월드 오프셋이다.
    [SerializeField] private Vector3 deployedPositionOffset;

    [Header("Sword Trail")]
    // 투척·회수·해킹 접근에 함께 쓰는 확정 궤적 프리팹 ID다.
    [SerializeField] private VfxId pathVfxId = VfxId.YujinSwordTrail;
    // 궤적 캔버스의 월드 높이다. 이동 거리와 독립적으로 유지한다.
    [SerializeField] private float pathVfxWidth = 0.5f;
    // 일반 투척·회수·해킹 접근의 비행 속도와 최소 이동 시간이다.
    [SerializeField] private float swordTravelSpeed = 18f;
    [SerializeField] private float minimumTravelDuration = 0.12f;
    // 도착 뒤 잔상이 사라지는 시간이다.
    [SerializeField] private float trailFadeDuration = 0.18f;
    // 끝부분은 늘리지 않고 몸통만 반복하도록 할 기본 길이다.
    [SerializeField] private float trailTailLength = 0.65f;
    [SerializeField] private float trailHeadLength = 0.45f;
    // 이동 중 검 위치와 궤적을 함께 갱신하는 코루틴과 대여 핸들이다.
    private Coroutine swordMoveCoroutine;
    private VfxHandle trailEffect;
    private PresentationEventHandle moveHandle;

    [Header("Debug")]
    // true면 검 이동과 근접 자세 시작·종료를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logSwordFlow = true;

    // true면 현재 검 Visual이 플레이어에게 회수되어 VisualRoot를 따라가는 상태다.
    private bool isRecalledVisual;
    // true면 검 소지 근접 공격용 기울기 자세를 유지하는 상태다.
    private bool isMeleePoseActive;

    /// <summary>
    /// 필수 참조와 데이터를 검사하고 연출 큐에 등록한 뒤 전투 연출 알림을 구독한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        SubscribeCombatPresentation();
        TryRegisterQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서가 끝난 뒤 큐 등록을 보정하고 논리 검 상태에 Visual을 맞춘다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
        SynchronizeInitialSwordVisual();
    }

    /// <summary>
    /// 회수 상태의 검이 플레이어 방향 변경과 이동을 계속 따라가도록 위치를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!isRecalledVisual)
        {
            return;
        }

        ApplyRecalledPosition();
        if (!isMeleePoseActive)
        {
            swordVisual.localRotation = Quaternion.Euler(0f, 0f, recalledRotation);
        }
    }

    /// <summary>
    /// 연출 큐 등록과 전투 연출 알림 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (combatActionPresenter != null)
        {
            combatActionPresenter.CombatPresentationStarted -= HandleCombatPresentationStarted;
            combatActionPresenter.CombatPresentationCompleted -= HandleCombatPresentationCompleted;
        }

        isMeleePoseActive = false;
        if (swordMoveCoroutine != null) StopCoroutine(swordMoveCoroutine);
        swordMoveCoroutine = null;
        trailEffect?.Release(); trailEffect = null;
        moveHandle?.Complete(); moveHandle = null;
    }

    /// <summary>
    /// 현재 씬의 연출 큐에 핸들러 등록을 시도한다.
    /// </summary>
    private void TryRegisterQueue(bool logMissingQueue)
    {
        ActionPresentationQueue queue = ActionPresentationQueue.Instance;
        if (queue == null)
        {
            if (logMissingQueue)
            {
                Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 통합 전투 Presenter의 시작·종료 알림을 중복 없이 구독한다.
    /// </summary>
    private void SubscribeCombatPresentation()
    {
        combatActionPresenter.CombatPresentationStarted -= HandleCombatPresentationStarted;
        combatActionPresenter.CombatPresentationStarted += HandleCombatPresentationStarted;
        combatActionPresenter.CombatPresentationCompleted -= HandleCombatPresentationCompleted;
        combatActionPresenter.CombatPresentationCompleted += HandleCombatPresentationCompleted;
    }

    /// <summary>
    /// 자신이 소유한 검의 SwordMove 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.SwordMove &&
               presentationEvent.Actor == ownerActor;
    }

    /// <summary>이벤트 목적지로 검을 비행시키고 잔상이 정리된 뒤 큐를 완료한다.</summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (!HasValidReference() || !HasValidData() || GridManager.Instance == null ||
            VfxManager.Instance == null || !VfxManager.Instance.isActiveAndEnabled || swordMoveCoroutine != null)
        {
            Debug.LogError("검 이동 연출의 참조 또는 재생 상태가 올바르지 않습니다.", this);
            handle.Complete();
            return;
        }
        var started = StartCoroutine(PlaySwordMove(presentationEvent, handle));
        swordMoveCoroutine = moveHandle == null ? null : started;
    }

    /// <summary>전투 투척은 타격과 같은 컷으로 표시하고, 일반 투척·회수·해킹은 비행을 재생한다.</summary>
    private IEnumerator PlaySwordMove(PresentationEvent evt, PresentationEventHandle handle)
    {
        moveHandle = handle;
        bool combatThrow = evt.SwordMoveKind == SwordMoveKind.Throw && combatActionPresenter.CameraPresenter.IsCombatActive;
        if (combatThrow && !combatActionPresenter.TryBeginSwordThrow(evt, ownerVisualController))
        {
            moveHandle = null;
            handle.Complete();
            yield break;
        }
        if (isRecalledVisual) ApplyRecalledPosition();
        Vector3 from = swordVisual.position;
        Vector3 to = evt.SwordMoveKind == SwordMoveKind.Recall
            ? GetRecalledWorldPosition()
            : GridManager.Instance.GridToWorld(evt.ToPosition) + deployedPositionOffset;
        if (combatThrow && combatActionPresenter.CameraPresenter.TryGetTargetCenter(out Vector3 targetCenter))
            to = targetCenter;
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        // 전투 투척만 비행 대기를 없애 타격과 연결하고, 정찰 투척은 실제 이동을 보여준다.
        float duration = combatThrow
            ? 0f : Mathf.Max(minimumTravelDuration, distance / swordTravelSpeed);
        trailEffect = VfxManager.TryAcquire(pathVfxId);
        if (trailEffect != null && trailEffect.PartCount != 3)
        {
            Debug.LogError("검 궤적 프리팹에는 꼬리·몸통·앞부분 3개 Sprite가 필요합니다.", this);
            trailEffect.Release(); trailEffect = null;
        }
        DeploySword(from, from - delta);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            swordVisual.position = Vector3.Lerp(from, to, t);
            UpdateTrail(from, swordVisual.position, 1f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        swordVisual.position = to;
        UpdateTrail(from, to, 1f);
        if (evt.SwordMoveKind == SwordMoveKind.Recall) AttachSwordToPlayer();
        // 대상이 있으면 전체 잔상을 표시한 이 프레임에 다음 타격을 시작한다.
        bool impactFollows = combatThrow && combatActionPresenter.CameraPresenter.HasActorTarget;
        if (impactFollows) handle.Complete();
        elapsed = 0f;
        while (elapsed < trailFadeDuration)
        {
            float t = Mathf.Clamp01(elapsed / trailFadeDuration);
            UpdateTrail(Vector3.Lerp(from, to, t), to, 1f - t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        trailEffect?.Release(); trailEffect = null;
        moveHandle = null; swordMoveCoroutine = null;
        if (combatThrow)
            combatActionPresenter.CameraPresenter.DeferUntilRestored(() =>
            {
                if (swordVisual != null && !isRecalledVisual)
                    swordVisual.position = GridManager.Instance.GridToWorld(evt.ToPosition) + deployedPositionOffset;
            });
        if (logSwordFlow) Debug.Log($"검 {evt.SwordMoveKind} 비행과 잔상 연출을 완료했습니다.", this);
        handle.Complete();
    }

    /// <summary>끝 조각의 비율과 두께를 유지하고 가운데 띠만 반복하여 현재 경로 길이를 채운다.</summary>
    private void UpdateTrail(Vector3 from, Vector3 to, float opacity)
    {
        if (trailEffect == null || !trailEffect.IsValid) return;
        Vector3 direction = to - from;
        float length = direction.magnitude;
        float capRatio = Mathf.Min(1f, length / (trailTailLength + trailHeadLength));
        float tail = trailTailLength * capRatio;
        float head = trailHeadLength * capRatio;
        float body = Mathf.Max(0f, length - tail - head);
        trailEffect.SetTransform(from, Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg), Vector3.one);
        SpriteRenderer owner = ownerVisualController.TargetRenderer;
        trailEffect.SetSorting(owner.sortingLayerID, owner.sortingOrder + 2);
        float alpha = length > 0.001f ? opacity : 0f;
        trailEffect.SetPart(0, new Vector3(tail * 0.5f, 0f, 0f), new Vector2(tail, pathVfxWidth), alpha);
        trailEffect.SetPart(1, new Vector3(tail + body * 0.5f, 0f, 0f), new Vector2(body, pathVfxWidth),
            body > 0.001f ? alpha : 0f, repeat: body / (pathVfxWidth * 12.8f));
        trailEffect.SetPart(2, new Vector3(length - head * 0.5f, 0f, 0f), new Vector2(head, pathVfxWidth), alpha);
    }

    /// <summary>
    /// 검 Visual을 플레이어 VisualRoot에서 분리하고 목표 월드 위치와 이동 방향 회전을 적용한다.
    /// </summary>
    private void DeploySword(Vector3 toWorldPosition, Vector3 fromWorldPosition)
    {
        isRecalledVisual = false;
        isMeleePoseActive = false;
        swordVisual.SetParent(null, true);
        swordVisual.position = toWorldPosition;
        swordVisual.rotation = Quaternion.Euler(0f, 0f, CalculateDirectionAngle(fromWorldPosition, toWorldPosition));
    }

    /// <summary>
    /// 검 Visual을 플레이어 VisualRoot의 자식으로 연결하고 현재 좌우 방향의 상단 위치에 둔다.
    /// </summary>
    private void AttachSwordToPlayer()
    {
        isRecalledVisual = true;
        isMeleePoseActive = false;
        swordVisual.SetParent(ownerVisualController.transform, true);
        ApplyRecalledPosition();
        swordVisual.localRotation = Quaternion.Euler(0f, 0f, recalledRotation);
    }

    /// <summary>
    /// 플레이어 현재 방향에 맞춰 회수 검의 좌우 상단 로컬 위치를 적용한다.
    /// </summary>
    private void ApplyRecalledPosition()
    {
        Vector3 localPosition = recalledOffset;
        localPosition.x = Mathf.Abs(recalledOffset.x) * (ownerVisualController.IsFacingRight ? 1f : -1f);
        swordVisual.localPosition = localPosition;
    }

    /// <summary>
    /// 현재 플레이어 방향의 회수 검 위치를 월드 좌표로 반환한다.
    /// </summary>
    private Vector3 GetRecalledWorldPosition()
    {
        Vector3 localPosition = recalledOffset;
        localPosition.x = Mathf.Abs(recalledOffset.x) * (ownerVisualController.IsFacingRight ? 1f : -1f);
        return ownerVisualController.transform.TransformPoint(localPosition);
    }

    /// <summary>
    /// 위쪽을 향하는 원본 검 아트를 출발점에서 도착점 방향으로 돌릴 Z 각도를 계산한다.
    /// </summary>
    private static float CalculateDirectionAngle(Vector3 fromWorldPosition, Vector3 toWorldPosition)
    {
        Vector3 direction = toWorldPosition - fromWorldPosition;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return 0f;
        }

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    }

    /// <summary>
    /// 검 소지 근접 공격 시작 시 플레이어 공격 방향으로 검을 기울인다.
    /// </summary>
    private void HandleCombatPresentationStarted(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Actor != ownerActor ||
            presentationEvent.AttackKind != AttackPresentationKind.MeleeWithSword ||
            !isRecalledVisual)
        {
            return;
        }

        if (!isRecalledVisual)
        {
            AttachSwordToPlayer();
        }

        isMeleePoseActive = true;
        ApplyRecalledPosition();
        float tiltAngle = ownerVisualController.IsFacingRight ? -meleeTiltAngle : meleeTiltAngle;
        swordVisual.localRotation = Quaternion.Euler(0f, 0f, recalledRotation + tiltAngle);

        if (logSwordFlow)
        {
            Debug.Log($"{nameof(SwordActionPresenter)}: 검 소지 근접 공격 자세를 시작했습니다. 오른쪽 방향: {ownerVisualController.IsFacingRight}", this);
        }
    }

    /// <summary>
    /// 검 소지 근접 공격 종료 시 검을 현재 방향의 좌우 상단 회수 자세로 복구한다.
    /// </summary>
    private void HandleCombatPresentationCompleted(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Actor != ownerActor ||
            presentationEvent.AttackKind != AttackPresentationKind.MeleeWithSword ||
            !isRecalledVisual)
        {
            return;
        }

        AttachSwordToPlayer();

        if (logSwordFlow)
        {
            Debug.Log($"{nameof(SwordActionPresenter)}: 검 소지 근접 공격 자세를 종료했습니다.", this);
        }
    }

    /// <summary>
    /// 시작 시 논리 검 상태에 맞춰 검 Visual을 회수 위치 또는 현재 배치 칸에 동기화한다.
    /// </summary>
    private void SynchronizeInitialSwordVisual()
    {
        if (swordState.IsRecalled)
        {
            AttachSwordToPlayer();
            return;
        }

        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 초기 검 위치 동기화에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            enabled = false;
            return;
        }

        isRecalledVisual = false;
        swordVisual.SetParent(null, true);
        swordVisual.position = gridManager.GridToWorld(swordState.CurrentPosition) + deployedPositionOffset;
    }

    /// <summary>
    /// 검 연출에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (ownerActor == null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 검 연출 기준 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (swordState == null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 논리 검 상태를 제공할 {nameof(PlayerSwordState)} 참조가 필요합니다.", this);
            return false;
        }

        if (swordVisual == null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 직접 제어할 검 Visual 참조가 필요합니다.", this);
            return false;
        }

        if (ownerVisualController == null || !ownerVisualController.HasValidReference())
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 플레이어 {nameof(ActorVisualController)} 참조가 필요합니다.", this);
            return false;
        }

        if (combatActionPresenter == null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 근접 검 연출을 연결할 {nameof(CombatActionPresenter)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 검 위치, 근접 각도와 선택 경로 VFX 설정이 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (meleeTiltAngle < 0f || meleeTiltAngle > 180f)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}의 근접 검 기울기 각도는 0 이상 180 이하여야 합니다.", this);
            return false;
        }

        if (pathVfxId == VfxId.None || pathVfxWidth <= 0f || swordTravelSpeed <= 0f || minimumTravelDuration <= 0f || trailFadeDuration <= 0f || trailTailLength <= 0f || trailHeadLength <= 0f)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}의 검 경로 VFX 두께와 길이 배율은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
