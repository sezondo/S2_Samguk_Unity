using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 비주얼 노벨 Story의 진행과 스킵 입력을 읽어 현재 Runner에 전달한다.
/// </summary>
public class StoryInputReader : MonoBehaviour
{
    [Header("Reference")]
    // 입력 요청을 전달할 Story Runner다.
    [SerializeField] private StoryRunner runner;

    /// <summary>
    /// 입력을 전달할 Runner 참조를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 좌클릭·Enter·Space 진행 입력과 Escape 스킵 입력을 처리한다.
    /// </summary>
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        bool advanceRequested =
            (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame));

        if (advanceRequested)
        {
            runner.RequestAdvance();
        }

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            runner.RequestSkip();
        }
    }

    /// <summary>
    /// Story 입력 전달에 필요한 Runner 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (runner != null)
        {
            return true;
        }

        Debug.LogError($"{nameof(StoryInputReader)} on {name}에는 {nameof(StoryRunner)} 참조가 필요합니다.", this);
        return false;
    }
}
