using UnityEngine;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerAim))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MeleeHitbox meleeHitbox;

    [Header("Melee Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private float hitboxStartTime = 0f;
    [SerializeField] private float hitboxActiveTime = 0.08f;
    [SerializeField] private float hitboxDistance = 0.6f;
    [SerializeField] private bool rotateHitboxToAim = true;

    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerAim aim;

    private float attackTimer;
    private bool hitboxActivated;
    private bool hitboxDeactivated;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        fsm = GetComponent<PlayerFSMManager>();
        aim = GetComponent<PlayerAim>();

        if (meleeHitbox == null)
        {
            meleeHitbox = GetComponentInChildren<MeleeHitbox>(true);
        }

        if (input == null || fsm == null || aim == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttack)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        if (meleeHitbox == null)
        {
            Debug.LogWarning($"{nameof(PlayerMeleeAttack)} on {name} has no melee hitbox assigned.", this);
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
        if (fsm.IsState(PlayerState.Idle) && input.MeleeAttackPressedThisFrame)
        {
            fsm.RequestState(PlayerState.MeleeAttack);
        }

        if (!fsm.IsState(PlayerState.MeleeAttack))
        {
            return;
        }

        UpdateAttack();
    }

    private void HandleStateChanged(PlayerState previousState, PlayerState nextState)
    {
        if (previousState == PlayerState.MeleeAttack && nextState != PlayerState.MeleeAttack)
        {
            DeactivateHitbox();
        }

        if (nextState != PlayerState.MeleeAttack)
        {
            return;
        }

        attackTimer = 0f;
        hitboxActivated = false;
        hitboxDeactivated = false;
        DeactivateHitbox(false);
    }

    private void UpdateAttack()
    {
        attackTimer += Time.deltaTime;

        if (!hitboxActivated && attackTimer >= hitboxStartTime)
        {
            ActivateHitbox();
        }

        if (!hitboxDeactivated && attackTimer >= hitboxStartTime + hitboxActiveTime)
        {
            DeactivateHitbox(true);
        }

        if (attackTimer >= attackDuration)
        {
            fsm.RequestState(PlayerState.Idle);
        }
    }

    private void ActivateHitbox()
    {
        hitboxActivated = true;
        hitboxDeactivated = false;

        if (meleeHitbox == null)
        {
            return;
        }

        Vector2 attackDirection = ResolveAttackDirection();
        Transform hitboxTransform = meleeHitbox.transform;
        hitboxTransform.localPosition = attackDirection * hitboxDistance;

        if (rotateHitboxToAim)
        {
            float angle = Mathf.Atan2(attackDirection.y, attackDirection.x) * Mathf.Rad2Deg;
            hitboxTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        meleeHitbox.Activate(transform, damage);
    }

    private void DeactivateHitbox(bool markDeactivated = true)
    {
        if (markDeactivated)
        {
            hitboxDeactivated = true;
        }

        if (meleeHitbox != null)
        {
            meleeHitbox.Deactivate();
        }
    }

    private Vector2 ResolveAttackDirection()
    {
        Vector2 attackDirection = aim.AimDirection;
        return attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : Vector2.down;
    }
}
