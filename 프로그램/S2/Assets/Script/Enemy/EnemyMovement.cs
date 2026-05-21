using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyFSMManager))]
[RequireComponent(typeof(EnemyDetector))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    private EnemyBase enemyBase;
    private EnemyFSMManager fsm;
    private EnemyDetector detector;
    private Rigidbody2D rb;

    public Vector2 MoveDirection { get; private set; }

    private void Awake()
    {
        EnemyContext context = GetComponent<EnemyContext>();
        context.ResolveReferences();

        enemyBase = context.Base;
        fsm = context.Fsm;
        detector = context.Detector;
        rb = context.Body;

        if (enemyBase == null || fsm == null || detector == null || rb == null || !HasValidData())
        {
            Debug.LogError($"{nameof(EnemyMovement)} on {name} is missing required data or components.", this);
            enabled = false;
            return;
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Update()
    {
        UpdateStateByDetection();
    }

    private void FixedUpdate()
    {
        if (!fsm.CanMove() || !detector.HasTarget || detector.DistanceToTarget <= ResolveStopDistance())
        {
            Stop();
            return;
        }

        MoveDirection = detector.DirectionToTarget;
        Vector2 nextPosition = rb.position + MoveDirection * ResolveMoveSpeed() * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private void UpdateStateByDetection()
    {
        if (fsm.IsState(EnemyState.Dead) || fsm.IsState(EnemyState.Attack) || fsm.IsState(EnemyState.Hit))
        {
            return;
        }

        if (detector.HasTarget && !detector.IsTargetInAttackRange)
        {
            fsm.RequestState(EnemyState.Chase);
            return;
        }

        if (!detector.HasTarget && fsm.IsState(EnemyState.Chase))
        {
            fsm.RequestState(EnemyState.Idle);
        }
    }

    private void Stop()
    {
        MoveDirection = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }

    private float ResolveMoveSpeed()
    {
        return enemyBase.Data.moveSpeed;
    }

    private float ResolveStopDistance()
    {
        return Mathf.Max(0f, enemyBase.Data.stopDistance);
    }

    private bool HasValidData()
    {
        if (enemyBase == null || enemyBase.Data == null)
        {
            return false;
        }

        if (enemyBase.Data.moveSpeed < 0f || enemyBase.Data.detectionRange < 0f || enemyBase.Data.attackRange < 0f || enemyBase.Data.stopDistance < 0f)
        {
            Debug.LogError($"{nameof(EnemyMovement)} on {name} requires non-negative movement values in {enemyBase.Data.name}.", this);
            return false;
        }

        return true;
    }
}
