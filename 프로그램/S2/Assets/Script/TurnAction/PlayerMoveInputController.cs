using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 이동 행동 선택과 목표 칸 클릭 입력을 처리하는 임시 입력 컨트롤러다.
/// 실제 이동 판정과 실행은 PlayerGridMoveAction과 PlayerActionFlowController에 맡긴다.
/// </summary>
public class PlayerMoveInputController : MonoBehaviour
{
    [Header("Reference")]
    // 이동 행동과 플레이어 참조를 제공하는 Context다.
    [SerializeField] private PlayerContext playerContext;
    // 행동 실행 흐름을 조정하는 필수 컨트롤러다.
    [SerializeField] private PlayerActionFlowController actionFlowController;
    // 마우스 화면 좌표를 월드 좌표로 바꿀 카메라다. 비어 있으면 Camera.main을 사용한다.
    [SerializeField] private Camera worldCamera;

    [Header("Input")]
    // UI 버튼이 붙기 전까지 키보드로 이동 행동 선택을 검증할지 정한다.
    [SerializeField] private bool allowDebugKeyboardSelect = true;
    // 디버그 이동 행동 선택에 사용할 키다.
    [SerializeField] private Key debugSelectMoveKey = Key.M;
    // true면 우클릭으로 현재 이동 행동 선택을 취소한다.
    [SerializeField] private bool cancelByRightClick = true;
    // true면 Escape 키로 현재 이동 행동 선택을 취소한다.
    [SerializeField] private bool cancelByEscape = true;

    // 입력 요청을 받을 이동 행동 컴포넌트다.
    private PlayerGridMoveAction moveAction;

    /// <summary>
    /// 입력 처리에 필요한 참조를 확인한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
            return;
        }

        moveAction = playerContext.GridMoveAction;
    }

    /// <summary>
    /// 이동 선택, 취소, 목표 칸 클릭 입력을 매 프레임 확인한다.
    /// </summary>
    private void Update()
    {
        HandleDebugSelectInput();

        if (moveAction == null || !moveAction.IsMoveSelected)
        {
            return;
        }

        HandleCancelInput();
        if (!moveAction.IsMoveSelected)
        {
            return;
        }

        if (!TryGetMouseGridPosition(out GridPosition targetPosition))
        {
            moveAction.ClearMovePathPreview();
            return;
        }

        moveAction.RefreshPathPreview(targetPosition);
        HandleTargetClickInput(targetPosition);
    }

    /// <summary>
    /// UI 버튼이 없을 때 사용할 디버그 이동 행동 선택 키 입력을 처리한다.
    /// </summary>
    private void HandleDebugSelectInput()
    {
        if (!allowDebugKeyboardSelect || debugSelectMoveKey == Key.None || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[debugSelectMoveKey].wasPressedThisFrame)
        {
            moveAction.SelectMoveAction();
        }
    }

    /// <summary>
    /// 이동 행동 선택 상태에서 취소 입력을 처리한다.
    /// </summary>
    private void HandleCancelInput()
    {
        if (cancelByEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            moveAction.CancelMoveAction();
            return;
        }

        if (cancelByRightClick && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            moveAction.CancelMoveAction();
        }
    }

    /// <summary>
    /// 이동 행동 선택 상태에서 마우스 좌클릭 목표 칸 입력을 처리한다.
    /// </summary>
    private void HandleTargetClickInput(GridPosition targetPosition)
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        actionFlowController.TryExecuteMove(targetPosition);
    }

    /// <summary>
    /// 현재 마우스 화면 좌표를 보드 칸 좌표로 변환한다.
    /// </summary>
    private bool TryGetMouseGridPosition(out GridPosition gridPosition)
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

    /// <summary>
    /// 입력 처리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerMoveInputController)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (!playerContext.HasValidReference())
        {
            return false;
        }

        if (playerContext.GridMoveAction == null)
        {
            Debug.LogError($"{nameof(PlayerMoveInputController)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerGridMoveAction)} 참조가 필요합니다.", this);
            return false;
        }

        if (actionFlowController == null)
        {
            Debug.LogError($"{nameof(PlayerMoveInputController)} on {name}에는 이동 행동 실행을 조정할 {nameof(PlayerActionFlowController)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
