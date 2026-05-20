using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
[RequireComponent(typeof(EnemyBase))]
public class EnemyDetector : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool usePlayerTagFallback = true;

    private EnemyBase enemyBase;

    public Transform Target => target;
    public bool HasTarget => target != null && IsTargetInDetectionRange;
    public bool IsTargetInDetectionRange => target != null && DistanceToTarget <= ResolveDetectionRange();
    public bool IsTargetInAttackRange => target != null && DistanceToTarget <= ResolveAttackRange();
    public float DistanceToTarget => target != null ? Vector2.Distance(transform.position, target.position) : float.PositiveInfinity;
    public Vector2 DirectionToTarget => target != null
        ? ((Vector2)target.position - (Vector2)transform.position).normalized
        : Vector2.zero;

    private void Awake()
    {
        EnemyContext context = GetComponent<EnemyContext>();
        context.ResolveReferences();

        enemyBase = context.Base;
        if (enemyBase == null || enemyBase.Data == null)
        {
            Debug.LogError($"{nameof(EnemyDetector)} on {name} requires {nameof(EnemyData)}.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        ResolveTargetIfNeeded();
    }

    private void ResolveTargetIfNeeded()
    {
        if (target != null || !usePlayerTagFallback)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    private float ResolveDetectionRange()
    {
        return enemyBase != null && enemyBase.Data != null ? Mathf.Max(0f, enemyBase.Data.detectionRange) : 0f;
    }

    private float ResolveAttackRange()
    {
        return enemyBase != null && enemyBase.Data != null ? Mathf.Max(0f, enemyBase.Data.attackRange) : 0f;
    }
}
