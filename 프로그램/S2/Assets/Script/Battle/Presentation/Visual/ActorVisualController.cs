using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// VisualRoot에 붙어 SpriteRenderer와 Animator 같은 실제 시각 컴포넌트를 제어한다.
/// 연출 큐는 알지 않고, Presenter의 요청에 따라 시각 작업만 수행한다.
/// </summary>
public class ActorVisualController : MonoBehaviour
{
    // 선택된 엄폐 대상이 낮은 엄폐여서 대기 자세를 바꿀지 나타낸다.
    private bool useLowCoverIdle;
    // 선택된 엄폐 대상이 벽이면 높은 엄폐 대기를 사용한다.
    private bool useWallCoverIdle;
    // 이동 등 일반 요청의 마지막 좌우 방향이며 엄폐/공격 방향과 분리한다.
    private bool requestedFacingRight;
    // 현재 엄폐 자세에 고정할 좌우 방향이다. 위아래 엄폐 진입 때는 현재 방향을 보존한다.
    private bool coverFacingRight;
    // 이동 연출 중 엄폐 재부착을 막는 상태다.
    private bool isMovingPresentation;
    // 전투 중 접촉 위치 보간을 잠시 정지하는 상태다.
    private bool isCombatPresentation;
    // 공격자의 방향만 엄폐 방향보다 우선하는지 나타낸다.
    private bool hasCombatFacing;
    // 공격 연출에서 임시로 바라볼 방향이다.
    private bool combatFacingRight;
    // 사망 그림의 위치를 고정하며 이후 엄폐 재진입을 막는다.
    private bool isDeathPresentation;
    // 논리/이동 기준 위치에 더하는 현재 월드 표시 보정이다.
    private Vector3 coverWorldOffset;
    // 이번 부착/해제 보간의 시작·목표 오프셋이다.
    private Vector3 coverOffsetStart;
    private Vector3 coverOffsetTarget;
    // 부착/해제 보간의 경과 시간과 설정 시간이다.
    private float coverTransitionElapsed;
    private float coverTransitionDuration = 0.15f;
    // 선택된 엄폐물의 좌우 구분이다. 0은 위아래 엄폐다.
    private int coverSide;

    // Idle을 실제 재생하기 전에 표시 위치에 맞는 엄폐 선택을 동기화하는 알림이다.
    public event Action IdleRequested;
    // 엄폐 선택 콜백에서 Idle 재생을 중복 요청하지 않게 하는 상태다.
    private bool resolvingIdleRequest;
    // 소품별 앞/뒤 정렬 요청이다. 뒤쪽 요청은 건물 전면 보정보다 낮은 상한을 적용한다.
    private readonly Dictionary<SpriteRenderer, bool> propSortingRequests = new();

    public Vector3 CoverWorldOffset => coverWorldOffset;
    public bool IsMovingPresentation => isMovingPresentation;
    public bool IsDeathPresentation => isDeathPresentation;

    [Header("Visual Components")]
    // Actor를 화면에 표시하는 스프라이트 렌더러다.
    [SerializeField] private SpriteRenderer targetRenderer;
    // Actor 애니메이션을 재생하는 Animator다. 애니메이션 요청을 사용할 때 필요하다.
    [SerializeField] private Animator animator;

    [Header("Facing")]
    // 원본 아트가 기본적으로 오른쪽을 바라보는지 나타낸다.
    [FormerlySerializedAs("artworkFacesLeft")]
    [SerializeField] private bool artworkFacesRight = true;

    [Header("Debug")]
    // true면 애니메이션 요청 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAnimationRequests;

