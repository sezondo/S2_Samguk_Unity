using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WASD와 방향키 입력으로 카메라를 이동하고 시작 위치 기준 범위 안으로 제한한다.
/// </summary>
public class CameraKeyboardMover : MonoBehaviour
{
    [Header("Movement")]
    // 카메라가 초당 이동하는 월드 거리다.
    [SerializeField] private float moveSpeed = 8f;
    // 시작 위치를 중심으로 카메라가 X/Y축에서 이동할 수 있는 최대 거리다.
    [SerializeField] private Vector2 maxMoveDistance = new(10f, 10f);

    [Header("Input Lock")]
    // true면 행동 연출 큐가 재생되는 동안 키보드 카메라 이동을 막는다.
    [SerializeField] private bool lockWhilePresentationPlaying = true;

    // 이동 범위를 계산할 때 기준으로 사용하는 카메라 시작 위치다.
    private Vector3 startPosition;

    /// <summary>
    /// 시작 위치를 저장하고 이동 설정값이 유효한지 확인한다.
    /// </summary>
    private void Awake()
    {
        startPosition = transform.position;

        if (!HasValidData())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 매 프레임 WASD와 방향키 입력을 읽어 제한 범위 안에서 카메라를 이동한다.
    /// </summary>
    private void Update()
    {
        if (!CanReceiveMoveInput() || !TryReadMoveInput(out Vector2 inputDirection))
        {
            return;
        }

        Vector3 movement = new(inputDirection.x, inputDirection.y, 0f);
        Vector3 targetPosition = transform.position + movement * (moveSpeed * Time.deltaTime);
        transform.position = ClampPosition(targetPosition);
    }

    /// <summary>
    /// 키보드의 WASD와 방향키 상태를 합쳐 정규화된 이동 방향을 반환한다.
    /// </summary>
    private static bool TryReadMoveInput(out Vector2 inputDirection)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            inputDirection = Vector2.zero;
            return false;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        inputDirection = new Vector2(horizontal, vertical);
        if (inputDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        inputDirection.Normalize();
        return true;
    }

    /// <summary>
    /// 현재 연출 상태를 확인해 카메라 이동 입력을 받을 수 있는지 반환한다.
    /// </summary>
    private bool CanReceiveMoveInput()
    {
        return !lockWhilePresentationPlaying ||
               ActionPresentationQueue.Instance == null ||
               !ActionPresentationQueue.Instance.IsPlaying;
    }

    /// <summary>
    /// 목표 위치를 시작 위치 기준 X/Y 이동 제한 안으로 보정하고 기존 Z 위치를 유지한다.
    /// </summary>
    private Vector3 ClampPosition(Vector3 targetPosition)
    {
        targetPosition.x = Mathf.Clamp(
            targetPosition.x,
            startPosition.x - maxMoveDistance.x,
            startPosition.x + maxMoveDistance.x);
        targetPosition.y = Mathf.Clamp(
            targetPosition.y,
            startPosition.y - maxMoveDistance.y,
            startPosition.y + maxMoveDistance.y);
        targetPosition.z = startPosition.z;
        return targetPosition;
    }

    /// <summary>
    /// 카메라 이동 속도와 최대 이동 거리가 사용할 수 있는 값인지 확인한다.
    /// </summary>
    public bool HasValidData()
    {
        if (moveSpeed <= 0f)
        {
            Debug.LogError($"{nameof(CameraKeyboardMover)} on {name}의 카메라 이동 속도는 0보다 커야 합니다.", this);
            return false;
        }

        if (maxMoveDistance.x < 0f || maxMoveDistance.y < 0f)
        {
            Debug.LogError($"{nameof(CameraKeyboardMover)} on {name}의 최대 이동 거리는 0 이상이어야 합니다.", this);
            return false;
        }

        return true;
    }
}

