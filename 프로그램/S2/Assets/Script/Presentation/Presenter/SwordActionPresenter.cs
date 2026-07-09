using System.Collections;
using UnityEngine;

/// <summary>
/// 검 투척과 회수, 검 투척 공격 연출 이벤트를 받아 임시 대기/로그 연출을 처리하는 Presenter다.
/// 실제 직선 이펙트, 검 위치 표시, 검 투척 피격/사망 애니메이션은 후속 아트 작업에서 이 컴포넌트를 확장해 연결한다.
/// </summary>
public class SwordActionPresenter : MonoBehaviour
{
    [Header("Target")]
    // 이 Presenter가 처리할 플레이어 논리 Actor다.
    [SerializeField] private GridActor ownerActor;

    [Header("Presentation")]
    // 임시 검 투척 연출 대기 시간이다.
    [SerializeField] private float throwDuration = 0.15f;
    // 임시 검 회수 연출 대기 시간이다.
    [SerializeField] private float recallDuration = 0.15f;

    [Header("Debug")]
    // true면 검 연출 시작과 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logSwordFlow = true;

    // 현재 실행 중인 검 연출 코루틴이다.
    private Coroutine swordCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;

    /// <summary>
    /// 필수 참조를 검사하고 연출 큐 구독을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        TrySubscribeQueue(false);
    }

    /// <summary>
    /// 씬 초기화 순서 때문에 OnEnable에서 놓친 큐 구독을 시작 시점에 한 번 더 시도한다.
    /// </summary>
    private void Start()
    {
        TrySubscribeQueue(true);
    }

    /// <summary>
    /// 큐 구독을 해제하고 진행 중인 이벤트가 있으면 큐 정지를 막기 위해 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.PresentationEventStarted -= HandlePresentationEventStarted;
        }

        if (swordCoroutine != null)
        {
            StopCoroutine(swordCoroutine);
            swordCoroutine = null;
        }

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
            activeHandle = null;
        }
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
    /// 검 연출 이벤트 중 자신이 담당하는 플레이어 Actor 이벤트만 처리한다.
    /// </summary>
    private bool HandlePresentationEventStarted(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        bool isSwordEvent = presentationEvent.Type == PresentationEventType.SwordThrow ||
            presentationEvent.Type == PresentationEventType.SwordRecall ||
            (presentationEvent.Type == PresentationEventType.CombatAction &&
                presentationEvent.AttackKind == AttackPresentationKind.SwordThrow);
        if (!isSwordEvent || presentationEvent.Actor != ownerActor)
        {
            return false;
        }

        if (swordCoroutine != null)
        {
            Debug.LogError($"{nameof(SwordActionPresenter)} on {name}은 이미 검 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return true;
        }

        if (!HasValidReference())
        {
            handle.Complete();
            return true;
        }

        swordCoroutine = StartCoroutine(PlaySwordEvent(presentationEvent, handle));
        return true;
    }

    /// <summary>
    /// 임시 검 연출을 처리하고 완료 신호를 보낸다.
    /// </summary>
    private IEnumerator PlaySwordEvent(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        activeHandle = handle;

        if (logSwordFlow)
        {
            Debug.Log($"{nameof(SwordActionPresenter)}: {presentationEvent.Type} 연출을 시작합니다. 시작 칸: {presentationEvent.FromPosition}, 목표 칸: {presentationEvent.ToPosition}", this);
            LogCombatResult(presentationEvent);
        }

        float duration = GetDuration(presentationEvent);
        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
        }

        if (logSwordFlow)
        {
            Debug.Log($"{nameof(SwordActionPresenter)}: {presentationEvent.Type} 연출을 완료했습니다.", this);
        }

        CompleteActiveSwordEvent(handle);
    }

    /// <summary>
    /// 현재 검 연출 상태를 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveSwordEvent(PresentationEventHandle handle)
    {
        swordCoroutine = null;
        activeHandle = null;
        handle.Complete();
    }

    /// <summary>
    /// 연출 이벤트 타입에 맞는 임시 대기 시간을 반환한다.
    /// </summary>
    private float GetDuration(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Type == PresentationEventType.CombatAction)
        {
            return GetCombatActionDuration(presentationEvent.AttackKind);
        }

        return presentationEvent.Type switch
        {
            PresentationEventType.SwordThrow => throwDuration,
            PresentationEventType.SwordRecall => recallDuration,
            _ => 0f,
        };
    }

    /// <summary>
    /// 통합 전투 연출의 공격 종류에 맞는 임시 대기 시간을 반환한다.
    /// </summary>
    private float GetCombatActionDuration(AttackPresentationKind attackKind)
    {
        return attackKind switch
        {
            AttackPresentationKind.SwordThrow => throwDuration,
            _ => 0f,
        };
    }

    /// <summary>
    /// 통합 전투 연출 이벤트에 포함된 피격/사망 결과를 로그로 출력한다.
    /// </summary>
    private void LogCombatResult(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Type != PresentationEventType.CombatAction || !presentationEvent.HasDamageResult)
        {
            return;
        }

        string targetName = presentationEvent.TargetActor != null ? presentationEvent.TargetActor.name : "없음";
        DamageResult result = presentationEvent.DamageResult;
        Debug.Log($"{nameof(SwordActionPresenter)}: 통합 전투 연출 결과. 공격 종류: {presentationEvent.AttackKind}, 대상: {targetName}, 피해량: {result.Damage}, HP: {result.HitPointBefore} -> {result.HitPointAfter}, 사망 여부: {result.KilledByThisDamage}", this);
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

        return true;
    }
}
