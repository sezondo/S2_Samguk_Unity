using System.Collections;
using UnityEngine;

/// <summary>
/// 담당 적의 발각 연출 이벤트를 받아 경고색을 점멸하고 큐 완료 신호를 보내는 Presenter다.
/// 발각 판정과 상태 변경에는 관여하지 않는다.
/// </summary>
public class AlertDetectedPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Target")]
    // 이 Presenter가 발각 연출을 처리할 적 Context다.
    [SerializeField] private EnemyContext targetEnemy;
    // 실제 스프라이트와 애니메이션을 제어할 Actor 시각 컴포넌트다.
    [SerializeField] private ActorVisualController visualController;

    [Header("Presentation")]
    // 평상 상태일 때 적용할 색이다.
    [SerializeField] private Color normalColor = Color.white;
    // 의심 상태일 때 적용할 색이다.
    [SerializeField] private Color suspiciousColor = new(1f, 0.75f, 0.15f, 1f);
    // 발각 상태일 때 적용할 색이다.
    [SerializeField] private Color alertedColor = new(1f, 0.25f, 0.2f, 1f);
    // 발각 순간 점멸에 사용할 경고색이다.
    [SerializeField] private Color warningColor = Color.white;
    // 경고색과 현재 상태 색상을 각각 유지할 시간이다.
    [SerializeField] private float flashInterval = 0.12f;
    // 발각 연출 중 경고색을 표시할 횟수다.
    [SerializeField] private int flashCount = 3;

    [Header("Animation")]
    // true면 발각 점멸과 함께 한 프레임 발각 자세를 재생한다.
    [SerializeField] private bool playAlertAnimation;
    // 발각 연출 중 재생할 Animator 상태 이름이다.
    [SerializeField] private string alertAnimationStateName = "Alert";
    // 발각 연출이 끝난 뒤 복귀할 Animator 상태 이름이다.
    [SerializeField] private string idleAnimationStateName = "Idle";

    [Header("Debug")]
    // true면 발각 연출 시작과 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logAlertFlow;

    // 현재 실행 중인 발각 연출 코루틴이다.
    private Coroutine alertCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;

    /// <summary>
    /// 필수 참조와 연출 데이터를 검사하고 연출 큐 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        ApplyCurrentStateColor();
        TryRegisterQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 등록을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 큐 등록을 해제하고 진행 중인 이벤트가 있으면 큐 정지를 막기 위해 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (alertCoroutine != null)
        {
            StopCoroutine(alertCoroutine);
            alertCoroutine = null;
        }

        if (visualController != null && targetEnemy != null && targetEnemy.AlertState != null)
        {
            ApplyCurrentStateColor();
        }

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
            activeHandle = null;
        }
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
                Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 발각 연출 이벤트 중 자신이 담당하는 적 대상 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return (presentationEvent.Type == PresentationEventType.AlertDetected ||
                presentationEvent.Type == PresentationEventType.SuspicionDetected) &&
               presentationEvent.Enemy == targetEnemy;
    }

    /// <summary>
    /// 담당 적의 발각 연출을 시작한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (alertCoroutine != null)
        {
            Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}은 이미 발각 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return;
        }

        if (!HasValidReference() || !HasValidData())
        {
            handle.Complete();
            return;
        }

        alertCoroutine = StartCoroutine(PlayAlertDetected(presentationEvent, handle));
    }

    /// <summary>
    /// 경고색과 현재 경계 상태 색상을 번갈아 표시한 뒤 완료 신호를 보낸다.
    /// </summary>
    private IEnumerator PlayAlertDetected(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        activeHandle = handle;

        if (logAlertFlow)
        {
            Debug.Log($"{nameof(AlertDetectedPresenter)}: {targetEnemy.name}의 발각 연출을 시작합니다. 발각 칸: {presentationEvent.EventPosition}", this);
        }

        if (playAlertAnimation && presentationEvent.Type == PresentationEventType.AlertDetected)
        {
            visualController.TryPlayAnimationState(alertAnimationStateName, 0f);
        }

        for (int i = 0; i < flashCount; i++)
        {
            visualController.ApplyColor(warningColor);
            yield return new WaitForSeconds(flashInterval);

            ApplyCurrentStateColor();
            yield return new WaitForSeconds(flashInterval);
        }

        if (logAlertFlow)
        {
            Debug.Log($"{nameof(AlertDetectedPresenter)}: {targetEnemy.name}의 발각 연출을 완료했습니다.", this);
        }

        CompleteActiveAlert(handle);
    }

    /// <summary>
    /// 현재 발각 연출 상태를 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveAlert(PresentationEventHandle handle)
    {
        ApplyCurrentStateColor();
        if (playAlertAnimation)
        {
            visualController.TryPlayAnimationState(idleAnimationStateName, 0f, false);
        }

        alertCoroutine = null;
        activeHandle = null;
        handle.Complete();
    }

    /// <summary>
    /// 발각 연출에 필요한 필수 컴포넌트 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (targetEnemy == null)
        {
            Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}에는 발각 연출 대상 {nameof(EnemyContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (visualController == null)
        {
            Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}에는 {nameof(ActorVisualController)} 참조가 필요합니다.", this);
            return false;
        }

        if (!visualController.HasValidReference())
        {
            return false;
        }

        if (playAlertAnimation && !visualController.HasValidAnimationReference())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 발각 점멸 연출에 사용할 데이터가 유효한지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (flashInterval <= 0f)
        {
            Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}의 점멸 간격은 0보다 커야 합니다.", this);
            return false;
        }

        if (flashCount <= 0)
        {
            Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}의 점멸 횟수는 1 이상이어야 합니다.", this);
            return false;
        }

        if (playAlertAnimation &&
            (string.IsNullOrWhiteSpace(alertAnimationStateName) || string.IsNullOrWhiteSpace(idleAnimationStateName)))
        {
            Debug.LogError($"{nameof(AlertDetectedPresenter)} on {name}의 발각 또는 대기 애니메이션 상태 이름이 비어 있습니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 담당 적의 현재 논리 경계 상태에 맞는 색상을 적용한다.
    /// </summary>
    private void ApplyCurrentStateColor()
    {
        Color stateColor = targetEnemy.AlertState.IsAlerted
            ? alertedColor
            : targetEnemy.AlertState.IsSuspicious
                ? suspiciousColor
                : normalColor;
        visualController.ApplyColor(stateColor);
    }
}
