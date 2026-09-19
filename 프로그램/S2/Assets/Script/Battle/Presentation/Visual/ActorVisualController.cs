using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// VisualRoot에 붙어 SpriteRenderer와 Animator 같은 실제 시각 컴포넌트를 제어한다.
/// 연출 큐는 알지 않고, Presenter의 요청에 따라 시각 작업만 수행한다.
/// </summary>
public class ActorVisualController : MonoBehaviour
{
    // 대기 재생 요청을 낮은 엄폐 자세로 바꿀지 나타내며, 벽 엄폐보다 우선한다.
    private bool useLowCoverIdle;
    // 낮은 엄폐물이 없고 벽이 인접하면 벽 엄폐 대기를 사용한다.
    private bool useWallCoverIdle;
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
    // 건물 정렬 요청이 시작되기 전의 원래 순서다.
    private int sortingOrderBeforeBuildingOverride;
    // 건물 앞 정렬 보정이 적용 중인지 나타낸다.
    private bool hasBuildingSortingOverride;

    [Header("Ground Sorting")]
    // 애니메이션에 흔들리지 않는 발 기준점이며 VisualRoot의 로컬 좌표다.
    [SerializeField] private Vector2 groundPointLocalOffset = new Vector2(0f, -2.2f);
    public Vector3 GroundWorldPosition => transform.TransformPoint(groundPointLocalOffset);
    public bool HasVisionSortingOverride => hasVisionSortingOverride;

    /// <summary>건물별 전면 정렬 요청을 모아 적용하며 요청 해제 시 원래 순서로 복원한다.</summary>
    public void SetBuildingFrontSorting(SpriteRenderer building, bool inFront)
    {
        if (targetRenderer == null || building == null) return;
        if (inFront && building.sortingLayerID == targetRenderer.sortingLayerID)
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

    /// <summary>복수 건물의 요청을 합치되 안개 위 공격 연출의 정렬은 우선 유지한다.</summary>
    private void ApplyBuildingSorting()
    {
        if (!hasBuildingSortingOverride || targetRenderer == null) return;
        int order = sortingOrderBeforeBuildingOverride;
        foreach (var request in buildingSortingRequests)
            if (request.Key != null && request.Key.enabled && request.Key.gameObject.activeInHierarchy) order = Mathf.Max(order, request.Value);
        if (hasVisionSortingOverride) sortingOrderBeforeVisionOverride = order;
        else targetRenderer.sortingOrder = order;
        if (buildingSortingRequests.Count == 0) hasBuildingSortingOverride = false;
    }

    /// <summary>표시를 중단하면 건물의 임시 정렬 요청을 해제한다.</summary>
    private void OnDisable()
    {
        buildingSortingRequests.Clear();
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
        useLowCoverIdle = isNearLowCover;
        useWallCoverIdle = isNearWallCover;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName("Idle") || state.IsName("LowCoverIdle") || state.IsName("WallCoverIdle"))
        {
            TryPlayAnimationState("Idle", 0f);
            // 한 장짜리 대기 자세는 Animator의 다음 평가를 기다리지 않고 즉시 표시한다.
            animator.Update(0f);
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
        ApplyCompositeColor();
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

        if (crossFadeDuration > 0f)
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
        ApplyFacing(true);
    }

    /// <summary>
    /// 원본 아트를 화면 왼쪽 방향으로 표시한다.
    /// </summary>
    public void FaceLeft()
    {
        ApplyFacing(false);
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
