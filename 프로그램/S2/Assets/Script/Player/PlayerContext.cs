using UnityEngine;

// 플레이어 루트에 붙는 참조 주머니다.
// 로직이나 튜닝 수치를 가지지 않고, 여러 컴포넌트가 공통으로 읽는 플레이어 컴포넌트 참조만 모은다.
public class PlayerContext : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerInput input;
    [SerializeField] private PlayerAim aim;
    [SerializeField] private PlayerFSMManager fsm;
    [SerializeField] private PlayerAnim anim;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerControlLock controlLock;
    [SerializeField] private PlayerDodge dodge;
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerLoadout loadout;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private PlayerWeaponThrow weaponThrow;
    [SerializeField] private PlayerMeleeAttack meleeAttack;
    [SerializeField] private PlayerMeleeSlashEffect meleeSlashEffect;
    [SerializeField] private PlayerHackController hackController;

    public Transform Root => transform;
    public PlayerInput Input => input;
    public PlayerAim Aim => aim;
    public PlayerFSMManager Fsm => fsm;
    public PlayerAnim Anim => anim;
    public PlayerMovement Movement => movement;
    public PlayerControlLock ControlLock => controlLock;
    public PlayerDodge Dodge => dodge;
    public PlayerHealth Health => health;
    public PlayerLoadout Loadout => loadout;
    public Rigidbody2D Body => body;
    public PlayerWeaponThrow WeaponThrow => weaponThrow;
    public PlayerMeleeAttack MeleeAttack => meleeAttack;
    public PlayerMeleeSlashEffect MeleeSlashEffect => meleeSlashEffect;
    public PlayerHackController HackController => hackController;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (input == null)
        {
            input = GetComponent<PlayerInput>();
        }

        if (aim == null)
        {
            aim = GetComponent<PlayerAim>();
        }

        if (fsm == null)
        {
            fsm = GetComponent<PlayerFSMManager>();
        }

        if (anim == null)
        {
            anim = GetComponent<PlayerAnim>();
        }

        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (controlLock == null)
        {
            controlLock = GetComponent<PlayerControlLock>();
        }

        if (dodge == null)
        {
            dodge = GetComponent<PlayerDodge>();
        }

        if (health == null)
        {
            health = GetComponent<PlayerHealth>();
        }

        if (loadout == null)
        {
            loadout = GetComponent<PlayerLoadout>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (weaponThrow == null)
        {
            weaponThrow = GetComponent<PlayerWeaponThrow>();
        }

        if (meleeAttack == null)
        {
            meleeAttack = GetComponent<PlayerMeleeAttack>();
        }

        if (meleeSlashEffect == null)
        {
            meleeSlashEffect = GetComponent<PlayerMeleeSlashEffect>();
        }

        if (hackController == null)
        {
            hackController = GetComponent<PlayerHackController>();
        }
    }

    public bool HasWeaponVisualRequiredReferences()
    {
        return input != null
            && aim != null
            && fsm != null
            && anim != null
            && weaponThrow != null;
    }
}
