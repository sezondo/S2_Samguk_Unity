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

        [Header("Transform")]
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
    private int activeEffectAttackSequenceId = -1;

    // PlayerWeaponVisualFSM이 이 값을 읽어서 참격이 켜져 있는 동안 검 본체를 숨긴다.
    public bool IsSlashVisible => ShouldShowSlashNow();
    // 참격 이펙트 자체는 짧게 끝나도, 콤보가 이어질 수 있는 동안에는 검 본체를 숨긴다.
    public bool ShouldHideWeaponVisual => ShouldHideWeaponNow();

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
            return;
        }

        int comboIndex = meleeAttack.CurrentComboIndex;
        SlashEffectEntry entry = GetEntry(comboIndex);
        if (entry == null)
        {
            DeactivateActiveEffect();
            return;
        }

        if (!ShouldShowSlashNow())
        {
            // 공격 중이어도 실제 판정 시간 밖이면 이펙트를 꺼둔다.
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

    private bool ShouldShowSlashNow() // 현재 공격 시간이 실제 판정 시간 안인지 확인한다.
    {
        if (meleeAttack == null || fsm == null || !fsm.IsState(PlayerState.MeleeAttack))
        {
            return false;
        }

        if (GetEntry(meleeAttack.CurrentComboIndex) == null)
        {
            return false;
        }

        PlayerMeleeAttackData attackData = meleeAttack.CurrentAttackData;
        if (attackData == null)
        {
            return false;
        }

        float startTime = Mathf.Max(0f, attackData.hitboxStartTime);
        float endTime = startTime + Mathf.Max(0f, attackData.hitboxActiveTime);
        float elapsedTime = meleeAttack.AttackElapsedTime;

        return elapsedTime >= startTime && elapsedTime <= endTime;
    }

    private bool ShouldHideWeaponNow()
    {
        if (meleeAttack == null || fsm == null)
        {
            return false;
        }

        if (fsm.IsState(PlayerState.MeleeAttack))
        {
            PlayerMeleeAttackData attackData = meleeAttack.CurrentAttackData;
            if (attackData == null)
            {
                return false;
            }

            // 검은 실제 판정 시점에 도착한 뒤부터 숨긴다.
            // 참격 VFX가 먼저 끝나도 공격 동작이 끝날 때까지 중간에 드러나지 않게 한다.
            return meleeAttack.AttackElapsedTime >= Mathf.Max(0f, attackData.hitboxStartTime);
        }

        // 공격이 끝난 뒤 콤보 입력을 기다리는 시간에도 검이 튀어나오지 않게 한다.
        return meleeAttack.IsComboWindowOpenForVisual;
    }

    private VfxHandle ActivateEffect(int comboIndex, SlashEffectEntry entry) // VfxManager에서 현재 콤보 이펙트를 빌려온다.
    {
        int attackSequenceId = meleeAttack.AttackSequenceId;
        if (activeEffectIndex != comboIndex || activeEffectAttackSequenceId != attackSequenceId)
        {
            // 콤보 번호나 공격 시작 번호가 바뀌면 이전 참격은 반납하고 새 참격을 빌린다.
            // 같은 콤보를 반복해도 공격마다 새 이펙트를 사용해야 한다.
            DeactivateActiveEffect();
            activeEffect = VfxManager.TryAcquire(entry.vfxId);
            if (activeEffect == null || !activeEffect.IsValid)
            {
                activeEffect = null;
                activeEffectIndex = -1;
                activeEffectAttackSequenceId = -1;
                return null;
            }

            activeEffectIndex = comboIndex;
            activeEffectAttackSequenceId = attackSequenceId;
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
        activeEffectAttackSequenceId = -1;
    }

    private void ApplyEffectTransform(VfxHandle effect, SlashEffectEntry entry) // 공격 방향과 오프셋을 기준으로 참격 위치/회전/크기를 적용한다.
    {
        Vector2 attackDirection = NormalizeOrDefault(meleeAttack.CurrentAttackDirection, Vector2.right);
        Vector3 position = transform.position;
        if (meleeAttack.TryGetCurrentHitboxCenter(out Vector3 hitboxCenter))
        {
            // 참격은 실제 판정 중심을 기준으로 표시한다.
            position = hitboxCenter;
        }

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
