using System;
using UnityEngine;

[RequireComponent(typeof(PlayerMeleeAttack))]
[RequireComponent(typeof(PlayerFSMManager))]
public class PlayerMeleeSlashEffect : MonoBehaviour
{
    [Serializable]
    private class SlashEffectEntry
    {
        [Header("Prefab")]
        // 이 콤보 단계에서 사용할 참격 이펙트 프리팹이다.
        public GameObject effectPrefab;

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

    [Header("References")]
    // 이펙트를 생성할 부모다. 비워두면 이 컴포넌트가 붙은 플레이어 Transform 아래에 만든다.
    [SerializeField] private Transform effectRoot;

    [Header("Combo Effects")]
    // PlayerMeleeAttack.CurrentComboIndex와 같은 번호의 이펙트 설정을 사용한다.
    [SerializeField] private SlashEffectEntry[] comboEffects;

    private PlayerMeleeAttack meleeAttack;
    private PlayerFSMManager fsm;
    private GameObject[] pooledEffects;
    private int activeEffectIndex = -1;
    private float previousNormalizedTime;

    private void Awake() // 참격 이펙트 풀을 미리 만들고 필요한 플레이어 참조를 찾는다.
    {
        meleeAttack = GetComponent<PlayerMeleeAttack>();
        fsm = GetComponent<PlayerFSMManager>();

        if (effectRoot == null)
        {
            effectRoot = transform;
        }

        if (meleeAttack == null || fsm == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeSlashEffect)} on {name} is missing required components.", this);
            enabled = false;
            return;
        }

        BuildPool();
    }

    private void OnDisable() // 컴포넌트가 꺼질 때 켜져 있던 참격 이펙트를 정리한다.
    {
        DeactivateActiveEffect();
    }

    private void LateUpdate() // 공격 진행률을 읽어 콤보 번호에 맞는 참격 이펙트를 켜고 위치/회전을 갱신한다.
    {
        if (!fsm.IsState(PlayerState.MeleeAttack))
        {
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
            DeactivateActiveEffect();
        }

        previousNormalizedTime = normalizedTime;

        if (!ShouldShowEffect(entry, normalizedTime))
        {
            DeactivateActiveEffect();
            return;
        }

        GameObject effect = ActivateEffect(comboIndex);
        if (effect == null)
        {
            return;
        }

        ApplyEffectTransform(effect.transform, entry);
    }

    private void BuildPool() // 콤보 이펙트 배열을 기준으로 비활성 풀 오브젝트를 미리 생성한다.
    {
        int count = comboEffects != null ? comboEffects.Length : 0;
        pooledEffects = new GameObject[count];

        for (int i = 0; i < count; i++)
        {
            SlashEffectEntry entry = comboEffects[i];
            if (entry == null || entry.effectPrefab == null)
            {
                continue;
            }

            GameObject effect = Instantiate(entry.effectPrefab, effectRoot);
            effect.name = $"{entry.effectPrefab.name}_Combo{i + 1}";
            effect.SetActive(false);
            pooledEffects[i] = effect;
        }
    }

    private SlashEffectEntry GetEntry(int comboIndex) // 현재 콤보 번호에 맞는 이펙트 설정을 가져온다.
    {
        if (comboEffects == null || comboIndex < 0 || comboIndex >= comboEffects.Length)
        {
            return null;
        }

        SlashEffectEntry entry = comboEffects[comboIndex];
        return entry != null && entry.effectPrefab != null ? entry : null;
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

    private GameObject ActivateEffect(int comboIndex) // 풀에서 현재 콤보 이펙트를 켜고, 다른 이펙트는 끈다.
    {
        if (pooledEffects == null || comboIndex < 0 || comboIndex >= pooledEffects.Length)
        {
            return null;
        }

        if (activeEffectIndex != comboIndex)
        {
            DeactivateActiveEffect();
            activeEffectIndex = comboIndex;
        }

        GameObject effect = pooledEffects[comboIndex];
        if (effect != null && !effect.activeSelf)
        {
            effect.SetActive(true);
        }

        return effect;
    }

    private void DeactivateActiveEffect() // 현재 켜져 있는 참격 이펙트를 끈다.
    {
        if (pooledEffects != null && activeEffectIndex >= 0 && activeEffectIndex < pooledEffects.Length)
        {
            GameObject activeEffect = pooledEffects[activeEffectIndex];
            if (activeEffect != null)
            {
                activeEffect.SetActive(false);
            }
        }

        activeEffectIndex = -1;
    }

    private void ApplyEffectTransform(Transform effectTransform, SlashEffectEntry entry) // 공격 방향과 오프셋을 기준으로 참격 위치/회전/크기를 적용한다.
    {
        Vector2 attackDirection = NormalizeOrDefault(meleeAttack.CurrentAttackDirection, Vector2.right);
        Vector2 perpendicular = new(-attackDirection.y, attackDirection.x);
        Vector2 worldOffset = attackDirection * entry.offset.x + perpendicular * entry.offset.y;

        effectTransform.position = (Vector2)transform.position + worldOffset;
        effectTransform.rotation = Quaternion.Euler(0f, 0f, DirectionToAngle(attackDirection) + entry.angleOffset);
        effectTransform.localScale = new Vector3(entry.scale.x, entry.scale.y, 1f);
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