    // 마지막으로 재생을 요청한 Animator 상태 이름이다.
    private string currentAnimationStateName;
    // 경계·피격 같은 기존 색상 연출이 요청한 원본 표시 색이다.
    private Color presentationColor = Color.white;
    // 플레이어 시야가 적용하는 별도 표시 알파다. 기존 색상 연출 알파와 곱해서 사용한다.
    private float visionAlpha = 1f;
    // 시야 밖 공격자 임시 노출 전에 사용하던 SpriteRenderer Sorting Layer ID다.
    private int sortingLayerIdBeforeVisionOverride;
    // 시야 밖 공격자 임시 노출 전에 사용하던 SpriteRenderer Sorting Order다.
    private int sortingOrderBeforeVisionOverride;
    // 공격자 임시 노출용 정렬 순서 덮어쓰기가 현재 적용돼 있는지 나타낸다.
    private bool hasVisionSortingOverride;
    // 건물 앞에 서 있을 때 필요한 정렬 요청을 건물별로 보관한다.
    private readonly Dictionary<SpriteRenderer, int> buildingSortingRequests = new();
    // 건물·소품 정렬 요청이 시작되기 전의 원래 순서다.
    private int sortingOrderBeforeBuildingOverride;
    // 건물 또는 소품의 임시 정렬 보정이 적용 중인지 나타낸다.
    private bool hasBuildingSortingOverride;

    [Header("Ground Sorting")]
    // 애니메이션에 흔들리지 않는 발 기준점이며 VisualRoot의 로컬 좌표다.
    [SerializeField] private Vector2 groundPointLocalOffset = new Vector2(0f, -2.2f);
    public Vector3 GroundWorldPosition => transform.TransformPoint(groundPointLocalOffset);
    // 이미지의 투명 여백과 무관한 머리 위 UI 기준점이며 VisualRoot 로컬 좌표다.
    [SerializeField] private Vector2 headPointLocalOffset = new(0f, 2.2f);
    public Vector3 HeadWorldPosition => transform.TransformPoint(headPointLocalOffset);
    public bool HasVisionSortingOverride => hasVisionSortingOverride;

    /// <summary>건물별 전면 정렬 요청을 모아 적용하며 요청 해제 시 원래 순서로 복원한다.</summary>
    public void SetBuildingFrontSorting(SpriteRenderer building, bool inFront)
    {
        if (targetRenderer == null || building == null) return;
        if (inFront && building.sortingLayerID == (hasVisionSortingOverride ? sortingLayerIdBeforeVisionOverride : targetRenderer.sortingLayerID))
        {
            if (!hasBuildingSortingOverride)
            {
                sortingOrderBeforeBuildingOverride = hasVisionSortingOverride ? sortingOrderBeforeVisionOverride : targetRenderer.sortingOrder;
                hasBuildingSortingOverride = true;
            }
            buildingSortingRequests[building] = building.sortingOrder + 1;
        }
        else buildingSortingRequests.Remove(building);
        ApplyBuildingSorting();
    }

    /// <summary>소품 앞/뒤의 상대 정렬을 요청하거나 null로 해제한다. 위치와 알파는 변경하지 않는다.</summary>
    public void SetPropDepthSorting(SpriteRenderer prop, bool? inFront)
    {
        if (targetRenderer == null || prop == null) return;
        int layer = hasVisionSortingOverride ? sortingLayerIdBeforeVisionOverride : targetRenderer.sortingLayerID;
        if (inFront.HasValue && prop.sortingLayerID == layer)
        {
            if (!hasBuildingSortingOverride)
            {
                sortingOrderBeforeBuildingOverride = hasVisionSortingOverride ? sortingOrderBeforeVisionOverride : targetRenderer.sortingOrder;
                hasBuildingSortingOverride = true;
            }
            propSortingRequests[prop] = inFront.Value;
        }
        else propSortingRequests.Remove(prop);
        ApplyBuildingSorting();
    }

