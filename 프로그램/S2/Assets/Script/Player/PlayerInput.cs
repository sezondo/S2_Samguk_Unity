using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInput : MonoBehaviour
{
    private PlayerControlLock controlLock;

    public Vector2 Move { get; private set; }  //움직임
    public Vector2 Look { get; private set; }
    public bool MeleeAttackPressedThisFrame { get; private set; } // 근접공격
    public bool WeaponThrowHeld { get; private set; } // 무기 투척 조준 유지
    public bool WeaponThrowPressedThisFrame { get; private set; } // 무기 투척 조준 시작
    public bool WeaponThrowReleasedThisFrame { get; private set; } // 무기 투척 발사
    public bool DodgePressedThisFrame { get; private set; } // 회피
    public bool HackPressedThisFrame { get; private set; } // 해킹 시작
    public bool InteractPressedThisFrame { get; private set; } // 일반 상호작용
    public bool IsLookLocked => controlLock != null && controlLock.IsLookLocked;

#if ENABLE_INPUT_SYSTEM
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string meleeAttackActionName = "MeleeAttack";
    [SerializeField] private string weaponThrowActionName = "WeaponThrow";
    [SerializeField] private string dodgeActionName = "Dodge";
    [SerializeField] private string hackActionName = "Hack";
    [SerializeField] private string interactActionName = "Interact";

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction meleeAttackAction;
    private InputAction weaponThrowAction;
    private InputAction dodgeAction;
    private InputAction hackAction;
    private InputAction interactAction;
#endif

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        if (context == null)
        {
            Debug.LogError($"{nameof(PlayerInput)} on {name} requires {nameof(PlayerContext)}.", this);
            enabled = false;
            return;
        }

        context.ResolveReferences();
        controlLock = context.ControlLock;
        if (controlLock == null)
        {
            Debug.LogError($"{nameof(PlayerInput)} on {name} requires {nameof(PlayerControlLock)}.", this);
            enabled = false;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (inputActions == null)
        {
            Debug.LogError($"{nameof(PlayerInput)} on {name} requires an InputActionAsset reference.", this);
            enabled = false;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap(playerActionMapName, throwIfNotFound: false);
        if (playerMap == null)
        {
            Debug.LogError($"Action map '{playerActionMapName}' was not found in {inputActions.name}.", this);
            enabled = false;
            return;
        }

        moveAction = playerMap.FindAction(moveActionName, throwIfNotFound: false);
        lookAction = playerMap.FindAction(lookActionName, throwIfNotFound: false);
        meleeAttackAction = playerMap.FindAction(meleeAttackActionName, throwIfNotFound: false);
        weaponThrowAction = playerMap.FindAction(weaponThrowActionName, throwIfNotFound: false);
        dodgeAction = playerMap.FindAction(dodgeActionName, throwIfNotFound: false);
        hackAction = playerMap.FindAction(hackActionName, throwIfNotFound: false);
        interactAction = playerMap.FindAction(interactActionName, throwIfNotFound: false);

        if (moveAction == null)
        {
            Debug.LogError($"Action '{moveActionName}' was not found in map '{playerActionMapName}'.", this);
            enabled = false;
        }
#endif
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        inputActions?.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        inputActions?.Disable();
#endif
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // 원시 입력은 항상 먼저 읽고, 아래에서 PlayerControlLock 상태에 맞춰 공개값만 필터링한다.
        // Interact처럼 대사 진행에 필요한 입력은 잠금 대상에서 제외해야 하므로 InputAction 자체를 끄지 않는다.
        Vector2 rawMove = moveAction != null ? moveAction.ReadValue<Vector2>().normalized : Vector2.zero;
        Vector2 rawLook = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        bool rawMeleeAttackPressedThisFrame = meleeAttackAction != null && meleeAttackAction.WasPressedThisFrame();
        bool rawWeaponThrowHeld = weaponThrowAction != null && weaponThrowAction.IsPressed();
        bool rawWeaponThrowPressedThisFrame = weaponThrowAction != null && weaponThrowAction.WasPressedThisFrame();
        bool rawWeaponThrowReleasedThisFrame = weaponThrowAction != null && weaponThrowAction.WasReleasedThisFrame();
        bool rawDodgePressedThisFrame = dodgeAction != null && dodgeAction.WasPressedThisFrame();
        HackPressedThisFrame = (hackAction != null && hackAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
        InteractPressedThisFrame = (interactAction != null && interactAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
#else
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector2 rawMove = new Vector2(horizontal, vertical).normalized;
        Vector2 rawLook = Vector2.zero;
        bool rawMeleeAttackPressedThisFrame = Input.GetButtonDown("Fire1");
        bool rawWeaponThrowHeld = Input.GetButton("Fire2");
        bool rawWeaponThrowPressedThisFrame = Input.GetButtonDown("Fire2");
        bool rawWeaponThrowReleasedThisFrame = Input.GetButtonUp("Fire2");
        bool rawDodgePressedThisFrame = Input.GetKeyDown(KeyCode.LeftShift);
        HackPressedThisFrame = Input.GetKeyDown(KeyCode.E);
        InteractPressedThisFrame = Input.GetKeyDown(KeyCode.F);
#endif

        // 외부 컴포넌트는 아래 공개 프로퍼티만 읽으므로, 잠금 중에는 입력이 없었던 것처럼 보이게 한다.
        Move = controlLock.IsMovementLocked ? Vector2.zero : rawMove;
        Look = controlLock.IsLookLocked ? Vector2.zero : rawLook;
        MeleeAttackPressedThisFrame = !controlLock.IsMeleeAttackLocked && rawMeleeAttackPressedThisFrame;
        WeaponThrowHeld = !controlLock.IsWeaponThrowLocked && rawWeaponThrowHeld;
        WeaponThrowPressedThisFrame = !controlLock.IsWeaponThrowLocked && rawWeaponThrowPressedThisFrame;
        WeaponThrowReleasedThisFrame = !controlLock.IsWeaponThrowLocked && rawWeaponThrowReleasedThisFrame;
        DodgePressedThisFrame = !controlLock.IsDodgeLocked && rawDodgePressedThisFrame;
    }

    public bool IsMeleeAttackOrDodgePressed() // 마우스 클릭 및 스페이스바 헬퍼
    {
        return MeleeAttackPressedThisFrame || DodgePressedThisFrame;
    }
}
