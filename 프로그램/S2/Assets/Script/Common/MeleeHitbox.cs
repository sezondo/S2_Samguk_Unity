using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MeleeHitbox : MonoBehaviour
{
    [Header("Target Filter")]
    // 플레이어/적 모두 같은 Hitbox를 쓸 수 있게 타격 대상은 레이어로 제한한다.
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private bool ignoreOwner = true;

    // 한 번의 공격에서 같은 대상이 여러 콜라이더로 중복 피격되는 것을 막는다.
    // 인터페이스 자체는 HashSet 키로 쓰기 애매하므로 실제 MonoBehaviour 컴포넌트를 저장한다.
    private readonly HashSet<Component> hitTargets = new();

    private Collider2D hitboxCollider;
    private Transform owner;
    private int damage;
    private Vector2 hitboxOffset;
    private Vector2 hitboxSize;
    private bool rotateHitboxToAim;
    private bool drawDebug;
    private Color debugColor;
    private float debugDrawDuration;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        // 평소에는 꺼두고 PlayerMeleeAttack이 공격 타이밍에만 켠다.
        hitboxCollider.enabled = false;

        if (!hitboxCollider.isTrigger)
        {
            Debug.LogWarning($"{nameof(MeleeHitbox)} on {name} is expected to use a trigger Collider2D.", this);
        }
    }

    public void Activate(Transform newOwner, PlayerMeleeAttackData newAttackData, Vector2 attackDirection)
    {
        if (newAttackData == null)
        {
            Deactivate();
            return;
        }

        Activate(
            newOwner,
            newAttackData.damage,
            newAttackData.hitboxOffset,
            newAttackData.hitboxSize,
            newAttackData.rotateHitboxToAim,
            newAttackData.drawDebug,
            newAttackData.debugColor,
            newAttackData.debugDrawDuration,
            attackDirection);
    }

    public void Activate(
        Transform newOwner,
        int newDamage,
        Vector2 newHitboxOffset,
        Vector2 newHitboxSize,
        bool newRotateHitboxToAim,
        bool newDrawDebug,
        Color newDebugColor,
        float newDebugDrawDuration,
        Vector2 attackDirection)
    {
        // owner는 플레이어 본인/자식 오브젝트를 때리지 않기 위해 저장한다.
        owner = newOwner;
        damage = Mathf.Max(0, newDamage);
        hitboxOffset = newHitboxOffset;
        hitboxSize = newHitboxSize;
        rotateHitboxToAim = newRotateHitboxToAim;
        drawDebug = newDrawDebug;
        debugColor = newDebugColor;
        debugDrawDuration = Mathf.Max(0f, newDebugDrawDuration);

        // 판정을 켜기 전에 Transform과 Collider 모양을 먼저 현재 공격 데이터에 맞춘다.
        ApplyHitboxTransform(attackDirection);
        ApplyColliderShape();

        // 공격이 새로 시작될 때마다 중복 피격 기록을 초기화한다.
        hitTargets.Clear();

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
        }

        DrawDebugHitbox();
    }

    public void Deactivate()
    {
        // 공격 판정 시간이 끝나면 콜라이더를 꺼서 더 이상 맞지 않게 한다.
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }

        hitTargets.Clear();
    }

    private void ApplyHitboxTransform(Vector2 attackDirection) //히트박스 위치
    {
        if (owner == null)
        {
            return;
        }

        Vector2 normalizedDirection = attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : Vector2.down;
        Vector2 offset = ResolveAttackOffset(hitboxOffset, normalizedDirection);

        // Hitbox 오브젝트는 플레이어 자식이라는 전제라 localPosition을 쓴다.
        transform.localPosition = offset;

        if (rotateHitboxToAim)
        {
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void ApplyColliderShape() // 히트박스 크기
    {
        if (hitboxCollider == null)
        {
            return;
        }

        // 현재 테스트는 BoxCollider2D가 가장 적합하지만, 다른 2D 콜라이더도 기본 대응해둔다.
        if (hitboxCollider is BoxCollider2D boxCollider)
        {
            boxCollider.size = hitboxSize;
            return;
        }

        if (hitboxCollider is CapsuleCollider2D capsuleCollider)
        {
            capsuleCollider.size = hitboxSize;
            return;
        }

        if (hitboxCollider is CircleCollider2D circleCollider)
        {
            circleCollider.radius = Mathf.Max(hitboxSize.x, hitboxSize.y) * 0.5f;
        }
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
        if (!CanHitTarget(target))
        {
            return;
        }

        // 데미지를 받을 수 있는 대상이면 구체 타입을 몰라도 공격할 수 있다.
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        Component damageableComponent = damageable as Component;
        if (damageableComponent != null)
        {
            // 같은 공격 판정이 켜져 있는 동안에는 같은 대상에게 한 번만 데미지를 준다.
            if (hitTargets.Contains(damageableComponent))
            {
                return;
            }

            hitTargets.Add(damageableComponent);
        }

        damageable.TakeDamage(damage);
    }

    private bool CanHitTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        // Hitbox가 공격자 자식이어도 자기 자신을 때리지 않도록 한다.
        if (ignoreOwner && IsOwnerObject(target))
        {
            return false;
        }

        // 플레이어 공격은 Enemy 레이어, 적 공격은 Player 레이어처럼 인스펙터에서 지정한다.
        int targetLayerMask = 1 << target.layer;
        return (targetLayers.value & targetLayerMask) != 0;
    }

    private bool IsOwnerObject(GameObject target)
    {
        return owner != null && target.transform.IsChildOf(owner);
    }

    private void DrawDebugHitbox()
    {
        if (!drawDebug)
        {
            return;
        }

        // 현재 Transform 회전을 반영한 사각형 외곽선을 그린다.
        // Unity Scene/Game 뷰에서 Gizmos가 켜져 있어야 확인하기 쉽다.
        Vector3 center = transform.position;
        Vector2 size = hitboxSize;
        Vector3 right = transform.right * (size.x * 0.5f);
        Vector3 up = transform.up * (size.y * 0.5f);

        Vector3 topRight = center + right + up;
        Vector3 topLeft = center - right + up;
        Vector3 bottomLeft = center - right - up;
        Vector3 bottomRight = center + right - up;
        float duration = debugDrawDuration;

        Debug.DrawLine(topRight, topLeft, debugColor, duration);
        Debug.DrawLine(topLeft, bottomLeft, debugColor, duration);
        Debug.DrawLine(bottomLeft, bottomRight, debugColor, duration);
        Debug.DrawLine(bottomRight, topRight, debugColor, duration);
    }

    public static Vector2 ResolveAttackOffset(Vector2 offset, Vector2 attackDirection)
    {
        Vector2 normalizedDirection = attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : Vector2.down;
        Vector2 perpendicular = new(-normalizedDirection.y, normalizedDirection.x);

        return normalizedDirection * offset.x + perpendicular * offset.y;
    }
}