    /// <summary>건물 전면과 소품 앞/뒤 요청을 합치되 안개 위 공격 표시를 최우선으로 유지한다.</summary>
    private void ApplyBuildingSorting()
    {
        if (!hasBuildingSortingOverride || targetRenderer == null) return;
        int order = sortingOrderBeforeBuildingOverride;
        foreach (var request in buildingSortingRequests)
            if (request.Key != null && request.Key.enabled && request.Key.gameObject.activeInHierarchy) order = Mathf.Max(order, request.Value);
        int behindLimit = int.MaxValue;
        foreach (var request in propSortingRequests)
        {
            SpriteRenderer prop = request.Key;
            if (prop == null || !prop.enabled || !prop.gameObject.activeInHierarchy) continue;
            if (request.Value) order = Mathf.Max(order, prop.sortingOrder + 1);
            else behindLimit = Mathf.Min(behindLimit, prop.sortingOrder - 1);
        }
        // 뒤에 선 소품보다 앞서 그려지지 않도록 건물 전면 보정에도 상한을 적용한다.
        order = Mathf.Min(order, behindLimit);
        if (hasVisionSortingOverride) sortingOrderBeforeVisionOverride = order;
        else targetRenderer.sortingOrder = order;
        if (buildingSortingRequests.Count == 0 && propSortingRequests.Count == 0) hasBuildingSortingOverride = false;
    }

    /// <summary>표시를 중단하면 건물·소품의 임시 정렬 요청을 해제한다.</summary>
    private void OnDisable()
    {
        // 재활성화 시 이전 표시 보정이 누적되지 않게 되돌린다.
        transform.position -= coverWorldOffset;
        coverWorldOffset = coverOffsetStart = coverOffsetTarget = Vector3.zero;
        useLowCoverIdle = useWallCoverIdle = false;
        isMovingPresentation = isCombatPresentation = hasCombatFacing = isDeathPresentation = false;
        buildingSortingRequests.Clear();
        propSortingRequests.Clear();
        ApplyBuildingSorting();
    }

    public SpriteRenderer TargetRenderer => targetRenderer;
    public Animator Animator => animator;
    public string CurrentAnimationStateName => currentAnimationStateName;
    public bool IsFacingRight { get; private set; }
    public float VisionAlpha => visionAlpha;

    /// <summary>낮은 엄폐·벽 엄폐 여부를 저장하고 현재 대기 중일 때만 자세를 즉시 갱신한다.</summary>
    public void SetCoverIdle(bool isNearLowCover, bool isNearWallCover)
    {
        if (isDeathPresentation) return;
        if (useLowCoverIdle == isNearLowCover && useWallCoverIdle == isNearWallCover) return;
        useLowCoverIdle = isNearLowCover;
        useWallCoverIdle = isNearWallCover;
        if (resolvingIdleRequest) return;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Idle") || state.IsName("LowCoverIdle") || state.IsName("WallCoverIdle"))
        {
            string idle = useLowCoverIdle ? "LowCoverIdle" : useWallCoverIdle ? "WallCoverIdle" : "Idle";
            // 이미 선택된 자세만 적용한다. 다시 선택하면 외부 갱신과 재귀적으로 충돌한다.
            resolvingIdleRequest = true;
            try { TryPlayAnimationState(idle, 0f); }
            finally { resolvingIdleRequest = false; }
        }
    }

    /// <summary>일반 대기와 낮은 엄폐·벽 엄폐 대기 상태가 연결되어 있는지 확인한다.</summary>
    public bool HasValidCoverIdleAnimation()
    {
        if (!HasValidAnimationReference())
        {
            return false;
        }

        if (animator.runtimeAnimatorController != null &&
            animator.HasState(0, Animator.StringToHash("Base Layer.Idle")) &&
            animator.HasState(0, Animator.StringToHash("Base Layer.LowCoverIdle")) &&
            animator.HasState(0, Animator.StringToHash("Base Layer.WallCoverIdle")))
        {
            return true;
        }

        Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 Idle·LowCoverIdle·WallCoverIdle 상태가 있는 Animator Controller가 필요합니다.", this);
        return false;
    }

