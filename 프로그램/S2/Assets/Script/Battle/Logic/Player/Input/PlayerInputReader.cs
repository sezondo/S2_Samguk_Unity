using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 원시 입력을 읽어 공개하는 입력 Reader다.
/// 현재는 임시 키/마우스 매핑을 사용하고, 이후 Unity Input Action Map 연결은 이 컴포넌트 안에서 처리한다.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    // 씬에서 원시 입력을 읽는 단일 Reader 인스턴스다.
    public static PlayerInputReader Instance { get; private set; }

    [Header("Reference")]
    // 마우스 화면 좌표를 월드 좌표로 바꿀 카메라다. 비어 있으면 Camera.main을 사용한다.
    [SerializeField] private Camera worldCamera;

    [Header("Debug Keyboard")]
    // UI나 Action Map이 붙기 전까지 키보드로 행동 선택을 검증할지 정한다.
    [SerializeField] private bool allowDebugKeyboardSelect = true;
    // 디버그 이동 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectMoveKey = Key.M;
    // 디버그 해킹 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectHackKey = Key.H;
    // 디버그 검 투척 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectSwordThrowKey = Key.T;
    // 디버그 검 회수 행동 실행에 사용할 키다.
    [SerializeField] private Key debugRecallSwordKey = Key.R;
    // 디버그 근접 공격 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectMeleeAttackKey = Key.F;
    // 디버그 총 공격 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectGunAttackKey = Key.G;

    [Header("Pointer")]
    // true면 마우스 좌클릭으로 현재 선택된 행동을 확정한다.
    [SerializeField] private bool confirmByLeftClick = true;
    // true면 우클릭으로 현재 행동 선택을 취소한다.
    [SerializeField] private bool cancelByRightClick = true;
    // true면 Escape 키로 현재 행동 선택을 취소한다.
    [SerializeField] private bool cancelByEscape = true;

    public bool SelectMovePressedThisFrame { get; private set; }
    public bool SelectHackPressedThisFrame { get; private set; }
    public bool SelectSwordThrowPressedThisFrame { get; private set; }
    public bool RecallSwordPressedThisFrame { get; private set; }
    public bool SelectMeleeAttackPressedThisFrame { get; private set; }
    public bool SelectGunAttackPressedThisFrame { get; private set; }
    public bool ConfirmPressedThisFrame { get; private set; }
    public bool CancelPressedThisFrame { get; private set; }

    /// <summary>
    /// 씬의 단일 입력 Reader 인스턴스를 등록한다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"{nameof(PlayerInputReader)}: 이미 인스턴스가 있습니다. 중복 오브젝트 {name}의 컴포넌트를 비활성화합니다.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 현재 입력 Reader가 제거될 때 전역 참조를 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 이번 프레임의 원시 입력 상태를 갱신한다.
    /// </summary>
    private void Update()
    {
        SelectMovePressedThisFrame = allowDebugKeyboardSelect &&
            debugSelectMoveKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[debugSelectMoveKey].wasPressedThisFrame;

        SelectHackPressedThisFrame = allowDebugKeyboardSelect &&
            debugSelectHackKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[debugSelectHackKey].wasPressedThisFrame;

        SelectSwordThrowPressedThisFrame = allowDebugKeyboardSelect &&
            debugSelectSwordThrowKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[debugSelectSwordThrowKey].wasPressedThisFrame;

        RecallSwordPressedThisFrame = allowDebugKeyboardSelect &&
            debugRecallSwordKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[debugRecallSwordKey].wasPressedThisFrame;

        SelectMeleeAttackPressedThisFrame = allowDebugKeyboardSelect &&
            debugSelectMeleeAttackKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[debugSelectMeleeAttackKey].wasPressedThisFrame;

        SelectGunAttackPressedThisFrame = allowDebugKeyboardSelect &&
            debugSelectGunAttackKey != Key.None &&
            Keyboard.current != null &&
            Keyboard.current[debugSelectGunAttackKey].wasPressedThisFrame;

        ConfirmPressedThisFrame = confirmByLeftClick &&
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;

        bool escapeCanceled = cancelByEscape &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;
        bool rightClickCanceled = cancelByRightClick &&
            Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame;
        CancelPressedThisFrame = escapeCanceled || rightClickCanceled;
    }

    /// <summary>
    /// 현재 마우스 화면 좌표를 보드 칸 좌표로 변환한다.
    /// </summary>
    public bool TryGetPointerGridPosition(out GridPosition gridPosition)
    {
        if (Mouse.current == null || GridManager.Instance == null)
        {
            gridPosition = GridPosition.Zero;
            return false;
        }

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            gridPosition = GridPosition.Zero;
            return false;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cameraToUse.transform.position.z));
        gridPosition = GridManager.Instance.WorldToGrid(worldPosition);
        return true;
    }
}
