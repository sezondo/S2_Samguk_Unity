using UnityEngine;

public class EnemyHealthTest : MonoBehaviour
{
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
        if (IsDead)
        {
            return;
        }

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

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
