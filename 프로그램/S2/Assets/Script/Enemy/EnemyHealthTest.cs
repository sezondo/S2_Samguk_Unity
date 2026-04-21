using UnityEngine;

public class EnemyHealthTest : MonoBehaviour
{
    // 전투 시스템이 생기기 전까지 쓰는 테스트용 HP 컴포넌트.
    [SerializeField] private int maxHp = 3;
    [SerializeField] private bool destroyOnDeath = true;

    private int currentHp;

    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0;

    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int damage)
    {
        // 이미 죽은 대상은 추가 데미지를 받지 않는다.
        if (IsDead)
        {
            return;
        }

        // 음수 데미지가 들어와도 회복처럼 동작하지 않게 막는다.
        int appliedDamage = Mathf.Max(0, damage);
        currentHp = Mathf.Max(0, currentHp - appliedDamage);

        Debug.Log($"{name} took {appliedDamage} damage. HP: {currentHp}/{maxHp}", this);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{name} died.", this);

        // 테스트 단계에서는 죽으면 삭제하는 방식으로 확인한다.
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
