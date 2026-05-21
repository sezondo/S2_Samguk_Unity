using System;
using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyFSMManager))]
public class EnemyHealth : MonoBehaviour, IDamageable
{
    private EnemyBase enemyBase;
    private EnemyFSMManager fsm;
    private int currentHp;
    private EnemyState stateBeforeHit = EnemyState.Idle;
    private float hitTimer;

    public int MaxHp => enemyBase != null && enemyBase.Data != null ? enemyBase.Data.maxHp : 0;
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0 || (fsm != null && fsm.IsState(EnemyState.Dead));

    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    private void Awake()
    {
        EnemyContext context = GetComponent<EnemyContext>();
        context.ResolveReferences();

        enemyBase = context.Base;
        fsm = context.Fsm;

        if (enemyBase == null || fsm == null || !HasValidData())
        {
            Debug.LogError($"{nameof(EnemyHealth)} on {name} is missing required data or components.", this);
            enabled = false;
            return;
        }

        currentHp = enemyBase.Data.maxHp;
    }

    private void OnEnable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Update()
    {
        if (!fsm.IsState(EnemyState.Hit))
        {
            return;
        }

        hitTimer -= Time.deltaTime;
        if (hitTimer <= 0f)
        {
            fsm.RequestState(stateBeforeHit == EnemyState.Attack ? EnemyState.Chase : stateBeforeHit);
        }
    }

    public bool TakeDamage(int damage)
    {
        if (IsDead)
        {
            return false;
        }

        int appliedDamage = Mathf.Max(0, damage);
        if (appliedDamage <= 0)
        {
            return false;
        }

        currentHp = Mathf.Max(0, currentHp - appliedDamage);
        OnHealthChanged?.Invoke(currentHp, enemyBase.Data.maxHp);

        Debug.Log($"{name} took {appliedDamage} damage. HP: {currentHp}/{enemyBase.Data.maxHp}", this);

        if (currentHp <= 0)
        {
            Die();
            return true;
        }

        if (fsm.IsState(EnemyState.Hit))
        {
            hitTimer = Mathf.Max(0f, enemyBase.Data.hitStateDuration);
        }
        else
        {
            fsm.RequestState(EnemyState.Hit);
        }
        return true;
    }

    private void HandleStateChanged(EnemyState previousState, EnemyState nextState)
    {
        if (nextState != EnemyState.Hit)
        {
            return;
        }

        stateBeforeHit = previousState;
        hitTimer = Mathf.Max(0f, enemyBase.Data.hitStateDuration);
    }

    private bool HasValidData()
    {
        if (enemyBase == null || enemyBase.Data == null)
        {
            return false;
        }

        if (enemyBase.Data.maxHp <= 0)
        {
            Debug.LogError($"{nameof(EnemyHealth)} on {name} requires maxHp greater than 0 in {enemyBase.Data.name}.", this);
            return false;
        }

        if (enemyBase.Data.hitStateDuration < 0f)
        {
            Debug.LogError($"{nameof(EnemyHealth)} on {name} requires hitStateDuration greater than or equal to 0 in {enemyBase.Data.name}.", this);
            return false;
        }

        return true;
    }

    private void Die()
    {
        fsm.ForceDead();
        OnDied?.Invoke();
        Debug.Log($"{name} died.", this);

        if (enemyBase.Data.destroyOnDeath) // 추후에 사망 연출 필요
        {
            Destroy(gameObject);
        }
    }
}
