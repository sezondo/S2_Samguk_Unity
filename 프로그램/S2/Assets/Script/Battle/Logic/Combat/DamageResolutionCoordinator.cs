using UnityEngine;

/// <summary>
/// 피해 적용 요청을 표준 피해 결과와 통합 전투 연출 이벤트로 변환하는 기본 논리 이벤트 처리자다.
/// 씬 배치 없이 ActionLogicEventBus에 기본 등록되어 공격 수단별 중복 피해 처리를 줄인다.
/// </summary>
public sealed class DamageResolutionCoordinator : IActionLogicEventHandler
{
    public static DamageResolutionCoordinator Instance { get; } = new();

    /// <summary>
    /// 외부 생성을 막고 기본 인스턴스만 사용한다.
    /// </summary>
    private DamageResolutionCoordinator()
    {
    }

    /// <summary>
    /// 피해 적용 요청 이벤트만 처리한다.
    /// </summary>
    public bool CanHandle(IActionLogicEvent logicEvent)
    {
        return logicEvent is ApplyDamageLogicEvent;
    }

    /// <summary>
    /// 피해 적용 요청을 받아 HP 변경, 피해 논리 이벤트, 사망 논리 이벤트, 통합 전투 연출 이벤트를 만든다.
    /// </summary>
    public void Handle(IActionLogicEvent logicEvent, ActionResolutionContext context)
    {
        if (logicEvent is not ApplyDamageLogicEvent applyDamage)
        {
            return;
        }

        if (!TryResolveDamage(applyDamage, out DamageResult damageResult))
        {
            // 공격 행동이 미리 잡아 둔 카메라가 남지 않도록 실패 흐름에서도 복귀시킨다.
            context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("피해 처리 실패 카메라 복귀"));
            return;
        }

        context.Publish(new DamageAppliedLogicEvent(
            applyDamage.Attacker,
            applyDamage.Target,
            applyDamage.TargetPosition,
            damageResult));

        if (damageResult.KilledByThisDamage)
        {
            context.Publish(new ActorDiedLogicEvent(
                applyDamage.Attacker,
                applyDamage.Target,
                applyDamage.TargetPosition,
                damageResult));
        }

        bool temporarilyRevealAttacker =
            applyDamage.PresentationKind == AttackPresentationKind.EnemyRanged &&
            PlayerVisionManager.Instance != null &&
            !PlayerVisionManager.Instance.IsVisible(applyDamage.FromPosition);
        if (temporarilyRevealAttacker)
        {
            context.EnqueuePresentation(PresentationEvent.ActorVisibilityOverride(
                applyDamage.Attacker,
                true,
                "시야 밖 공격자 임시 노출"));
        }

        context.EnqueuePresentation(PresentationEvent.CombatAction(
            applyDamage.Attacker,
            applyDamage.Target,
            applyDamage.FromPosition,
            applyDamage.TargetPosition,
            applyDamage.PresentationKind,
            damageResult,
            applyDamage.Message));

        context.EnqueuePresentation(PresentationEvent.CombatCameraRestore("전투 카메라 복귀"));

        if (temporarilyRevealAttacker)
        {
            context.EnqueuePresentation(PresentationEvent.ActorVisibilityOverride(
                applyDamage.Attacker,
                false,
                "시야 밖 공격자 임시 노출 해제"));
        }
    }

    /// <summary>
    /// 대상의 IDamageable을 찾아 피해를 적용하고 결과를 반환한다.
    /// </summary>
    private static bool TryResolveDamage(ApplyDamageLogicEvent applyDamage, out DamageResult damageResult)
    {
        damageResult = default;

        if (applyDamage.Target == null)
        {
            Debug.LogError($"{nameof(DamageResolutionCoordinator)}: 피해 적용 대상이 없어 처리를 중단합니다.");
            return false;
        }

        IDamageable damageable = applyDamage.Target.GetComponent<IDamageable>();
        if (damageable == null)
        {
            Debug.LogError($"{nameof(DamageResolutionCoordinator)}: {applyDamage.Target.name} 대상에는 {nameof(IDamageable)}이 없어 피해를 적용할 수 없습니다.");
            return false;
        }

        damageResult = damageable.TakeDamage(applyDamage.Damage);
        return true;
    }
}
