using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerInput))]
public class PlayerAim : MonoBehaviour
{
    // 아무 입력/마우스 기준도 없을 때 사용할 기본 방향.
    // Vector2.down은 로직 기준 화면 아래 방향이며, 애니메이션 기준으로는 Front에 대응한다.
    [SerializeField] private Vector2 defaultAimDirection = Vector2.down;

    private PlayerInput input;
    private Vector2 lastAimDirection = Vector2.down;

    public Vector2 AimDirection { get; private set; } = Vector2.down;
    public PlayerSide8 AimSide { get; private set; } = PlayerSide8.Down;

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        if (input == null)
        {
            Debug.LogError($"{nameof(PlayerAim)} on {name} requires {nameof(PlayerInput)}.", this);
            enabled = false;
            return;
        }

        lastAimDirection = defaultAimDirection.normalized;
        AimDirection = lastAimDirection;
        AimSide = PlayerFacingUtil.Quantize8OrDefault(AimDirection, PlayerSide8.Down);
    }

    private void Update()
    {
        // 조준 방향은 활뿐 아니라 근접 공격/캐릭터 방향/애니메이션에서도 재사용할 수 있다.
        AimDirection = ResolveAimDirection();
        AimSide = PlayerFacingUtil.Quantize8OrDefault(AimDirection, AimSide);
    }

    private Vector2 ResolveAimDirection()
    {
#if ENABLE_INPUT_SYSTEM
        // 1순위: 마우스 월드 위치를 기준으로 플레이어에서 마우스까지의 방향을 구한다.
        if (Mouse.current != null && Camera.main != null)
        {
            Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector2 aimDirection = worldPosition - transform.position;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                lastAimDirection = aimDirection.normalized;
                return lastAimDirection;
            }
        }
#endif

        // 2순위: 마우스 기준을 못 잡으면 이동 방향을 조준 방향으로 사용한다.
        if (input.Move.sqrMagnitude > 0.0001f)
        {
            lastAimDirection = input.Move.normalized;
            return lastAimDirection;
        }

        // 3순위: 입력이 전혀 없으면 마지막 유효 조준 방향을 유지한다.
        if (lastAimDirection.sqrMagnitude > 0.0001f)
        {
            return lastAimDirection.normalized;
        }

        return defaultAimDirection.normalized;
    }
}
