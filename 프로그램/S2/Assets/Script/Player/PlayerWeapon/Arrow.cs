using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Arrow : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private Rigidbody2D rb;
    private float lifeTimer;
    private ArrowData arrowData;
    private int arrowTypeId;
    private int damage;

    // 피격 처리나 속성 효과에서 어떤 화살인지 확인할 때 사용한다.
    public ArrowData ArrowData => arrowData;
    public int ArrowTypeId => arrowTypeId;
    public int Damage => damage;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lifeTimer = lifeTime;
    }

    protected virtual void OnEnable()
    {
        lifeTimer = lifeTime;
    }

    public virtual void Initialize(Vector2 direction, ArrowData newArrowData)
    {
        // 발사 직후 필요한 초기값은 PlayerBow가 넘겨주고,
        // 이후 이동/수명/충돌 책임은 Arrow가 가진다.
        arrowData = newArrowData;
        arrowTypeId = arrowData != null ? arrowData.arrowTypeId : 0;
        damage = arrowData != null ? Mathf.Max(0, arrowData.damage) : 0;
        float speed = arrowData != null ? arrowData.projectileSpeed : 0f;

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;

        transform.right = normalizedDirection;
        rb.linearVelocity = normalizedDirection * speed;
    }

    protected virtual void Update()
    {
        // 기본 화살은 일정 시간이 지나면 자동으로 제거된다.
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        // 발사 직후 플레이어 콜라이더와 닿아도 화살이 사라지지 않게 한다.
        if (IsPlayerObject(other.gameObject))
        {
            return;
        }

        HandleHit(other.gameObject);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        // Trigger가 아닌 충돌에서도 Player 태그는 무시한다.
        if (IsPlayerObject(collision.gameObject))
        {
            return;
        }

        HandleHit(collision.gameObject);
    }

    private static bool IsPlayerObject(GameObject target)
    {
        return target.CompareTag("Player");
    }

    protected virtual void HandleHit(GameObject otherObject)
    {
        // 테스트용 적 HP가 있으면 데미지를 적용한다.
        EnemyHealthTest enemyHealth = otherObject.GetComponentInParent<EnemyHealthTest>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
        }

        // 기본 화살은 충돌 후 자기 자신을 제거한다.
        // 속성 화살은 이 메서드를 override 해서 효과를 확장하면 된다.
        Destroy(gameObject);
    }
}
