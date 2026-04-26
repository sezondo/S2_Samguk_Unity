using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyAttackTest : MonoBehaviour
{
    // 테스트용 접촉 공격 컴포넌트.
    // 더미 적의 Trigger Collider에 붙여 플레이어 피격/무적/Dead 전이를 확인한다.
    [SerializeField] private int damage = 1;
    [SerializeField] private float damageCooldown = 0.5f;
    [SerializeField] private bool requirePlayerTag = true;

    private readonly Dictionary<Component, float> nextDamageTimes = new();
    private Collider2D attackCollider;

    private void Awake()
    {
        attackCollider = GetComponent<Collider2D>();
        if (attackCollider != null && !attackCollider.isTrigger)
        {
            Debug.LogWarning($"{nameof(EnemyAttackTest)} on {name} is expected to use a trigger Collider2D.", this);
        }
    }

    private void OnDisable()
    {
        nextDamageTimes.Clear();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }

    private void TryDamage(GameObject target)
    {
        if (requirePlayerTag && !target.CompareTag("Player"))
        {
            return;
        }

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        Component damageableComponent = damageable as Component;
        if (damageableComponent == null || !CanDamageNow(damageableComponent))
        {
            return;
        }

        if (damageable.TakeDamage(damage))
        {
            nextDamageTimes[damageableComponent] = Time.time + Mathf.Max(0f, damageCooldown);
        }
    }

    private bool CanDamageNow(Component damageableComponent)
    {
        return !nextDamageTimes.TryGetValue(damageableComponent, out float nextDamageTime)
            || Time.time >= nextDamageTime;
    }
}
