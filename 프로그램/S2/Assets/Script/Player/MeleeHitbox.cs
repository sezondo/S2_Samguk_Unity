using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MeleeHitbox : MonoBehaviour
{
    private readonly HashSet<EnemyHealthTest> hitEnemies = new();

    private Collider2D hitboxCollider;
    private Transform owner;
    private int damage;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        hitboxCollider.enabled = false;

        if (!hitboxCollider.isTrigger)
        {
            Debug.LogWarning($"{nameof(MeleeHitbox)} on {name} is expected to use a trigger Collider2D.", this);
        }
    }

    public void Activate(Transform newOwner, int newDamage)
    {
        owner = newOwner;
        damage = Mathf.Max(0, newDamage);
        hitEnemies.Clear();

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
        }
    }

    public void Deactivate()
    {
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        hitEnemies.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.gameObject);
    }

    private void TryHit(GameObject target)
    {
        if (IsOwnerObject(target) || target.CompareTag("Player"))
        {
            return;
        }

        EnemyHealthTest enemyHealth = target.GetComponentInParent<EnemyHealthTest>();
        if (enemyHealth == null || hitEnemies.Contains(enemyHealth))
        {
            return;
        }

        hitEnemies.Add(enemyHealth);
        enemyHealth.TakeDamage(damage);
    }

    private bool IsOwnerObject(GameObject target)
    {
        return owner != null && target.transform.IsChildOf(owner);
    }
}
