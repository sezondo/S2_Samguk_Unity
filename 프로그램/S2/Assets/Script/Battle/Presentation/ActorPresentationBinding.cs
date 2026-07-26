using UnityEngine;

/// <summary>
/// 논리 GridActor와 화면 표시용 ActorVisualController를 연출 등록소에 연결한다.
/// </summary>
public class ActorPresentationBinding : MonoBehaviour
{
    [Header("Reference")]
    // 이 연출 오브젝트가 화면에 표시하는 논리 Actor다.
    [SerializeField] private GridActor targetActor;
    // 대상 Actor의 스프라이트와 Animator를 제어하는 시각 컴포넌트다.
    [SerializeField] private ActorVisualController visualController;

    // 현재 연출 등록소에 정상 등록되어 있는지 나타낸다.
    private bool registered;

    /// <summary>
    /// 활성화 시점에 준비된 연출 등록소에 연결을 등록한다.
    /// </summary>
    private void OnEnable()
    {
        TryRegister(false);
    }

    /// <summary>
    /// 초기화 순서로 OnEnable에서 등록하지 못한 연결을 시작 시점에 다시 등록한다.
    /// </summary>
    private void Start()
    {
        TryRegister(true);
    }

    /// <summary>
    /// 비활성화될 때 현재 연출 등록소에서 연결을 해제한다.
    /// </summary>
    private void OnDisable()
    {
        if (registered && ActorPresentationRegistry.Instance != null)
        {
            ActorPresentationRegistry.Instance.Unregister(targetActor, visualController);
        }

        registered = false;
    }

    /// <summary>
    /// 현재 씬의 연출 등록소에 논리 Actor와 시각 컴포넌트를 등록한다.
    /// </summary>
    private void TryRegister(bool logMissingRegistry)
    {
        if (registered)
        {
            return;
        }

        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        ActorPresentationRegistry registry = ActorPresentationRegistry.Instance;
        if (registry == null)
        {
            if (logMissingRegistry)
            {
                Debug.LogError($"{nameof(ActorPresentationBinding)} on {name}에는 씬의 {nameof(ActorPresentationRegistry)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        registered = registry.Register(targetActor, visualController);
        if (!registered)
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 연출 등록에 필요한 필수 참조와 Animator 연결을 검사한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetActor == null)
        {
            Debug.LogError($"{nameof(ActorPresentationBinding)} on {name}에는 연출 기준 {nameof(GridActor)} 참조가 필요합니다.", this);
            return false;
        }

        if (visualController == null)
        {
            Debug.LogError($"{nameof(ActorPresentationBinding)} on {name}에는 {nameof(ActorVisualController)} 참조가 필요합니다.", this);
            return false;
        }

        return visualController.HasValidAnimationReference();
    }
}
