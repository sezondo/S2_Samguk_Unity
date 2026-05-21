using UnityEngine;

// Enemy 루트에 붙는 참조 주머니다.
// 로직이나 튜닝 수치를 넣지 않고, Enemy 계열 컴포넌트가 공유할 참조만 모은다.
public class EnemyContext : MonoBehaviour
{
    [Header("Enemy References")]
    [SerializeField] private EnemyBase enemyBase;
    [SerializeField] private EnemyFSMManager fsm;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyDetector detector;
    [SerializeField] private EnemyMovement movement;
    [SerializeField] private EnemyAttack attack;
    [SerializeField] private EnemyAnimation enemyAnimation;
    [SerializeField] private Rigidbody2D body;

    public Transform Root => transform;
    public EnemyBase Base => enemyBase;
    public EnemyFSMManager Fsm => fsm;
    public EnemyHealth Health => health;
    public EnemyDetector Detector => detector;
    public EnemyMovement Movement => movement;
    public EnemyAttack Attack => attack;
    public IAttackInfoProvider AttackInfoProvider => attack;
    public EnemyAnimation Animation => enemyAnimation;
    public Rigidbody2D Body => body;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (enemyBase == null)
        {
            enemyBase = GetComponent<EnemyBase>();
        }

        if (fsm == null)
        {
            fsm = GetComponent<EnemyFSMManager>();
        }

        if (health == null)
        {
            health = GetComponent<EnemyHealth>();
        }

        if (detector == null)
        {
            detector = GetComponent<EnemyDetector>();
        }

        if (movement == null)
        {
            movement = GetComponent<EnemyMovement>();
        }

        if (attack == null)
        {
            attack = GetComponent<EnemyAttack>();
        }

        if (enemyAnimation == null)
        {
            enemyAnimation = GetComponent<EnemyAnimation>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

    }
}
