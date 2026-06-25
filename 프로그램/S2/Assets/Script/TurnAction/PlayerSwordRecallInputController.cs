using UnityEngine;

/// <summary>
/// 검 회수 입력값을 읽어 플레이어 검 회수 행동 요청으로 변환한다.
/// 실제 입력 매핑은 PlayerInputReader가 담당하고, 이 컴포넌트는 검 회수 행동 해석만 담당한다.
/// </summary>
public class PlayerSwordRecallInputController : MonoBehaviour
{
    [Header("Reference")]
    // 검 회수 행동과 플레이어 참조를 제공하는 Context다.
    [SerializeField] private PlayerContext playerContext;

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

        inputReader = playerContext.InputReader;
    }

    /// <summary>
    /// 검 회수 입력을 매 프레임 확인한다.
    /// </summary>
    private void Update()
    {
        if (inputReader.RecallSwordPressedThisFrame)
        {
            PlayerActionFlowController actionFlowController = PlayerActionFlowController.Instance;
            if (actionFlowController == null)
            {
                Debug.LogError($"{nameof(PlayerSwordRecallInputController)} on {name}에는 검 회수 행동 실행을 조정할 {nameof(PlayerActionFlowController)} 인스턴스가 필요합니다.", this);
                return;
            }

            actionFlowController.TryExecuteSwordRecall();
        }
    }

    /// <summary>
    /// 입력 처리에 필요한 필수 참조가 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasValidReference()
    {
        if (playerContext == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallInputController)} on {name}에는 {nameof(PlayerContext)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.InputReader == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallInputController)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerInputReader)} 참조가 필요합니다.", this);
            return false;
        }

        if (playerContext.SwordRecallAction == null)
        {
            Debug.LogError($"{nameof(PlayerSwordRecallInputController)} on {name}에는 {nameof(PlayerContext)}에 연결된 {nameof(PlayerSwordRecallAction)} 참조가 필요합니다.", this);
            return false;
        }

        return true;
    }
}
