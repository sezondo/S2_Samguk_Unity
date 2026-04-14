using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Arrow : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private Rigidbody2D rb;
    private float lifeTimer;
    private int arrowTypeId;

    public int ArrowTypeId => arrowTypeId;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lifeTimer = lifeTime;
    }

    protected virtual void OnEnable()
    {
        lifeTimer = lifeTime;
    }

    public virtual void Initialize(Vector2 direction, float speed, int newArrowTypeId)
    {
        // 발사 직후 필요한 초기값은 PlayerBow가 넘겨주고,
        // 이후 이동/수명/충돌 책임은 Arrow가 가진다.
        arrowTypeId = newArrowTypeId;

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
        HandleHit(other.gameObject);
    }

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    protected virtual void HandleHit(GameObject otherObject)
    {
        // 기본 화살은 맞은 대상을 세부 처리하지 않고 일단 자기 자신만 제거한다.
        // 속성 화살은 이 메서드를 override 해서 효과를 확장하면 된다.
        Destroy(gameObject);
    }
}
