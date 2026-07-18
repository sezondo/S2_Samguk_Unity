using UnityEngine;

/// <summary>
/// 검 이동 연출과 검 소지 근접 공격 자세를 실제 검 Visual에 적용하는 Presenter다.
/// 논리 검 위치는 변경하지 않고 PresentationEvent와 CombatActionPresenter 알림만 화면에 반영한다.
/// </summary>
public class SwordActionPresenter : MonoBehaviour
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

    [Header("Optional Path VFX")]
    // 검 이동 경로에 재생할 선택 VFX다. None이면 경로 계산만 하고 재생하지 않는다.
    [SerializeField] private VfxId pathVfxId = VfxId.None;
    // 경로 VFX가 로컬 Y축을 길이 방향으로 사용할 때 적용할 가로 두께다.
    [SerializeField] private float pathVfxWidth = 1f;
    // 실제 이동 거리를 경로 VFX의 로컬 Y 스케일로 바꾸는 배율이다.
    [SerializeField] private float pathVfxLengthScale = 1f;

    [Header("Debug")]
    // true면 검 이동과 근접 자세 시작·종료를 Unity 콘솔에 출력한다.
    [SerializeField] private bool logSwordFlow = true;

    // true면 현재 검 Visual이 플레이어에게 회수되어 VisualRoot를 따라가는 상태다.
    private bool isRecalledVisual;
    // true면 검 소지 근접 공격용 기울기 자세를 유지하는 상태다.
    private bool isMeleePoseActive;

    /// <summary>
    /// 필수 참조와 데이터를 검사하고 연출 큐 및 전투 연출 알림을 구독한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        SubscribeCombatPresentation();
        TrySubscribeQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서가 끝난 뒤 큐 구독을 보정하고 논리 검 상태에 Visual을 맞춘다.
    /// </summary>
    private void Start()
    {
        TrySubscribeQueue(true);
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
    /// 연출 큐와 전투 연출 알림 구독을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.PresentationEventStarted -= HandlePresentationEventStarted;
        }

        if (combatActionPresenter != null)
        {
            combatActionPresenter.CombatPresentationStarted -= HandleCombatPresentationStarted;
            combatActionPresenter.CombatPresentationCompleted -= HandleCombatPresentationCompleted;
        }

        isMeleePoseActive = false;
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
                Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.PresentationEventStarted -= HandlePresentationEventStarted;
        queue.PresentationEventStarted += HandlePresentationEventStarted;
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
    /// 자신이 소유한 검의 SwordMove 이벤트만 처리하고 즉시 큐 완료 신호를 보낸다.
    /// </summary>
    private bool HandlePresentationEventStarted(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (presentationEvent.Type != PresentationEventType.SwordMove || presentationEvent.Actor != ownerActor)
        {
            return false;
        }

        if (!HasValidReference() || !HasValidData())
        {
            handle.Complete();
            return true;
        }

        PlaySwordMove(presentationEvent);
        handle.Complete();
        return true;
    }

    /// <summary>
    /// 검의 실제 현재 위치에서 이벤트 목표 위치까지 방향과 경로를 계산해 Visual을 즉시 옮긴다.
    /// </summary>
    private void PlaySwordMove(PresentationEvent presentationEvent)
    {
        GridManager gridManager = GridManager.Instance;
        if (gridManager == null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}에는 검 위치 변환에 사용할 {nameof(GridManager)}가 필요합니다.", this);
            return;
        }

        Vector3 fromWorldPosition = swordVisual.position;
        Vector3 toWorldPosition = presentationEvent.SwordMoveKind == SwordMoveKind.Recall
            ? GetRecalledWorldPosition()
            : gridManager.GridToWorld(presentationEvent.ToPosition) + deployedPositionOffset;

        PlayPathVfx(fromWorldPosition, toWorldPosition);

        switch (presentationEvent.SwordMoveKind)
        {
            case SwordMoveKind.Throw:
            case SwordMoveKind.Hack:
                DeploySword(toWorldPosition, fromWorldPosition);
                break;
            case SwordMoveKind.Recall:
                AttachSwordToPlayer();
                break;
            default:
                Debug.LogError($"{nameof(SwordActionPresenter)}: 지원하지 않는 검 이동 종류입니다. 종류: {presentationEvent.SwordMoveKind}", this);
                break;
        }

        if (logSwordFlow)
        {
            Debug.Log($"{nameof(SwordActionPresenter)}: {presentationEvent.SwordMoveKind} 검 이동 연출을 완료했습니다. 시작 칸: {presentationEvent.FromPosition}, 목표 칸: {presentationEvent.ToPosition}", this);
        }
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
        swordVisual.SetParent(ownerVisualController.transform, false);
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
    /// 검 이동 경로의 중간점, 방향, 길이를 계산해 설정된 선택 VFX를 재생한다.
    /// </summary>
    private void PlayPathVfx(Vector3 fromWorldPosition, Vector3 toWorldPosition)
    {
        if (pathVfxId == VfxId.None)
        {
            return;
        }

        Vector3 direction = toWorldPosition - fromWorldPosition;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 middlePosition = Vector3.Lerp(fromWorldPosition, toWorldPosition, 0.5f);
        Quaternion rotation = Quaternion.Euler(0f, 0f, CalculateDirectionAngle(fromWorldPosition, toWorldPosition));
        Vector3 scale = new(pathVfxWidth, distance * pathVfxLengthScale, 1f);

        if (!VfxManager.TryPlay(pathVfxId, middlePosition, rotation, scale) && logSwordFlow)
        {
            Debug.LogWarning($"{nameof(SwordActionPresenter)}: {pathVfxId} 검 경로 VFX를 재생하지 못했습니다. 씬의 {nameof(VfxManager)}와 VFX 테이블을 확인하세요.", this);
        }
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
            !swordState.IsRecalled)
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
            !swordState.IsRecalled)
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

        if (pathVfxId != VfxId.None && (pathVfxWidth <= 0f || pathVfxLengthScale <= 0f))
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}의 검 경로 VFX 두께와 길이 배율은 0보다 커야 합니다.", this);
            return false;
        }

        return true;
    }
}
