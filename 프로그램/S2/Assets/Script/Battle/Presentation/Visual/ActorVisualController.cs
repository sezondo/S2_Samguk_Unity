using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// VisualRoot에 붙어 SpriteRenderer와 Animator 같은 실제 시각 컴포넌트를 제어한다.
/// 연출 큐는 알지 않고, Presenter의 요청에 따라 시각 작업만 수행한다.
/// </summary>
public class ActorVisualController : MonoBehaviour
{
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

    public SpriteRenderer TargetRenderer => targetRenderer;
    public Animator Animator => animator;
    public string CurrentAnimationStateName => currentAnimationStateName;
    public bool IsFacingRight { get; private set; }
    public float VisionAlpha => visionAlpha;

    /// <summary>
    /// 시작 시 SpriteRenderer의 현재 반전값을 화면상 바라보는 방향으로 변환한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
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
    /// Animator에 지정한 상태 재생을 요청한다. 같은 상태를 유지할 때 재시작 여부를 선택할 수 있다.
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

    /// <summary>
    /// VisualRoot에 필요한 기본 시각 컴포넌트 참조가 연결되어 있는지 확인한다.
    /// </summary>
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
