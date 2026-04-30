using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ThrownWeapon : MonoBehaviour
{
    private enum ThrowPhase
    {
        Outbound,
        Returning,
    }

    [SerializeField] private float recoverDistance = 0.25f;

    private Rigidbody2D rb;
    private PlayerWeaponThrow owner;
    private Vector2 startPosition;
    private float throwSpeed;
    private float returnSpeed;
    private float maxDistance;
    private int damage;
    private ThrowPhase phase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(
        PlayerWeaponThrow newOwner,
        Vector2 direction,
        PlayerWeaponThrowData throwData)
    {
        owner = newOwner;
        startPosition = transform.position;
        throwSpeed = throwData != null ? Mathf.Max(0f, throwData.throwSpeed) : 0f;
        returnSpeed = throwData != null ? Mathf.Max(0f, throwData.returnSpeed) : 0f;
        maxDistance = throwData != null ? Mathf.Max(0f, throwData.maxDistance) : 0f;
        damage = throwData != null ? Mathf.Max(0, throwData.damage) : 0;
        phase = ThrowPhase.Outbound;

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;

        transform.right = normalizedDirection;
        rb.linearVelocity = normalizedDirection * throwSpeed;
    }

    private void FixedUpdate()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (phase == ThrowPhase.Outbound)
        {
            if (Vector2.Distance(startPosition, rb.position) >= maxDistance)
            {
                BeginReturn();
            }

            return;
        }

        Vector2 ownerPosition = owner.transform.position;
        Vector2 toOwner = ownerPosition - rb.position;
        if (toOwner.magnitude <= recoverDistance)
        {
            owner.RecoverWeapon(this);
            Destroy(gameObject);
            return;
        }

        Vector2 returnDirection = toOwner.normalized;
        transform.right = returnDirection;
        rb.linearVelocity = returnDirection * returnSpeed;
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
        if (target == null)
        {
            return;
        }

        if (IsOwnerObject(target))
        {
            TryRecoverByOwnerHit();
            return;
        }

        TryDamage(target);
        BeginReturn();
    }

    private void TryRecoverByOwnerHit()
    {
        if (phase != ThrowPhase.Returning || owner == null)
        {
            return;
        }

        owner.RecoverWeapon(this);
        Destroy(gameObject);
    }

    private void BeginReturn()
    {
        if (phase == ThrowPhase.Returning)
        {
            return;
        }

        phase = ThrowPhase.Returning;
    }

    private void TryDamage(GameObject target)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(damage);
    }

    private bool IsOwnerObject(GameObject target)
    {
        return owner != null && target.transform.IsChildOf(owner.transform);
    }
}
