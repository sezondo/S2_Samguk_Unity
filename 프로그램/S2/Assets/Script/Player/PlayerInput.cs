using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInput : MonoBehaviour
{
    public Vector2 Move { get; private set; }  //움직임
    public Vector2 Look { get; private set; }
    public bool MeleeAttackPressedThisFrame { get; private set; } // 근접공격
    public bool BowHeld { get; private set; } // 활 홀드
    public bool BowPressedThisFrame { get; private set; } // 활 공격 때기
    public bool BowReleasedThisFrame { get; private set; } // 활 공격 사출
    public bool PreviousArrowPressedThisFrame { get; private set; } // 이전 화살 선택
    public bool NextArrowPressedThisFrame { get; private set; } // 다음 화살 선택
    public bool DodgePressedThisFrame { get; private set; } // 회피
    public bool InteractPressedThisFrame { get; private set; } // 상호작용

#if ENABLE_INPUT_SYSTEM
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string meleeAttackActionName = "MeleeAttack";
    [SerializeField] private string bowActionName = "Bow";
    [SerializeField] private string dodgeActionName = "Dodge";
    [SerializeField] private string interactActionName = "Interact";

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction meleeAttackAction;
    private InputAction bowAction;
    private InputAction dodgeAction;
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
        bowAction = playerMap.FindAction(bowActionName, throwIfNotFound: false);
        dodgeAction = playerMap.FindAction(dodgeActionName, throwIfNotFound: false);
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
        BowHeld = bowAction != null && bowAction.IsPressed();
        BowPressedThisFrame = bowAction != null && bowAction.WasPressedThisFrame();
        BowReleasedThisFrame = bowAction != null && bowAction.WasReleasedThisFrame();
        PreviousArrowPressedThisFrame = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        NextArrowPressedThisFrame = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        DodgePressedThisFrame = dodgeAction != null && dodgeAction.WasPressedThisFrame();
        InteractPressedThisFrame = interactAction != null && interactAction.WasPressedThisFrame();
#else
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Move = new Vector2(horizontal, vertical).normalized;
        Look = Vector2.zero;
        MeleeAttackPressedThisFrame = Input.GetButtonDown("Fire1");
        BowHeld = Input.GetButton("Fire2");
        BowPressedThisFrame = Input.GetButtonDown("Fire2");
        BowReleasedThisFrame = Input.GetButtonUp("Fire2");
        PreviousArrowPressedThisFrame = Input.GetKeyDown(KeyCode.Q);
        NextArrowPressedThisFrame = Input.GetKeyDown(KeyCode.E);
        DodgePressedThisFrame = Input.GetKeyDown(KeyCode.LeftShift);
        InteractPressedThisFrame = false;
#endif
    }

    public bool IsMeleeAttackOrDodgePressed() // 마우스 클릭 및 스페이스바 헬퍼
    {
        return MeleeAttackPressedThisFrame || DodgePressedThisFrame;
    }
}