    /// <summary>
    /// 시작 시 SpriteRenderer의 현재 반전값을 화면상 바라보는 방향으로 변환한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        presentationColor = targetRenderer.color;
        IsFacingRight = artworkFacesRight ? !targetRenderer.flipX : targetRenderer.flipX;
        requestedFacingRight = IsFacingRight;
        coverFacingRight = IsFacingRight;
        ApplyCompositeColor();
    }

    /// <summary>이동 기준 월드 위치에 현재 엄폐 보정만 합쳐 표시한다.</summary>
    public void SetPresentationPosition(Vector3 worldPosition)
    {
        transform.position = worldPosition + coverWorldOffset;
    }

    /// <summary>선택된 엄폐물의 자세·방향·접촉 오프셋을 하나의 요청으로 적용한다.</summary>
    public void SetCoverPresentation(bool low, bool wall, Vector3 offset, int horizontalSide, float duration)
    {
        if (isDeathPresentation || isMovingPresentation) return;
        bool hadCover = useLowCoverIdle || useWallCoverIdle;
        bool hasCover = low || wall;
        if (hasCover && (!hadCover || coverSide != horizontalSide))
            coverFacingRight = horizontalSide == 0 ? IsFacingRight : horizontalSide > 0;
        coverSide = horizontalSide;
        SetCoverIdle(low, wall);
        SetCoverOffsetTarget(hasCover ? offset : Vector3.zero, duration);
        ApplyRequestedFacing();
    }

    /// <summary>같은 목표 요청은 보간을 재시작하지 않고 새 목표만 저장한다.</summary>
    private void SetCoverOffsetTarget(Vector3 target, float duration)
    {
        if ((coverOffsetTarget - target).sqrMagnitude < 0.00000001f) return;
        coverOffsetStart = coverWorldOffset;
        coverOffsetTarget = target;
        coverTransitionElapsed = 0f;
        coverTransitionDuration = duration;
    }

    /// <summary>논리 위치와 무관하게 현재 그림의 엄폐 오프셋만 부드럽게 보간한다.</summary>
    private void Update()
    {
        if (isDeathPresentation || isCombatPresentation || coverWorldOffset == coverOffsetTarget) return;
        coverTransitionElapsed += Time.deltaTime;
        float time = coverTransitionDuration <= 0f ? 1f : Mathf.Clamp01(coverTransitionElapsed / coverTransitionDuration);
        Vector3 next = Vector3.Lerp(coverOffsetStart, coverOffsetTarget, Mathf.SmoothStep(0f, 1f, time));
        transform.position += next - coverWorldOffset;
        coverWorldOffset = next;
    }

    /// <summary>이동 시작 즉시 엄폐 방향·자세 고정을 풀고 현재 보정을 부드럽게 해제한다.</summary>
    public void BeginMovePresentation()
    {
        if (isDeathPresentation) return;
        isMovingPresentation = true;
        useLowCoverIdle = useWallCoverIdle = false;
        isCombatPresentation = hasCombatFacing = false;
        SetCoverOffsetTarget(Vector3.zero, coverTransitionDuration);
        ApplyRequestedFacing();
    }

    /// <summary>이동 종료 후 큐가 비면 엄폐 Presenter가 새 칸을 평가할 수 있게 한다.</summary>
    public void EndMovePresentation()
    {
        isMovingPresentation = false;
    }

    /// <summary>전투 중 위치는 유지하며 공격자만 엄폐보다 우선해 목표를 바라본다.</summary>
    public void BeginCombatPresentation(GridPosition from, GridPosition to, bool attacker, bool killed)
    {
        isCombatPresentation = true;
        hasCombatFacing = attacker;
        combatFacingRight = to.x == from.x ? IsFacingRight : to.x > from.x;
        // 엄폐하지 않은 공격자는 기존처럼 공격 방향을 일반 대기에도 유지한다.
        if (attacker && !useLowCoverIdle && !useWallCoverIdle) requestedFacingRight = combatFacingRight;
        if (killed)
        {
            isDeathPresentation = true;
            useLowCoverIdle = useWallCoverIdle = false;
            hasCombatFacing = false;
            requestedFacingRight = IsFacingRight;
            coverOffsetStart = coverOffsetTarget = coverWorldOffset;
        }
        else if (!attacker) FaceFromTo(from, to);
        ApplyRequestedFacing();
    }

    /// <summary>생존자의 공격 방향 우선권을 해제하고 엄폐 또는 일반 방향으로 복귀한다.</summary>
    public void EndCombatPresentation()
    {
        if (isDeathPresentation) return;
        isCombatPresentation = hasCombatFacing = false;
        ApplyRequestedFacing();
    }

    /// <summary>공격 → 엄폐 → 일반 요청 순서로 실제 좌우 방향을 결정한다.</summary>
    private void ApplyRequestedFacing()
    {
        bool facing = hasCombatFacing ? combatFacingRight :
            (useLowCoverIdle || useWallCoverIdle) && !isMovingPresentation ? coverFacingRight : requestedFacingRight;
        ApplyFacing(facing);
    }

    /// <summary>
    /// Presenter가 요청한 색상을 Actor 스프라이트에 적용한다.
    /// </summary>
    public void ApplyColor(Color color)
    {
        presentationColor = color;
        ApplyCompositeColor();
    }

    /// <summary>
    /// 기존 경계·피격 색상은 보존하면서 플레이어 시야에 따른 표시 알파를 적용한다.
    /// </summary>
    public void SetVisionAlpha(float alpha)
    {
        visionAlpha = Mathf.Clamp01(alpha);
        ApplyCompositeColor();
    }

    /// <summary>
    /// 시야 밖 공격 연출 동안 Actor Sprite를 Fog보다 위에 표시하고 기존 정렬 값을 보존한다.
    /// </summary>
    public void BeginVisionSortingOverride(string sortingLayerName, int sortingOrder)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (!hasVisionSortingOverride)
        {
            sortingLayerIdBeforeVisionOverride = targetRenderer.sortingLayerID;
            sortingOrderBeforeVisionOverride = targetRenderer.sortingOrder;
            hasVisionSortingOverride = true;
        }

        targetRenderer.sortingLayerName = sortingLayerName;
        targetRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>
    /// 시야 밖 공격자 임시 노출이 끝난 뒤 Actor Sprite의 원래 정렬 값을 복원한다.
    /// </summary>
    public void EndVisionSortingOverride()
    {
        if (targetRenderer == null || !hasVisionSortingOverride)
        {
            return;
        }

        targetRenderer.sortingLayerID = sortingLayerIdBeforeVisionOverride;
        targetRenderer.sortingOrder = sortingOrderBeforeVisionOverride;
        hasVisionSortingOverride = false;
    }

    /// <summary>
    /// 연출 색상과 시야 알파를 합쳐 실제 SpriteRenderer 색상에 적용한다.
    /// </summary>
    private void ApplyCompositeColor()
    {
        if (targetRenderer == null)
        {
            return;
        }

        Color finalColor = presentationColor;
        finalColor.a *= visionAlpha;
        targetRenderer.color = finalColor;
    }

    /// <summary>
    /// 이동 연출 시작 시 필요한 애니메이션을 재생한다.
    /// </summary>
    public bool TryPlayMoveAnimation(MovePresentationData moveData)
    {
        if (moveData == null)
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 이동 애니메이션 요청에 사용할 {nameof(MovePresentationData)}가 필요합니다.", this);
            return false;
        }

        if (!moveData.UseMoveAnimation)
        {
            return true;
        }

        return TryPlayAnimationState(moveData.MoveAnimationStateName, moveData.MoveAnimationCrossFadeDuration, false);
    }

    /// <summary>
    /// 이동 연출 종료 시 필요한 애니메이션을 재생한다.
    /// </summary>
    public bool TryCompleteMoveAnimation(MovePresentationData moveData)
    {
        if (moveData == null)
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 이동 애니메이션 종료 요청에 사용할 {nameof(MovePresentationData)}가 필요합니다.", this);
            return false;
        }

        if (!moveData.PlayIdleAnimationOnComplete)
        {
            return true;
        }

        return TryPlayAnimationState(moveData.IdleAnimationStateName, moveData.IdleAnimationCrossFadeDuration, false);
    }

    /// <summary>
    /// Animator 상태 재생을 요청한다. Idle 요청은 엄폐 여부에 맞게 바꾸고 같은 상태의 재시작 여부를 적용한다.
    /// </summary>
    public bool TryPlayAnimationState(string stateName, float crossFadeDuration, bool restartIfSameState = true)
    {
        if (animator == null)
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 애니메이션 재생에 사용할 {nameof(Animator)} 참조가 필요합니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(stateName))
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 재생할 Animator 상태 이름이 필요합니다.", this);
            return false;
        }

        if (stateName == "Idle" && !resolvingIdleRequest)
        {
            resolvingIdleRequest = true;
            try { IdleRequested?.Invoke(); }
            finally { resolvingIdleRequest = false; }
        }

        if (stateName == "Idle" && useLowCoverIdle)
        {
            stateName = "LowCoverIdle";
        }
        else if (stateName == "Idle" && useWallCoverIdle)
        {
            stateName = "WallCoverIdle";
        }

        if (!restartIfSameState && currentAnimationStateName == stateName)
        {
            return true;
        }

        bool isIdle = stateName == "Idle" || stateName == "LowCoverIdle" || stateName == "WallCoverIdle";
        if (isIdle)
        {
            // 한 장짜리 Sprite 자세는 블렌딩하지 않고 확정된 최종 자세를 같은 프레임에 평가한다.
            animator.Play(stateName, 0, 0f);
            animator.Update(0f);
        }
        else if (crossFadeDuration > 0f)
        {
            animator.CrossFade(stateName, crossFadeDuration);
        }
        else
        {
            animator.Play(stateName);
        }

        currentAnimationStateName = stateName;

        if (logAnimationRequests)
        {
            Debug.Log($"{nameof(ActorVisualController)}: {stateName} 애니메이션 재생을 요청했습니다.", this);
        }

        return true;
    }

    /// <summary>
    /// 두 그리드 위치의 수평 차이를 기준으로 바라보는 방향을 갱신한다.
    /// 수직 이동처럼 X 좌표가 같으면 마지막 좌우 방향을 유지한다.
    /// </summary>
    public void FaceFromTo(GridPosition fromPosition, GridPosition toPosition)
    {
        if (toPosition.x > fromPosition.x)
        {
            FaceRight();
        }
        else if (toPosition.x < fromPosition.x)
        {
            FaceLeft();
        }
    }

    /// <summary>
    /// 원본 아트를 화면 오른쪽 방향으로 표시한다.
    /// </summary>
    public void FaceRight()
    {
        requestedFacingRight = true;
        ApplyRequestedFacing();
    }

    /// <summary>
    /// 원본 아트를 화면 왼쪽 방향으로 표시한다.
    /// </summary>
    public void FaceLeft()
    {
        requestedFacingRight = false;
        ApplyRequestedFacing();
    }

    /// <summary>
    /// 지정한 좌우 방향을 SpriteRenderer 반전 상태에 적용한다.
    /// </summary>
    private void ApplyFacing(bool faceRight)
    {
        IsFacingRight = faceRight;
        targetRenderer.flipX = artworkFacesRight ? !faceRight : faceRight;
    }

    /// <summary>
    /// Animator를 사용하는 연출에 필요한 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidAnimationReference()
    {
        if (!HasValidReference())
        {
            return false;
        }

        if (animator == null)
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 애니메이션 재생에 사용할 {nameof(Animator)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>고정 발 기준점의 좌표가 유효한지 검사한다.</summary>
    public bool HasValidData()
    {
        if (float.IsNaN(groundPointLocalOffset.x) || float.IsInfinity(groundPointLocalOffset.x) ||
            float.IsNaN(groundPointLocalOffset.y) || float.IsInfinity(groundPointLocalOffset.y))
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}의 발 기준 좌표가 유효하지 않습니다.", this);
            return false;
        }
        return true;
    }

    /// <summary>VisualRoot에 필요한 기본 시각 컴포넌트 참조가 연결되어 있는지 확인한다.</summary>
    public bool HasValidReference()
    {
        if (targetRenderer == null)
        {
            Debug.LogError($"{nameof(ActorVisualController)} on {name}에는 {nameof(SpriteRenderer)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
