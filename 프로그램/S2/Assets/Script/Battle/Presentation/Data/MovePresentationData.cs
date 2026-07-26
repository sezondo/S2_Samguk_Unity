using UnityEngine;

/// <summary>
/// GridActor 이동 연출에 사용하는 튜닝 데이터를 보관한다.
/// 실제 유효성 검사는 이 데이터를 사용하는 Presenter가 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "MovePresentationData", menuName = "S2-T/Presentation/Move Presentation Data")]
public class MovePresentationData : ScriptableObject
{
    [Header("Move")]
    // 한 칸 또는 한 이동 이벤트를 화면에서 재생하는 시간이다.
    [SerializeField] private float moveDuration = 0.2f;
    // 이동 시간 진행률을 실제 위치 보간률로 바꾸는 곡선이다.
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Animation")]
    // true면 이동 시작 시 ActorVisualController에 이동 애니메이션 재생을 요청한다.
    [SerializeField] private bool useMoveAnimation;
    // 이동 중 재생할 Animator 상태 이름이다.
    [SerializeField] private string moveAnimationStateName = "Move";
    // 이동 애니메이션을 전환할 때 사용할 CrossFade 시간이다. 0 이하이면 Play를 사용한다.
    [SerializeField] private float moveAnimationCrossFadeDuration = 0.05f;
    // true면 이동 종료 시 ActorVisualController에 대기 애니메이션 재생을 요청한다.
    [SerializeField] private bool playIdleAnimationOnComplete;
    // 이동 종료 후 재생할 Animator 상태 이름이다.
    [SerializeField] private string idleAnimationStateName = "Idle";
    // 대기 애니메이션으로 전환할 때 사용할 CrossFade 시간이다. 0 이하이면 Play를 사용한다.
    [SerializeField] private float idleAnimationCrossFadeDuration = 0.05f;

    public float MoveDuration => moveDuration;
    public AnimationCurve MoveCurve => moveCurve;
    public bool UseMoveAnimation => useMoveAnimation;
    public string MoveAnimationStateName => moveAnimationStateName;
    public float MoveAnimationCrossFadeDuration => moveAnimationCrossFadeDuration;
    public bool PlayIdleAnimationOnComplete => playIdleAnimationOnComplete;
    public string IdleAnimationStateName => idleAnimationStateName;
    public float IdleAnimationCrossFadeDuration => idleAnimationCrossFadeDuration;
}
