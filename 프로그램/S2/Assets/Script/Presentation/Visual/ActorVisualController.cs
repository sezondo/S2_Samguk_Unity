using UnityEngine;

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

    [Header("Debug")]
    // true면 애니메이션 요청 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAnimationRequests;

    public SpriteRenderer TargetRenderer => targetRenderer;
    public Animator Animator => animator;

    

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

        return TryPlayAnimationState(moveData.MoveAnimationStateName, moveData.MoveAnimationCrossFadeDuration);
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

        return TryPlayAnimationState(moveData.IdleAnimationStateName, moveData.IdleAnimationCrossFadeDuration);
    }

    /// <summary>
    /// Animator에 지정한 상태 재생을 요청한다.
    /// </summary>
    public bool TryPlayAnimationState(string stateName, float crossFadeDuration)
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

        if (crossFadeDuration > 0f)
        {
            animator.CrossFade(stateName, crossFadeDuration);
        }
        else
        {
            animator.Play(stateName);
        }

        if (logAnimationRequests)
        {
            Debug.Log($"{nameof(ActorVisualController)}: {stateName} 애니메이션 재생을 요청했습니다.", this);
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
