using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 통합 전투 연출 이벤트를 받아 공격자와 피격자의 애니메이션을 동시에 지휘하는 씬 단위 Presenter다.
/// 공격 판정과 피해 적용에는 관여하지 않고, 양쪽 연출이 끝난 뒤 큐 완료 신호를 보낸다.
/// </summary>
public class CombatActionPresenter : MonoBehaviour, IPresentationEventHandler
{
    [Header("Reference")]
    // 논리 Actor를 화면 표시용 ActorVisualController로 변환할 씬 단위 등록소다.
    [SerializeField] private ActorPresentationRegistry presentationRegistry;

    [Header("Data")]
    // 공격 종류별 애니메이션 상태와 동시 연출 시간을 보관하는 데이터다.
    [SerializeField] private CombatPresentationData presentationData;

    [Header("Debug")]
    // true면 통합 전투 연출 시작과 종료 흐름을 Unity 콘솔에 출력한다.
    [SerializeField] private bool logCombatFlow = true;

    // 현재 실행 중인 통합 전투 연출 코루틴이다.
    private Coroutine combatCoroutine;
    // 현재 처리 중인 큐 이벤트 완료 핸들이다.
    private PresentationEventHandle activeHandle;
    // 현재 전투 연출 중인 공격자 시각 컴포넌트다.
    private ActorVisualController activeAttackerVisual;
    // 현재 전투 연출 중인 피격자 시각 컴포넌트다.
    private ActorVisualController activeTargetVisual;
    // 현재 피격자가 이번 피해로 사망했는지 나타낸다.
    private bool activeTargetKilled;
    // 현재 전투 시작 알림을 보냈는지 나타낸다.
    private bool combatPresentationStarted;
    // 현재 시작·종료 알림에 전달할 전투 연출 이벤트다.
    private PresentationEvent activePresentationEvent;

    // 전투 자세가 시작된 직후 검 같은 부가 Visual에 현재 이벤트를 알린다.
    public event Action<PresentationEvent> CombatPresentationStarted;
    // 전투 자세 유지가 끝난 직후 부가 Visual에 현재 이벤트 종료를 알린다.
    public event Action<PresentationEvent> CombatPresentationCompleted;

    /// <summary>
    /// 필수 참조와 전투 연출 데이터를 검사하고 연출 큐 등록을 시도한다.
    /// </summary>
    private void OnEnable()
    {
        if (!HasValidReference() || !HasValidData())
        {
            enabled = false;
            return;
        }

        TryRegisterQueue(false);
    }

    /// <summary>
    /// 초기화 순서 때문에 OnEnable에서 놓친 큐 등록을 시작 시점에 다시 시도한다.
    /// </summary>
    private void Start()
    {
        TryRegisterQueue(true);
    }

    /// <summary>
    /// 큐 등록과 진행 중인 연출을 정리하고 큐 정지를 막기 위해 완료 처리한다.
    /// </summary>
    private void OnDisable()
    {
        if (ActionPresentationQueue.Instance != null)
        {
            ActionPresentationQueue.Instance.Unregister(this);
        }

        if (combatCoroutine != null)
        {
            StopCoroutine(combatCoroutine);
            combatCoroutine = null;
        }

        NotifyCombatPresentationCompleted();
        RestoreActiveVisuals();

        if (activeHandle != null && !activeHandle.IsCompleted)
        {
            activeHandle.Complete();
        }

        ClearActiveCombat();
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
                Debug.LogError($"{nameof(CombatActionPresenter)} on {name}에는 씬의 {nameof(ActionPresentationQueue)}가 필요합니다.", this);
                enabled = false;
            }

