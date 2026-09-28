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

    // 진입·타격 카메라와 복귀 시점의 자세 정리를 담당하는 명시적 참조다.
    [SerializeField] private CombatCameraPresenter cameraPresenter;
    public CombatCameraPresenter CameraPresenter => cameraPresenter;

    /// <summary>검 출발 시 투척 자세만 먼저 시작한다. 피격·결과·타격 알림은 검 도착 이벤트에서 처리한다.</summary>
    public bool TryBeginSwordThrow(PresentationEvent evt, ActorVisualController visual)
    {
        if (!HasValidReference() || !HasValidData() || visual == null || !visual.HasValidAnimationReference()) return false;
        if (!presentationData.TryGetEntry(AttackPresentationKind.SwordThrow, out CombatPresentationEntry entry))
        {
            Debug.LogError("검 투척 출발에 필요한 공격 연출 데이터가 없습니다.", this);
            return false;
        }
        visual.BeginCombatPresentation(evt.FromPosition, evt.ToPosition, true, false);
        cameraPresenter.AlignAttackerFacing(visual);
        return visual.TryPlayAnimationState(entry.AttackerAnimationStateName, presentationData.CrossFadeDuration);
    }

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
    // 현재 공격이 빗나가 대상에게 피해가 적용되지 않았는지 나타낸다.
    private bool activeAttackMissed;
    // 현재 전투 시작 알림을 보냈는지 나타낸다.
    private bool combatPresentationStarted;
    // 현재 시작·종료 알림에 전달할 전투 연출 이벤트다.
    private PresentationEvent activePresentationEvent;
    // 현재 전투에 대여한 공격자/대상 단발 VFX다.
    private VfxHandle attackerEffect;
    private VfxHandle targetEffect;

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

        ReleaseCombatEffects();
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

        if (VfxManager.Instance == null || !VfxManager.Instance.isActiveAndEnabled)
        {
            Debug.LogError("전투 연출에 필요한 활성 VfxManager가 없습니다.", this);
            return false;
        }

        if (!HasValidReference() || !HasValidData())
        {
            return false;
        }

        if (presentationEvent.Actor == null || presentationEvent.TargetActor == null || !presentationEvent.HasAttackResult)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)}: 전투 연출 이벤트에 공격자, 대상 또는 공격 판정 결과가 없습니다. 이벤트: {presentationEvent}", this);
            return false;
        }

        if (presentationEvent.AttackResult.IsHit && !presentationEvent.HasDamageResult)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)}: 명중 전투 연출 이벤트에 피해 결과가 없습니다. 이벤트: {presentationEvent}", this);
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

        // 배우별 원화는 표시 컴포넌트의 명시적 데이터로 선택한다. 연결된 데이터의 누락은 오류다.
        CombatPresentationData actorData = activeAttackerVisual.CombatPresentationOverride;
        if (actorData != null && (!HasValidData(actorData) || !actorData.TryGetEntry(presentationEvent.AttackKind, out entry)))
        {
            Debug.LogError($"{actorData.name}에 유효한 {presentationEvent.AttackKind} 공격 연출이 없습니다.", this);
            ClearActiveCombat();
            return false;
        }

        activeAttackMissed = !presentationEvent.AttackResult.IsHit;
        activeTargetKilled = presentationEvent.HasDamageResult && presentationEvent.DamageResult.KilledByThisDamage;
        return true;
    }

    /// <summary>
    /// 공격자는 대상을 보고 피격자는 엄폐 방향을 유지하며 공격·피격 또는 사망 자세를 재생한다.
    /// </summary>
    private IEnumerator PlayCombatAction(
        PresentationEvent presentationEvent,
        CombatPresentationEntry entry,
        PresentationEventHandle handle)
    {
        activeHandle = handle;
        activePresentationEvent = presentationEvent;

        // 기존 VFX 지연은 준비 박자로 사용하고 자세·결과·이펙트는 같은 프레임에 맞춘다.
        if (entry.VfxDelay > 0f && presentationEvent.AttackKind != AttackPresentationKind.SwordThrow) yield return new WaitForSeconds(entry.VfxDelay);
        GridPosition attackerPosition = presentationEvent.FromPosition;
        GridPosition targetPosition = presentationEvent.ToPosition;
        if (presentationEvent.HasDamageResult)
            presentationRegistry.PresentDamage(presentationEvent.TargetActor, presentationEvent.DamageResult);
        activeAttackerVisual.BeginCombatPresentation(attackerPosition, targetPosition, true, false);
        activeTargetVisual.BeginCombatPresentation(targetPosition, attackerPosition, false, activeTargetKilled);

        activeAttackerVisual.TryPlayAnimationState(
            entry.AttackerAnimationStateName,
            presentationData.CrossFadeDuration,
            presentationEvent.AttackKind != AttackPresentationKind.SwordThrow);

        string targetStateName = activeAttackMissed
            ? presentationData.MissAnimationStateName
            : activeTargetKilled
                ? presentationData.DeathAnimationStateName
                : presentationData.HitAnimationStateName;
        activeTargetVisual.TryPlayAnimationState(targetStateName, presentationData.CrossFadeDuration);

        cameraPresenter.AlignAttackerFacing(activeAttackerVisual);
        cameraPresenter.PresentImpact(presentationEvent, activeTargetVisual);
        combatPresentationStarted = true;
        CombatPresentationStarted?.Invoke(presentationEvent);

        if (logCombatFlow)
        {
            string resultText = activeAttackMissed ? "빗나감" : activeTargetKilled ? "명중·사망" : "명중";
            Debug.Log($"{nameof(CombatActionPresenter)}: {presentationEvent.AttackKind} 전투 연출을 시작합니다. 공격자: {presentationEvent.Actor.name}, 대상: {presentationEvent.TargetActor.name}, 결과: {resultText}", this);
        }

        float elapsed = 0f;
        bool effectsStarted = false;
        while (elapsed < entry.PresentationDuration)
        {
            if (!effectsStarted && elapsed >= 0f)
            {
                effectsStarted = true;
                BeginCombatEffects(entry);
            }
            if (effectsStarted)
            {
                float age = elapsed;
                float opacity = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(entry.VfxDuration * 0.5f, entry.VfxDuration, age));
                UpdateCombatEffects(entry, opacity);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        ReleaseCombatEffects();
        // 복귀 중에도 공격/피격 자세를 유지하며 카메라가 복귀 완료 후 정리한다.
        if (cameraPresenter.IsCombatActive)
            cameraPresenter.DeferUntilRestored(() => { NotifyCombatPresentationCompleted(); RestoreActiveVisuals(); ClearActiveCombat(); });
        else { NotifyCombatPresentationCompleted(); RestoreActiveVisuals(); }

        if (logCombatFlow)
        {
            Debug.Log($"{nameof(CombatActionPresenter)}: {presentationEvent.AttackKind} 전투 연출을 완료했습니다.", this);
        }

        CompleteActiveCombat(handle);
    }

    /// <summary>이벤트 판정에 따라 공격자 이펙트와 공용 혈흔 또는 회피를 한 번 대여한다.</summary>
    private void BeginCombatEffects(CombatPresentationEntry entry)
    {
        // 맨손 충격은 접촉한 대상에만 표시하며 빗나간 경우에는 공용 회피만 표시한다.
        if (entry.AttackerVfx != VfxId.None && !(activeAttackMissed && entry.AttackKind == AttackPresentationKind.MeleeUnarmed))
            attackerEffect = VfxManager.TryAcquire(entry.AttackerVfx);
        targetEffect = VfxManager.TryAcquire(activeAttackMissed ? VfxId.SharedDodge : VfxId.SharedBloodHit);
        UpdateCombatEffects(entry, 1f);
    }

    /// <summary>논리 Actor 위치 대신 현재 표시 중인 Sprite 위치에 이펙트를 붙인다.</summary>
    private void UpdateCombatEffects(CombatPresentationEntry entry, float opacity)
    {
        SpriteRenderer attacker = activeAttackerVisual.TargetRenderer;
        SpriteRenderer target = activeTargetVisual.TargetRenderer;
        float facing = activeAttackerVisual.IsFacingRight ? 1f : -1f;
        if (attackerEffect != null)
        {
            Vector3 position = attacker.bounds.center + new Vector3(
                entry.VfxOffset.x * attacker.bounds.size.x * facing,
                entry.VfxOffset.y * attacker.bounds.size.y, 0f);
            bool gun = entry.AttackKind == AttackPresentationKind.PlayerGun || entry.AttackKind == AttackPresentationKind.EnemyRanged;
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = new(facing, 1f, 1f);
            if (gun || entry.SpanToTarget)
            {
                Vector3 direction = target.bounds.center - position;
                rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                scale = Vector3.one;
            }
            attackerEffect.SetTransform(position, rotation, scale);
            attackerEffect.SetSorting(attacker.sortingLayerID, attacker.sortingOrder + 3);
            attackerEffect.SetSingle(attacker.bounds.size.y * entry.VfxHeightRatio, opacity * activeAttackerVisual.VisionAlpha);
            if (entry.SpanToTarget)
            {
                // 비행 대기 없이 실제 두 표시 위치 사이를 한 컷의 잔상으로 잇는다.
                Vector3 end = target.bounds.center;
                attackerEffect.SetTransform((position + end) * 0.5f, rotation, Vector3.one);
                attackerEffect.SetPart(0, Vector3.zero, new Vector2(Vector3.Distance(position, end),
                    attacker.bounds.size.y * entry.VfxHeightRatio), opacity * activeAttackerVisual.VisionAlpha);
            }
            if (entry.AttackKind == AttackPresentationKind.MeleeUnarmed)
            {
                attackerEffect.SetTransform(target.bounds.center, Quaternion.identity, scale);
                attackerEffect.SetSorting(target.sortingLayerID, target.sortingOrder + 2);
                attackerEffect.SetSingle(target.bounds.size.y * entry.VfxHeightRatio, opacity * activeTargetVisual.VisionAlpha);
            }
        }
        if (targetEffect != null)
        {
            targetEffect.SetTransform(target.bounds.center, Quaternion.identity, new Vector3(facing, 1f, 1f));
            targetEffect.SetSorting(target.sortingLayerID, target.sortingOrder + (activeAttackMissed ? -1 : 3));
            targetEffect.SetSingle(target.bounds.size.y * (activeAttackMissed ? 0.9f : 0.65f), opacity * activeTargetVisual.VisionAlpha);
        }
    }

    /// <summary>정상 종료와 중단에서 단발 VFX를 모두 반납한다.</summary>
    private void ReleaseCombatEffects()
    {
        attackerEffect?.Release(); targetEffect?.Release();
        attackerEffect = null; targetEffect = null;
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

        activeAttackerVisual?.EndCombatPresentation();
        activeTargetVisual?.EndCombatPresentation();
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
        activeHandle = null;
        if (!cameraPresenter.IsCombatActive) ClearActiveCombat();
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
        activeAttackMissed = false;
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
        if (cameraPresenter == null)
        {
            Debug.LogError("전투 연출 카메라 참조가 없습니다.", this);
            return false;
        }
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
    public bool HasValidData() => HasValidData(presentationData);

    /// <summary>공통 데이터와 배우별 공격 데이터에 동일한 검증 규칙을 적용한다.</summary>
    private bool HasValidData(CombatPresentationData presentationData)
    {
        if (presentationData == null)
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}에는 검사할 {nameof(CombatPresentationData)}가 필요합니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(presentationData.HitAnimationStateName) ||
            string.IsNullOrWhiteSpace(presentationData.MissAnimationStateName) ||
            string.IsNullOrWhiteSpace(presentationData.DeathAnimationStateName) ||
            string.IsNullOrWhiteSpace(presentationData.IdleAnimationStateName))
        {
            Debug.LogError($"{nameof(CombatActionPresenter)} on {name}의 피격, 빗나감, 사망 또는 대기 애니메이션 상태 이름이 비어 있습니다.", this);
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

            if (entry.VfxDelay < 0f || entry.VfxDuration <= 0f || entry.VfxHeightRatio <= 0f ||
                entry.VfxDelay + entry.VfxDuration > entry.PresentationDuration)
            {
                Debug.LogError($"{nameof(CombatActionPresenter)}의 {entry.AttackKind} VFX 시간/크기가 올바르지 않습니다.", this);
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
