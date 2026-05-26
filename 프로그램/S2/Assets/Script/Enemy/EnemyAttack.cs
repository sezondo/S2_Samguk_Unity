using UnityEngine;

[RequireComponent(typeof(EnemyContext))]
[RequireComponent(typeof(EnemyBase))]
[RequireComponent(typeof(EnemyFSMManager))]
[RequireComponent(typeof(EnemyDetector))]
public class EnemyAttack : MonoBehaviour, IAttackInfoProvider
{
    [Header("Hitbox")]
    [SerializeField] private MeleeHitbox meleeHitbox;

    private EnemyBase enemyBase;
    private EnemyFSMManager fsm;
    private EnemyDetector detector;

    private Vector2 attackDirection = Vector2.down;
    private float attackTimer;
    private float cooldownTimer;
    private bool hitboxActivated;
    private bool hitboxDeactivated;

    public Vector2 AttackDirection => attackDirection;
    public bool IsAttacking => fsm != null && fsm.IsState(EnemyState.Attack);

    private void Awake()
    {
        EnemyContext context = GetComponent<EnemyContext>();
        context.ResolveReferences();

        enemyBase = context.Base;
        fsm = context.Fsm;
        detector = context.Detector;

        if (enemyBase == null || fsm == null || detector == null || meleeHitbox == null || !HasValidData())
        {
            Debug.LogError($"{nameof(EnemyAttack)} on {name} is missing required data or components.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDisable()
    {
        if (fsm != null)
        {
            fsm.OnStateChanged -= HandleStateChanged;
        }

        DeactivateHitbox();
    }

    private void Update()
    {
        UpdateCooldown();
        TryEnterAttack();

        if (!fsm.IsState(EnemyState.Attack))
        {
            return;
        }

        UpdateAttack();
    }

    private void TryEnterAttack()
    {
        if (cooldownTimer > 0f || !fsm.CanAttack() || !detector.IsTargetDetected || !detector.IsTargetInAttackRange)
        {
            return;
        }

        fsm.RequestState(EnemyState.Attack);
    }

    private void UpdateAttack()
    {
        EnemyMeleeAttackData attackData = ResolveAttackData();
        attackTimer += Time.deltaTime;

        if (!hitboxActivated && attackTimer >= attackData.hitboxStartTime)
        {
            ActivateHitbox(attackData);
        }

        if (!hitboxDeactivated && attackTimer >= attackData.hitboxStartTime + attackData.hitboxActiveTime)
        {
            DeactivateHitbox();
        }

        if (attackTimer >= attackData.attackDuration)
        {
            FinishAttack();
        }
    }

    private void HandleStateChanged(EnemyState previousState, EnemyState nextState)
    {
        if (previousState == EnemyState.Attack && nextState != EnemyState.Attack)
        {
            DeactivateHitbox();
        }

        if (nextState != EnemyState.Attack)
        {
            return;
        }

        attackDirection = ResolveAttackDirection();
        attackTimer = 0f;
        hitboxActivated = false;
        hitboxDeactivated = false;
    }

    private void ActivateHitbox(EnemyMeleeAttackData attackData)
    {
        hitboxActivated = true;
        hitboxDeactivated = false;

        meleeHitbox.Activate(
            transform,
            attackData.damage,
            attackData.hitboxOffset,
            attackData.hitboxSize,
            attackData.rotateHitboxToAim,
            attackData.drawDebug,
            attackData.debugColor,
            attackData.debugDrawDuration,
            attackDirection);
    }

    private void DeactivateHitbox()
    {
        hitboxDeactivated = true;

        if (meleeHitbox != null)
        {
            meleeHitbox.Deactivate();
        }
    }

    private void FinishAttack()
    {
        DeactivateHitbox();
        cooldownTimer = ResolveAttackData().cooldown;

        fsm.RequestState(detector.IsTargetDetected ? EnemyState.Chase : EnemyState.Idle);
    }

    private void UpdateCooldown()
    {
        if (cooldownTimer <= 0f)
        {
            return;
        }

        cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
    }

    private Vector2 ResolveAttackDirection()
    {
        Vector2 direction = detector.DirectionToTarget;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
    }

    private EnemyMeleeAttackData ResolveAttackData()
    {
        return enemyBase.Data.meleeAttack;
    }

    private bool HasValidData()
    {
        if (enemyBase == null || enemyBase.Data == null || enemyBase.Data.meleeAttack == null)
        {
            return false;
        }

        EnemyMeleeAttackData attackData = enemyBase.Data.meleeAttack;
        bool valid = true;
        valid &= RequireNonNegative(attackData.damage, nameof(attackData.damage));
        valid &= RequirePositive(attackData.attackDuration, nameof(attackData.attackDuration));
        valid &= RequireNonNegative(attackData.hitboxStartTime, nameof(attackData.hitboxStartTime));
        valid &= RequireNonNegative(attackData.hitboxActiveTime, nameof(attackData.hitboxActiveTime));
        valid &= RequireNonNegative(attackData.cooldown, nameof(attackData.cooldown));
        return valid;
    }

    private bool RequirePositive(float value, string fieldName)
    {
        if (value > 0f)
        {
            return true;
        }

        Debug.LogError($"{nameof(EnemyAttack)} on {name} requires {fieldName} greater than 0 in {enemyBase.Data.name}.", this);
        return false;
    }

    private bool RequireNonNegative(float value, string fieldName)
    {
        if (value >= 0f)
        {
            return true;
        }

        Debug.LogError($"{nameof(EnemyAttack)} on {name} requires {fieldName} greater than or equal to 0 in {enemyBase.Data.name}.", this);
        return false;
    }

    private bool RequireNonNegative(int value, string fieldName)
    {
        if (value >= 0)
        {
            return true;
        }

        Debug.LogError($"{nameof(EnemyAttack)} on {name} requires {fieldName} greater than or equal to 0 in {enemyBase.Data.name}.", this);
        return false;
    }
}
