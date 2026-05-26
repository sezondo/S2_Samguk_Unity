using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
[RequireComponent(typeof(EnemyBase))]
public class EnemyDetector : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool usePlayerTagFallback = true;

    [Header("Sight")]
    [SerializeField] private Vector2 initialFacingDirection = Vector2.down;

    private EnemyBase enemyBase;
    private EnemyMovement movement;
    private Vector2 lastFacingDirection = Vector2.down;
    private EnemyDetectionState detectionState = EnemyDetectionState.PlayerUndetected;

    public Transform Target => target;
    public EnemyDetectionState DetectionState => detectionState;
    public bool HasTarget => target != null && IsTargetInDetectionRange;
    public bool IsTargetInDetectionRange => target != null && DistanceToTarget <= ResolveDetectionRange();
    public bool IsTargetInAttackRange => target != null && DistanceToTarget <= ResolveAttackRange();
    public bool IsTargetInViewAngle => target != null && IsDirectionInViewAngle(DirectionToTarget);
    public bool HasLineOfSight => target != null && !IsSightBlocked();
    public bool CanSeeTarget => IsTargetInDetectionRange && IsTargetInViewAngle && HasLineOfSight;
    public bool IsTargetDetected => target != null && detectionState == EnemyDetectionState.PlayerDetected;
    public float DistanceToTarget => target != null ? Vector2.Distance(transform.position, target.position) : float.PositiveInfinity;
    public Vector2 DirectionToTarget => target != null
        ? ((Vector2)target.position - (Vector2)transform.position).normalized
        : Vector2.zero;

    private void Awake()
    {
        EnemyContext context = GetComponent<EnemyContext>();
        context.ResolveReferences();

        enemyBase = context.Base;
        movement = context.Movement;
        lastFacingDirection = ResolveInitialFacingDirection();
        if (enemyBase == null || enemyBase.Data == null)
        {
            Debug.LogError($"{nameof(EnemyDetector)} on {name} requires {nameof(EnemyData)}.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        ResolveTargetIfNeeded();
        UpdateDetectionState();
        UpdateFacingDirection();
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

    private void UpdateDetectionState()
    {
        if (target == null || detectionState == EnemyDetectionState.PlayerDetected)
        {
            return;
        }

        // 한 번 시야로 플레이어를 관측하면 이후에는 거리/시야각/장애물 판정을 무시하고 추적한다.
        if (CanSeeTarget)
        {
            detectionState = EnemyDetectionState.PlayerDetected;
        }
    }

    public void ForgetDetectedTarget()
    {
        detectionState = EnemyDetectionState.PlayerUndetected;
    }

    private void UpdateFacingDirection()
    {
        if (movement == null || movement.MoveDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // 정지 중에는 마지막 방향을 유지해서 등 뒤 감지 테스트가 가능하게 둔다.
        lastFacingDirection = movement.MoveDirection.normalized;
    }

    private bool IsDirectionInViewAngle(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        float viewAngle = ResolveViewAngle();
        if (viewAngle >= 360f)
        {
            return true;
        }

        return Vector2.Angle(lastFacingDirection, direction) <= viewAngle * 0.5f;
    }

    private bool IsSightBlocked()
    {
        LayerMask blockLayers = ResolveSightBlockLayers();
        if (blockLayers.value == 0)
        {
            return false;
        }

        Vector2 origin = transform.position;
        Vector2 destination = target.position;
        RaycastHit2D hit = Physics2D.Linecast(origin, destination, blockLayers);
        return hit.collider != null;
    }

    private Vector2 ResolveInitialFacingDirection()
    {
        return initialFacingDirection.sqrMagnitude > 0.0001f ? initialFacingDirection.normalized : Vector2.down;
    }

    private float ResolveViewAngle()
    {
        return enemyBase != null && enemyBase.Data != null ? Mathf.Clamp(enemyBase.Data.viewAngle, 0f, 360f) : 0f;
    }

    private LayerMask ResolveSightBlockLayers()
    {
        return enemyBase != null && enemyBase.Data != null ? enemyBase.Data.sightBlockLayers : default;
    }

    private void OnDrawGizmosSelected()
    {
        EnemyData data = enemyBase != null ? enemyBase.Data : GetComponent<EnemyBase>()?.Data;
        if (data == null || !data.drawSightDebug)
        {
            return;
        }

        Vector2 origin = transform.position;
        Vector2 facing = Application.isPlaying ? lastFacingDirection : ResolveInitialFacingDirection();
        float range = Mathf.Max(0f, data.detectionRange);
        float halfAngle = Mathf.Clamp(data.viewAngle, 0f, 360f) * 0.5f;

        Gizmos.color = data.sightDebugColor;
        Gizmos.DrawWireSphere(origin, range);
        Gizmos.DrawLine(origin, origin + Rotate(facing, -halfAngle) * range);
        Gizmos.DrawLine(origin, origin + Rotate(facing, halfAngle) * range);

        if (target != null)
        {
            Gizmos.color = IsTargetDetected ? Color.green : Color.red;
            Gizmos.DrawLine(origin, target.position);
        }
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }
}
