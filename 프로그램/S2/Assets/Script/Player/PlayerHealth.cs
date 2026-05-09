using System;
using UnityEngine;

[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    // PlayerData가 없거나 maxHp가 비어 있을 때 테스트용으로 사용할 기본 HP.
    [SerializeField] private int fallbackMaxHp = 5;
    // PlayerData가 없거나 invincibleDuration이 비어 있을 때 테스트용으로 사용할 기본 무적 시간.
    [SerializeField] private float fallbackInvincibleDuration = 0.6f;

    private PlayerFSMManager fsm;
    private PlayerLoadout loadout;
    private int currentHp;
    private float hitTimer;

    public int MaxHp { get; private set; }
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0 || (fsm != null && fsm.IsState(PlayerState.Dead));
    // 메인 FSM 상태가 아니라, 현재 데미지를 받을 수 있는지 판단하는 별도 축이다.
    public PlayerHitJudgment HitJudgment { get; private set; } = PlayerHitJudgment.Vulnerable;

    public event Action<int, int> OnHealthChanged; //HP가 바뀌었을 때 호출
    public event Action<PlayerHitJudgment> OnHitJudgmentChanged; //피격 판정 상태가 바뀌었을 때 호출
    public event Action OnDied; //플레이어가 죽었을 때 호출

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        fsm = context.Fsm;
        loadout = context.Loadout;
        if (fsm == null || loadout == null)
        {
            Debug.LogError($"{nameof(PlayerHealth)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        MaxHp = ResolveMaxHp();
        currentHp = MaxHp;
    }

    private void Update()
    {
        UpdateHitJudgmentTimer();
    }

    public bool TakeDamage(int damage)
    {
        if (!CanTakeDamage())
        {
            return false;
        }

        int appliedDamage = Mathf.Max(0, damage);
        if (appliedDamage <= 0)
        {
            return false;
        }

        currentHp = Mathf.Max(0, currentHp - appliedDamage);
        OnHealthChanged?.Invoke(currentHp, MaxHp);

        Debug.Log($"{name} took {appliedDamage} damage. HP: {currentHp}/{MaxHp}", this);

        if (currentHp <= 0)
        {
            Die();
            return true;
        }

        EnterHitJudgment();
        return true;
    }

    public void Heal(int amount)
    {
        if (IsDead)
        {
            return;
        }

        int healAmount = Mathf.Max(0, amount);
        if (healAmount <= 0)
        {
            return;
        }

        currentHp = Mathf.Min(MaxHp, currentHp + healAmount);
        OnHealthChanged?.Invoke(currentHp, MaxHp);
    }

    public void ResetHealth()
    {
        MaxHp = ResolveMaxHp();
        currentHp = MaxHp;
        SetHitJudgment(PlayerHitJudgment.Vulnerable);
        OnHealthChanged?.Invoke(currentHp, MaxHp);
    }

    private int ResolveMaxHp()
    {
        // 실제 밸런스 값은 PlayerData를 우선하고, 없을 때만 fallback 값을 쓴다.
        PlayerData playerData = loadout != null ? loadout.PlayerData : null;
        if (playerData != null && playerData.maxHp > 0)
        {
            return playerData.maxHp;
        }

        return Mathf.Max(1, fallbackMaxHp);
    }

    private float ResolveInvincibleDuration()
    {
        // 실제 밸런스 값은 PlayerData를 우선하고, 없을 때만 fallback 값을 쓴다.
        PlayerData playerData = loadout != null ? loadout.PlayerData : null;
        if (playerData != null && playerData.invincibleDuration > 0f)
        {
            return playerData.invincibleDuration;
        }

        return Mathf.Max(0f, fallbackInvincibleDuration);
    }

    private bool CanTakeDamage()
    {
        // Dead와 Dodge는 메인 FSM 기준으로 데미지를 받지 않는 상태다.
        if (fsm.IsState(PlayerState.Dead))
        {
            return false;
        }

        if (fsm.IsState(PlayerState.Dodge))
        {
            return false;
        }

        // Invincible은 피격 직후 추가 데미지를 막기 위한 별도 판정 상태다.
        if (HitJudgment == PlayerHitJudgment.Invincible)
        {
            return false;
        }

        return true;
    }

    private void EnterHitJudgment()
    {
        hitTimer = ResolveInvincibleDuration();

        if (hitTimer <= 0f)
        {
            SetHitJudgment(PlayerHitJudgment.Vulnerable);
            return;
        }

        SetHitJudgment(PlayerHitJudgment.Invincible);
    }

    private void UpdateHitJudgmentTimer()
    {
        if (HitJudgment != PlayerHitJudgment.Invincible)
        {
            return;
        }

        hitTimer = Mathf.Max(0f, hitTimer - Time.deltaTime);
        if (hitTimer <= 0f)
        {
            SetHitJudgment(PlayerHitJudgment.Vulnerable);
        }
    }

    private void SetHitJudgment(PlayerHitJudgment nextJudgment)
    {
        if (HitJudgment == nextJudgment)
        {
            return;
        }

        HitJudgment = nextJudgment;
        OnHitJudgmentChanged?.Invoke(HitJudgment);
    }

    private void Die()
    {
        // 사망은 일반 상태 전이가 아니라 어떤 상태에서도 Dead로 고정되는 강제 전이다.
        SetHitJudgment(PlayerHitJudgment.Vulnerable);
        fsm.ForceDead();
        OnDied?.Invoke();
        Debug.Log($"{name} died.", this);
    }
}
