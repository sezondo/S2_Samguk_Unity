using UnityEngine;

/// <summary>
/// 편대가 한 칸씩 동시에 이동할 때 사용하는 공통 연출 설정을 보관한다.
/// 실제 유효성 검사는 이 데이터를 사용하는 GroupMovePresenter가 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "GroupMovePresentationData", menuName = "S2-T/Presentation/Group Move Presentation Data")]
public class GroupMovePresentationData : ScriptableObject
{
    [Header("Move")]
    // 편대 전체가 한 칸을 이동하는 데 사용하는 공통 시간이다.
    [SerializeField] private float moveDurationPerCell = 0.2f;
    // 한 칸 이동 시간의 진행률을 실제 위치 보간률로 바꾸는 공통 곡선이다.
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Animation")]
    // true면 편대 이동 시작 시 각 구성원에게 이동 애니메이션을 요청한다.
    [SerializeField] private bool useMoveAnimation;
    // 편대 이동 중 재생할 Animator 상태 이름이다.
    [SerializeField] private string moveAnimationStateName = "Move";
    // 이동 애니메이션 전환 시간이다. 0 이하이면 즉시 재생한다.
    [SerializeField] private float moveAnimationCrossFadeDuration = 0.05f;
    // true면 편대 이동 완료 시 각 구성원에게 대기 애니메이션을 요청한다.
    [SerializeField] private bool playIdleAnimationOnComplete;
    // 편대 이동 완료 후 재생할 Animator 상태 이름이다.
    [SerializeField] private string idleAnimationStateName = "Idle";
    // 대기 애니메이션 전환 시간이다. 0 이하이면 즉시 재생한다.
    [SerializeField] private float idleAnimationCrossFadeDuration = 0.05f;

    public float MoveDurationPerCell => moveDurationPerCell;
    public AnimationCurve MoveCurve => moveCurve;
    public bool UseMoveAnimation => useMoveAnimation;
    public string MoveAnimationStateName => moveAnimationStateName;
    public float MoveAnimationCrossFadeDuration => moveAnimationCrossFadeDuration;
    public bool PlayIdleAnimationOnComplete => playIdleAnimationOnComplete;
    public string IdleAnimationStateName => idleAnimationStateName;
    public float IdleAnimationCrossFadeDuration => idleAnimationCrossFadeDuration;
}
