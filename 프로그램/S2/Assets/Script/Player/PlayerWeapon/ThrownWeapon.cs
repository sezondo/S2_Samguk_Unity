using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ThrownWeapon : MonoBehaviour
{
    private enum ThrowPhase
    {
        Outbound,
        Embedded,
        Returning,
    }

    [SerializeField] private float recoverDistance = 0.25f;
    // 새 연출 구조에서는 PlayerWeaponVisualFSM이 보이는 검을 담당하고,
    // 이 투척체는 충돌/데미지/회수 판정만 맡는다.
    [SerializeField] private bool hideVisualOnInitialize = true;

    private Rigidbody2D rb;
    private PlayerWeaponThrow owner;
    private Vector2 startPosition;
    private float throwSpeed;
    private float returnSpeed;
    private float maxDistance;
    private int damage;
    private ThrowPhase phase;

    public bool IsReturning => phase == ThrowPhase.Returning;
    public bool IsEmbeddedForHack => phase == ThrowPhase.Embedded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void Initialize(
        PlayerWeaponThrow newOwner,
        Vector2 direction,
        PlayerWeaponThrowData throwData)
    {
        if (throwData == null)
        {
            Debug.LogError($"{nameof(ThrownWeapon)} on {name} requires {nameof(PlayerWeaponThrowData)}.", this);
            Destroy(gameObject);
            return;
        }

        owner = newOwner;
        startPosition = transform.position;
        throwSpeed = throwData.throwSpeed;
        returnSpeed = throwData.returnSpeed;
        maxDistance = throwData.maxDistance;
        damage = throwData.damage;
        phase = ThrowPhase.Outbound;
        ApplyVisualVisibility(!hideVisualOnInitialize);

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

        if (phase == ThrowPhase.Embedded)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Returning일때
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

        // 공격 판정은 날아가는 중에만 처리한다.
        // 해킹 대상으로 박힌 상태와 회수 중에는 추가 충돌/데미지/해킹 감지를 모두 무시한다.
        if (phase != ThrowPhase.Outbound)
        {
            return;
        }

        if (TryEmbedForHack(target))
        {
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

    private bool TryEmbedForHack(GameObject target)
    {
        IHackable hackable = target.GetComponentInParent<IHackable>();
        if (hackable == null || owner == null)
        {
            return false;
        }

        phase = ThrowPhase.Embedded;
        rb.linearVelocity = Vector2.zero;
        owner.RegisterEmbeddedHackWeapon(this, hackable);
        return true;
    }

    private void BeginReturn()
    {
        if (phase == ThrowPhase.Returning)
        {
            return;
        }

        phase = ThrowPhase.Returning;
    }

    public void BeginReturnFromHack()
    {
        BeginReturn();
    }

    private void ApplyVisualVisibility(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            targetRenderer.enabled = visible;
        }
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
