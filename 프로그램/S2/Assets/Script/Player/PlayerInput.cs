using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInput : MonoBehaviour
{
    public Vector2 Move { get; private set; }  //움직임
    public Vector2 Look { get; private set; }
    public bool MeleeAttackPressedThisFrame { get; private set; } // 근접공격
    public bool WeaponThrowHeld { get; private set; } // 무기 투척 조준 유지
    public bool WeaponThrowPressedThisFrame { get; private set; } // 무기 투척 조준 시작
    public bool WeaponThrowReleasedThisFrame { get; private set; } // 무기 투척 발사
    public bool DodgePressedThisFrame { get; private set; } // 회피
    public bool HackPressedThisFrame { get; private set; } // 해킹 시작
    public bool InteractPressedThisFrame { get; private set; } // 일반 상호작용

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
        Move = moveAction != null ? moveAction.ReadValue<Vector2>().normalized : Vector2.zero;
        Look = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        MeleeAttackPressedThisFrame = meleeAttackAction != null && meleeAttackAction.WasPressedThisFrame();
        WeaponThrowHeld = weaponThrowAction != null && weaponThrowAction.IsPressed();
        WeaponThrowPressedThisFrame = weaponThrowAction != null && weaponThrowAction.WasPressedThisFrame();
        WeaponThrowReleasedThisFrame = weaponThrowAction != null && weaponThrowAction.WasReleasedThisFrame();
        DodgePressedThisFrame = dodgeAction != null && dodgeAction.WasPressedThisFrame();
        HackPressedThisFrame = (hackAction != null && hackAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
        InteractPressedThisFrame = (interactAction != null && interactAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame);
#else
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Move = new Vector2(horizontal, vertical).normalized;
        Look = Vector2.zero;
        MeleeAttackPressedThisFrame = Input.GetButtonDown("Fire1");
        WeaponThrowHeld = Input.GetButton("Fire2");
        WeaponThrowPressedThisFrame = Input.GetButtonDown("Fire2");
        WeaponThrowReleasedThisFrame = Input.GetButtonUp("Fire2");
        DodgePressedThisFrame = Input.GetKeyDown(KeyCode.LeftShift);
        HackPressedThisFrame = Input.GetKeyDown(KeyCode.E);
        InteractPressedThisFrame = Input.GetKeyDown(KeyCode.F);
#endif
    }

    public bool IsMeleeAttackOrDodgePressed() // 마우스 클릭 및 스페이스바 헬퍼
    {
        return MeleeAttackPressedThisFrame || DodgePressedThisFrame;
    }
}
