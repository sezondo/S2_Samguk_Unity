using UnityEngine;

/// <summary>
/// 근접 공격 관련 입력값을 읽어 플레이어 근접 공격 행동 요청으로 변환한다.
/// 실제 입력 매핑은 PlayerInputReader가 담당하고, 이 컴포넌트는 근접 공격 행동 해석만 담당한다.
/// </summary>
public class PlayerMeleeAttackInputController : MonoBehaviour
{
    [Header("Reference")]
    // 근접 공격 행동과 플레이어 참조를 제공하는 Context다.
    [SerializeField] private PlayerContext playerContext;

    // 입력 요청을 받을 근접 공격 행동 컴포넌트다.
    private PlayerMeleeAttackAction meleeAttackAction;
    // PlayerContext에서 꺼내 쓰는 플레이어 입력 Reader다.
    private PlayerInputReader inputReader;

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

        meleeAttackAction = playerContext.MeleeAttackAction;
        inputReader = playerContext.InputReader;
    }

    /// <summary>
    /// 근접 공격 선택, 취소, 목표 칸 클릭 입력을 매 프레임 확인한다.
    /// </summary>
    private void Update()
    {
        if (inputReader.SelectMeleeAttackPressedThisFrame)
        {
            meleeAttackAction.SelectMeleeAttackAction();
        }

        if (meleeAttackAction == null || !meleeAttackAction.IsMeleeAttackSelected)
        {
            return;
        }

        if (inputReader.CancelPressedThisFrame)
        {
            meleeAttackAction.CancelMeleeAttackAction();
            return;
        }

        if (!inputReader.ConfirmPressedThisFrame)
        {
            return;
        }

        if (!inputReader.TryGetPointerGridPosition(out GridPosition targetPosition))
        {
            return;
        }

        PlayerActionFlowController actionFlowController = PlayerActionFlowController.Instance;
        if (actionFlowController == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackInputController)} on {name}에는 근접 공격 실행을 조정할 {nameof(PlayerActionFlowController)} 인스턴스가 필요합니다.", this);
            return;
        }

        actionFlowController.TryExecuteMeleeAttack(targetPosition);
    }

    /// <summary>
    /// 입력 처리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackInputController)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.InputReader == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackInputController)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerInputReader)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.MeleeAttackAction == null)
        {
            Debug.LogError($"{nameof(PlayerMeleeAttackInputController)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerMeleeAttackAction)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
