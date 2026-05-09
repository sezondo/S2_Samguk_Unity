using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMeleeAttack))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerMeleeSlashEffect : MonoBehaviour
{
    [Serializable]
    private class SlashEffectEntry
    {
        [Header("VFX")]
        // 이 콤보 단계에서 호출할 전역 VFX ID다.
        public VfxId vfxId = VfxId.None;

        [Header("Timing")]
        // AttackNormalizedTime 기준으로 이 값 이상일 때 이펙트를 켠다.
        [Range(0f, 1f)] public float startNormalizedTime = 0f;
        // AttackNormalizedTime 기준으로 이 값 이후에는 이펙트를 끈다.
        [Range(0f, 1f)] public float endNormalizedTime = 0.35f;

        [Header("Transform")]
        // x는 공격 방향 앞쪽 거리, y는 공격 방향의 수직 오프셋이다.
        public Vector2 offset = new(0.65f, 0f);
        // 프리팹 기본 크기에 곱할 로컬 스케일이다.
        public Vector2 scale = Vector2.one;
        // 마우스 공격 방향 기준 회전 보정값이다.
        public float angleOffset = 0f;
    }

    [Header("Combo Effects")]
    // PlayerMeleeAttack.CurrentComboIndex와 같은 번호의 이펙트 설정을 사용한다.
    // Element 0은 1타, Element 1은 2타처럼 콤보 인덱스와 직접 대응한다.
    [SerializeField] private SlashEffectEntry[] comboEffects;

    private PlayerMeleeAttack meleeAttack;
    private PlayerFSMManager fsm;
    // 참격은 공격 진행률 동안 유지되어야 하므로 VfxManager.Play가 아니라 Acquire/Release로 직접 수명을 관리한다.
    private VfxHandle activeEffect;
    private int activeEffectIndex = -1;
    private float previousNormalizedTime;

    // PlayerWeaponVisualFSM이 이 값을 읽어서 참격이 켜져 있는 동안 검 본체를 숨긴다.
    public bool IsSlashVisible => activeEffect != null && activeEffect.IsValid;

    private void Awake() // 필요한 플레이어 참조를 찾는다. 실제 이펙트 생성/풀링은 VfxManager가 담당한다.
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        meleeAttack = context.MeleeAttack;
        fsm = context.Fsm;

        if (meleeAttack == null || fsm == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeSlashEffect)} on {name} is missing required components.", this);
            enabled = false;
            return;
        }
    }

    private void OnDisable() // 컴포넌트가 꺼질 때 켜져 있던 참격 이펙트를 정리한다.
    {
        DeactivateActiveEffect();
    }

    private void LateUpdate() // 공격 진행률을 읽어 콤보 번호에 맞는 참격 이펙트를 켜고 위치/회전을 갱신한다.
    {
        if (!fsm.IsState(PlayerState.MeleeAttack))
        {
            // 근접 공격 상태가 아니면 참격은 절대 남아 있으면 안 된다.
            // 상태가 Dodge/Dead 등으로 끊겨도 여기서 반납한다.
            DeactivateActiveEffect();
            previousNormalizedTime = 0f;
            return;
        }

        int comboIndex = meleeAttack.CurrentComboIndex;
        SlashEffectEntry entry = GetEntry(comboIndex);
        if (entry == null)
        {
            DeactivateActiveEffect();
            return;
        }

        float normalizedTime = meleeAttack.AttackNormalizedTime;
        if (normalizedTime < previousNormalizedTime)
        {
            // 같은 FSM 상태 안에서 다음 콤보가 바로 시작되면 진행률이 0으로 되돌아온다.
            // 이때 이전 콤보 참격을 반납하지 않으면 1타 이펙트가 2타 위치까지 따라오는 문제가 생긴다.
            DeactivateActiveEffect();
        }

        previousNormalizedTime = normalizedTime;

        if (!ShouldShowEffect(entry, normalizedTime))
        {
            // 공격 중이어도 현재 콤보의 참격 표시 구간 밖이면 이펙트를 꺼둔다.
            // 검 본체는 PlayerWeaponVisualFSM이 이 타이밍을 보고 다시 표시한다.
            DeactivateActiveEffect();
            return;
        }

        VfxHandle effect = ActivateEffect(comboIndex, entry);
        if (effect == null || !effect.IsValid)
        {
            return;
        }

        ApplyEffectTransform(effect, entry);
    }

    private SlashEffectEntry GetEntry(int comboIndex) // 현재 콤보 번호에 맞는 이펙트 설정을 가져온다.
    {
        if (comboEffects == null || comboIndex < 0 || comboIndex >= comboEffects.Length)
        {
            return null;
        }

        SlashEffectEntry entry = comboEffects[comboIndex];
        return entry != null && entry.vfxId != VfxId.None ? entry : null;
    }

    private bool ShouldShowEffect(SlashEffectEntry entry, float normalizedTime) // 현재 공격 진행률이 이펙트 표시 구간 안인지 확인한다.
    {
        float startTime = Mathf.Clamp01(entry.startNormalizedTime);
        float endTime = Mathf.Clamp01(entry.endNormalizedTime);
        if (endTime < startTime)
        {
            (startTime, endTime) = (endTime, startTime);
        }

        return normalizedTime >= startTime && normalizedTime <= endTime;
    }

    private VfxHandle ActivateEffect(int comboIndex, SlashEffectEntry entry) // VfxManager에서 현재 콤보 이펙트를 빌려온다.
    {
        if (activeEffectIndex != comboIndex)
        {
            // 콤보 번호가 바뀌면 이전 참격은 반납하고 새 참격을 빌린다.
            // 같은 콤보가 계속 유지되는 동안에는 아래에서 Transform만 갱신한다.
            DeactivateActiveEffect();
            activeEffect = VfxManager.TryAcquire(entry.vfxId);
            if (activeEffect == null || !activeEffect.IsValid)
            {
                activeEffect = null;
                activeEffectIndex = -1;
                return null;
            }

            activeEffectIndex = comboIndex;
        }

        return activeEffect;
    }

    private void DeactivateActiveEffect() // 현재 켜져 있는 참격 이펙트를 VfxManager에 반납한다.
    {
        if (activeEffect != null)
        {
            activeEffect.Release();
            activeEffect = null;
        }

        activeEffectIndex = -1;
    }

    private void ApplyEffectTransform(VfxHandle effect, SlashEffectEntry entry) // 공격 방향과 오프셋을 기준으로 참격 위치/회전/크기를 적용한다.
    {
        Vector2 attackDirection = NormalizeOrDefault(meleeAttack.CurrentAttackDirection, Vector2.right);
        // y 오프셋을 공격 방향의 좌우 축으로 적용하기 위한 수직 벡터다.
        Vector2 perpendicular = new(-attackDirection.y, attackDirection.x);
        Vector2 worldOffset = attackDirection * entry.offset.x + perpendicular * entry.offset.y;

        Vector3 position = (Vector2)transform.position + worldOffset;
        Quaternion rotation = Quaternion.Euler(0f, 0f, DirectionToAngle(attackDirection) + entry.angleOffset);
        Vector3 scale = new(entry.scale.x, entry.scale.y, 1f);
        effect.SetTransform(position, rotation, scale);
    }

    private static float DirectionToAngle(Vector2 direction) // 방향 벡터를 Z축 회전 각도로 변환한다.
    {
        Vector2 normalizedDirection = NormalizeOrDefault(direction, Vector2.right);
        return Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
    }

    private static Vector2 NormalizeOrDefault(Vector2 direction, Vector2 fallback) // 방향이 0에 가까우면 fallback을 대신 정규화해서 반환한다.
    {
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback.normalized;
    }
}