            return;
        }

        queue.Register(this);
    }

    /// <summary>
    /// 통합 전투 연출 이벤트인지 확인한다.
    /// </summary>
    public bool CanHandle(PresentationEvent presentationEvent)
    {
        return presentationEvent.Type == PresentationEventType.CombatAction;
    }

    /// <summary>
    /// 공격자와 대상의 통합 전투 연출을 시작한다.
    /// </summary>
    public void Handle(PresentationEvent presentationEvent, PresentationEventHandle handle)
    {
        if (combatCoroutine != null)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}은 이미 전투 연출을 처리 중입니다. 새 이벤트를 자동 완료합니다. 이벤트: {presentationEvent}", this);
            handle.Complete();
            return;
        }

        if (!TryPrepareCombat(presentationEvent, out CombatPresentationEntry entry))
        {
            handle.Complete();
            return;
        }

        combatCoroutine = StartCoroutine(PlayCombatAction(presentationEvent, entry, handle));
    }

    /// <summary>
    /// 전투 이벤트와 공격 종류 데이터를 확인하고 공격자·피격자 시각 컴포넌트를 준비한다.
    /// </summary>
    private bool TryPrepareCombat(PresentationEvent presentationEvent, out CombatPresentationEntry entry)
    {
        entry = default;

        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (presentationEvent.Actor == null || presentationEvent.TargetActor == null || !presentationEvent.HasDamageResult)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)}: 전투 연출 이벤트에 공격자, 피격자 또는 피해 결과가 없습니다. 이벤트: {presentationEvent}", this);
            return false;
        }

        if (!presentationData.TryGetEntry(presentationEvent.AttackKind, out entry))
        {
            Debug.LogError($"{nameof(CombatActionPresenter)}: {presentationEvent.AttackKind} 공격 종류의 연출 데이터가 없습니다.", this);
            return false;
        }

        if (!presentationRegistry.TryGetVisual(presentationEvent.Actor, out activeAttackerVisual))
        {
            Debug.LogError($"{nameof(CombatActionPresenter)}: 공격자 {presentationEvent.Actor.name}의 {nameof(ActorVisualController)}를 찾지 못했습니다.", this);
            return false;
        }

        if (!presentationRegistry.TryGetVisual(presentationEvent.TargetActor, out activeTargetVisual))
        {
            Debug.LogError($"{nameof(CombatActionPresenter)}: 피격자 {presentationEvent.TargetActor.name}의 {nameof(ActorVisualController)}를 찾지 못했습니다.", this);
            activeAttackerVisual = null;
            return false;
        }

        if (!activeAttackerVisual.HasValidAnimationReference() || !activeTargetVisual.HasValidAnimationReference())
        {
            ClearActiveCombat();
            return false;
        }

        activeTargetKilled = presentationEvent.DamageResult.KilledByThisDamage;
        return true;
    }

    /// <summary>
    /// 공격자와 피격자가 서로 마주보게 한 뒤 공격·피격 또는 사망 자세를 동시에 재생한다.
    /// </summary>
    private IEnumerator PlayCombatAction(
        PresentationEvent presentationEvent,
        CombatPresentationEntry entry,
        PresentationEventHandle handle)
    {
        activeHandle = handle;
        activePresentationEvent = presentationEvent;

        GridPosition attackerPosition = presentationEvent.Actor.GridPosition;
        GridPosition targetPosition = presentationEvent.TargetActor.GridPosition;
        activeAttackerVisual.FaceFromTo(attackerPosition, targetPosition);
        activeTargetVisual.FaceFromTo(targetPosition, attackerPosition);

        activeAttackerVisual.TryPlayAnimationState(
            entry.AttackerAnimationStateName,
            presentationData.CrossFadeDuration);

        string targetStateName = activeTargetKilled
            ? presentationData.DeathAnimationStateName
            : presentationData.HitAnimationStateName;
        activeTargetVisual.TryPlayAnimationState(targetStateName, presentationData.CrossFadeDuration);

        combatPresentationStarted = true;
        CombatPresentationStarted?.Invoke(presentationEvent);

        if (logCombatFlow)
        {
            Debug.Log($"{nameof(CombatActionPresenter)}: {presentationEvent.AttackKind} 전투 연출을 시작합니다. 공격자: {presentationEvent.Actor.name}, 대상: {presentationEvent.TargetActor.name}, 사망 여부: {activeTargetKilled}", this);
        }

        yield return new WaitForSeconds(entry.PresentationDuration);

        NotifyCombatPresentationCompleted();
        RestoreActiveVisuals();

        if (logCombatFlow)
        {
            Debug.Log($"{nameof(CombatActionPresenter)}: {presentationEvent.AttackKind} 전투 연출을 완료했습니다.", this);
        }

        CompleteActiveCombat(handle);
    }

    /// <summary>
    /// 전투 연출이 끝난 공격자와 살아 있는 피격자를 대기 자세로 복귀시킨다.
    /// </summary>
    private void RestoreActiveVisuals()
    {
        if (presentationData == null)
        {
            return;
        }

        activeAttackerVisual?.TryPlayAnimationState(
            presentationData.IdleAnimationStateName,
            presentationData.CrossFadeDuration,
            false);

        if (!activeTargetKilled)
        {
            activeTargetVisual?.TryPlayAnimationState(
                presentationData.IdleAnimationStateName,
                presentationData.CrossFadeDuration,
                false);
        }
    }

    /// <summary>
    /// 현재 전투 연출 상태를 정리하고 큐 완료 신호를 보낸다.
    /// </summary>
    private void CompleteActiveCombat(PresentationEventHandle handle)
    {
        combatCoroutine = null;
        ClearActiveCombat();
        handle.Complete();
    }

    /// <summary>
    /// 현재 전투 연출이 참조하던 핸들과 양쪽 시각 컴포넌트를 비운다.
    /// </summary>
    private void ClearActiveCombat()
    {
        activeHandle = null;
        activeAttackerVisual = null;
        activeTargetVisual = null;
        activeTargetKilled = false;
        combatPresentationStarted = false;
        activePresentationEvent = default;
    }

    /// <summary>
    /// 시작 알림을 보낸 전투 연출의 종료를 부가 Visual 구독자에게 한 번만 알린다.
    /// </summary>
    private void NotifyCombatPresentationCompleted()
    {
        if (!combatPresentationStarted)
        {
            return;
        }

        combatPresentationStarted = false;
        CombatPresentationCompleted?.Invoke(activePresentationEvent);
    }

    /// <summary>
    /// 통합 전투 연출에 필요한 씬 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (presentationRegistry == null)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}에는 {nameof(ActorPresentationRegistry)} 참조가 필요합니다.", this);
            return false;
        }

        if (presentationData == null)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}에는 {nameof(CombatPresentationData)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 전투 연출 데이터의 상태 이름, 유지 시간과 공격 종류 중복 여부를 검사한다.
    /// </summary>
    public bool HasValidData()
    {
        if (presentationData == null)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}에는 검사할 {nameof(CombatPresentationData)}가 필요합니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(presentationData.HitAnimationStateName) ||
            string.IsNullOrWhiteSpace(presentationData.DeathAnimationStateName) ||
            string.IsNullOrWhiteSpace(presentationData.IdleAnimationStateName))
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 피격, 사망 또는 대기 애니메이션 상태 이름이 비어 있습니다.", this);
            return false;
        }

        if (presentationData.CrossFadeDuration < 0f)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 애니메이션 전환 시간은 0 이상이어야 합니다.", this);
            return false;
        }

        IReadOnlyList<CombatPresentationEntry> entries = presentationData.Entries;
        if (entries == null || entries.Count == 0)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 공격 종류별 연출 데이터가 비어 있습니다.", this);
            return false;
        }

        HashSet<AttackPresentationKind> registeredKinds = new();
        for (int i = 0; i < entries.Count; i++)
        {
            CombatPresentationEntry entry = entries[i];
            if (entry.AttackKind == AttackPresentationKind.None)
            {
                Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 {i}번 연출 데이터에는 실제 공격 종류가 필요합니다.", this);
                return false;
            }

            if (!registeredKinds.Add(entry.AttackKind))
            {
                Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 {entry.AttackKind} 공격 연출 데이터가 중복되었습니다.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.AttackerAnimationStateName) || entry.PresentationDuration <= 0f)
            {
                Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 {entry.AttackKind} 공격 상태 이름 또는 연출 시간이 올바르지 않습니다.", this);
                return false;
            }
        }

        return true;
    }
}
