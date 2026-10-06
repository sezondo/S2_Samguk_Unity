using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Story 명령 목록을 분기 없이 순서대로 실행하고 완료 결과를 외부에 알린다.
/// </summary>
public class StoryRunner : MonoBehaviour
{
    [Header("Reference")]
    // Story 입력과 각 화면 Presenter 참조를 제공하는 Context다.
    [SerializeField] private StoryContext context;

    // 현재 재생 중인 Story 데이터다.
    private StorySequenceData activeSequence;
    // 현재 실행 중이거나 입력을 기다리는 명령 인덱스다.
    private int currentCommandIndex = -1;
    // 현재 Runner의 명령 실행 또는 입력 대기 단계다.
    private StoryPlaybackState playbackState = StoryPlaybackState.Idle;
    // 현재 Story가 이미 본 Story로 판단되어 스킵 가능한지 나타낸다.
    private bool allowSkip;
    // 현재 Story 완료 이벤트를 이미 발생시켰는지 나타낸다.
    private bool completionRaised;
    // Wait 명령이 실행 중일 때 보관하는 대기 코루틴이다.
    private Coroutine waitRoutine;

    public StorySequenceData ActiveSequence => activeSequence;
    public int CurrentCommandIndex => currentCommandIndex;
    public StoryPlaybackState PlaybackState => playbackState;
    public bool IsPlaying => activeSequence != null;
    public bool CanSkip => IsPlaying && allowSkip;

    // Story가 정상 완료되거나 스킵됐을 때 시퀀스와 완료 이유를 한 번 전달한다.
    public event Action<StorySequenceData, StoryCompletionReason> StoryCompleted;

    /// <summary>
    /// 필수 Context와 Presenter 참조를 검사한다.
    /// </summary>
    private void Awake()
    {
        if (!HasValidReference())
        {
            enabled = false;
        }
    }

    /// <summary>
    /// 지정한 Story 시퀀스를 처음 명령부터 선형으로 재생한다.
    /// </summary>
    public bool Play(StorySequenceData sequence, bool canSkip)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogError($"{nameof(StoryRunner)} on {name}이 비활성화되어 Story를 시작할 수 없습니다.", this);
            return false;
        }

        if (IsPlaying)
        {
            Debug.LogWarning($"{nameof(StoryRunner)}: 이미 '{activeSequence.DisplayName}' Story를 재생 중입니다.", this);
            return false;
        }

        if (!HasValidReference() || !HasValidData(sequence))
        {
            return false;
        }

        activeSequence = sequence;
        currentCommandIndex = -1;
        playbackState = StoryPlaybackState.Executing;
        allowSkip = canSkip;
        completionRaised = false;

        context.DialoguePresenter.HideImmediately();
        context.StandingPresenter.HideAllImmediately();
        context.ComicPanelPresenter.HideImmediately();
        context.BackgroundPresenter.ResetComicCut();

        ExecuteCommandsUntilBlocked();
        return true;
    }

    /// <summary>
    /// 현재 타이핑을 완성하거나 입력을 기다리는 다음 Story 명령으로 진행한다.
    /// </summary>
    public void RequestAdvance()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (playbackState == StoryPlaybackState.WaitingForDialogueInput)
        {
            if (!context.DialoguePresenter.IsTextComplete)
            {
                context.DialoguePresenter.CompleteText();
                return;
            }

            playbackState = StoryPlaybackState.Executing;
            ExecuteCommandsUntilBlocked();
            return;
        }

        if (playbackState == StoryPlaybackState.WaitingForComicInput)
        {
            playbackState = StoryPlaybackState.Executing;
            ExecuteCommandsUntilBlocked();
        }
    }

    /// <summary>
    /// 이미 본 Story로 허용된 경우 현재 재생을 정리하고 스킵 완료를 알린다.
    /// </summary>
    public void RequestSkip()
    {
        if (!CanSkip)
        {
            return;
        }

        StopActiveRoutines();
        context.DialoguePresenter.HideImmediately();
        context.StandingPresenter.HideAllImmediately();
        context.ComicPanelPresenter.HideImmediately();
        context.BackgroundPresenter.ResetComicCut();
        context.FadePresenter.SetAlphaImmediately(0f);
        Finish(StoryCompletionReason.Skipped);
    }

    /// <summary>
    /// 즉시 완료되는 명령을 연속 실행하고 입력이나 시간 완료가 필요할 때 멈춘다.
    /// </summary>
    private void ExecuteCommandsUntilBlocked()
    {
        if (!IsPlaying || playbackState != StoryPlaybackState.Executing)
        {
            return;
        }

        StoryCommandData[] commands = activeSequence.Commands;
        while (playbackState == StoryPlaybackState.Executing)
        {
            currentCommandIndex++;
            if (currentCommandIndex >= commands.Length)
            {
                Finish(StoryCompletionReason.Completed);
                return;
            }

            if (!ExecuteCommand(commands[currentCommandIndex]))
            {
                Debug.LogError(
                    $"{nameof(StoryRunner)}: '{activeSequence.DisplayName}' Story의 {currentCommandIndex}번 {commands[currentCommandIndex].CommandType} 명령 실행에 실패했습니다.",
                    this);
                AbortPlayback();
                return;
            }
        }
    }

    /// <summary>
    /// 현재 명령 종류에 맞는 Presenter 또는 시간 대기 기능을 실행한다.
    /// </summary>
    private bool ExecuteCommand(StoryCommandData command)
    {
        switch (command.CommandType)
        {
            case StoryCommandType.Dialogue:
                if (!context.DialoguePresenter.ShowDialogue(
                        command.SpeakerName,
                        command.DialogueText,
                        command.UseTypewriter,
                        command.CharactersPerSecond,
                        command.HideSpeakerName))
                {
                    return false;
                }

                playbackState = StoryPlaybackState.WaitingForDialogueInput;
                return true;

            case StoryCommandType.HideDialogue:
                context.DialoguePresenter.HideImmediately();
                return true;

            case StoryCommandType.ShowComicCut:
                playbackState = StoryPlaybackState.WaitingForTimedCommand;
                return context.BackgroundPresenter.PlayComicCut(
                    command.VisualSprite, command.ComicCutVertices, command.Duration,
                    HandleTimedCommandCompleted);

            case StoryCommandType.ChangeBackground:
                return context.BackgroundPresenter.ShowBackground(command.VisualSprite);

            case StoryCommandType.ShowStanding:
                return context.StandingPresenter.ShowStanding(command.StandingPosition, command.VisualSprite);

            case StoryCommandType.HideStanding:
                return context.StandingPresenter.HideStanding(command.StandingPosition);

            case StoryCommandType.ChangeStandingExpression:
                return context.StandingPresenter.ChangeExpression(command.StandingPosition, command.VisualSprite);

            case StoryCommandType.ChangeStandingFocus:
                return context.StandingPresenter.SetFocus(command.StandingPosition);

            case StoryCommandType.ShowComicPanel:
                if (!context.ComicPanelPresenter.Show(command.ComicPanelSprite))
                {
                    return false;
                }

                playbackState = StoryPlaybackState.WaitingForComicInput;
                return true;

            case StoryCommandType.HideComicPanel:
                context.ComicPanelPresenter.HideImmediately();
                return true;

            case StoryCommandType.Fade:
                playbackState = StoryPlaybackState.WaitingForTimedCommand;
                return context.FadePresenter.PlayFade(
                    command.FadeDirection,
                    command.Duration,
                    HandleTimedCommandCompleted);

            case StoryCommandType.Wait:
                playbackState = StoryPlaybackState.WaitingForTimedCommand;
                waitRoutine = StartCoroutine(WaitRoutine(command.Duration));
                return true;

            default:
                Debug.LogError($"{nameof(StoryRunner)}가 알 수 없는 Story 명령 {command.CommandType}을 받았습니다.", this);
                return false;
        }
    }

    /// <summary>
    /// 페이드 또는 컷 진입 전환이 끝난 뒤 다음 선형 Story 명령을 실행한다.
    /// </summary>
    private void HandleTimedCommandCompleted()
    {
        if (!IsPlaying || playbackState != StoryPlaybackState.WaitingForTimedCommand)
        {
            return;
        }

        playbackState = StoryPlaybackState.Executing;
        ExecuteCommandsUntilBlocked();
    }

    /// <summary>
    /// 비스케일 시간으로 Wait 명령을 처리한 뒤 다음 Story 명령을 실행한다.
    /// </summary>
    private IEnumerator WaitRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        waitRoutine = null;
        if (IsPlaying && playbackState == StoryPlaybackState.WaitingForTimedCommand)
        {
            playbackState = StoryPlaybackState.Executing;
            ExecuteCommandsUntilBlocked();
        }
    }

    /// <summary>
    /// 현재 Story 상태를 완료로 정리하고 외부 완료 이벤트를 정확히 한 번 발생시킨다.
    /// </summary>
    private void Finish(StoryCompletionReason reason)
    {
        if (!IsPlaying || completionRaised)
        {
            return;
        }

        StorySequenceData finishedSequence = activeSequence;
        completionRaised = true;
        activeSequence = null;
        currentCommandIndex = -1;
        playbackState = StoryPlaybackState.Completed;
        allowSkip = false;
        StopActiveRoutines();

        StoryCompleted?.Invoke(finishedSequence, reason);
    }

    /// <summary>
    /// 데이터 또는 Presenter 오류가 발생한 현재 Story를 완료 통지 없이 중단한다.
    /// </summary>
    private void AbortPlayback()
    {
        StopActiveRoutines();
        context.FadePresenter.StopFade();
        context.BackgroundPresenter.ResetComicCut();
        activeSequence = null;
        currentCommandIndex = -1;
        playbackState = StoryPlaybackState.Idle;
        allowSkip = false;
        completionRaised = false;
    }

    /// <summary>
    /// Runner가 관리하는 Wait·페이드·컷 진입 전환을 중단한다.
    /// </summary>
    private void StopActiveRoutines()
    {
        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        if (context != null && context.BackgroundPresenter != null)
        {
            context.BackgroundPresenter.StopComicCutTransition();
        }

        if (context != null && context.FadePresenter != null)
        {
            context.FadePresenter.StopFade();
        }
    }

    /// <summary>
    /// Story Runner에 필요한 Context와 내부 Presenter 참조가 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasValidReference()
    {
        if (context == null)
        {
            Debug.LogError($"{nameof(StoryRunner)} on {name}에는 {nameof(StoryContext)} 참조가 필요합니다.", this);
            return false;
        }

        return context.HasValidReference();
    }

    /// <summary>
    /// 시퀀스 식별값과 모든 선형 명령 데이터가 현재 명령 규칙에 맞는지 검사한다.
    /// </summary>
    public bool HasValidData(StorySequenceData sequence)
    {
        if (sequence == null)
        {
            Debug.LogError($"{nameof(StoryRunner)}가 비어 있는 Story 시퀀스를 받았습니다.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sequence.StoryId) || string.IsNullOrWhiteSpace(sequence.DisplayName))
        {
            Debug.LogError($"{nameof(StoryRunner)}: {sequence.name}의 Story ID와 표시 이름이 필요합니다.", sequence);
            return false;
        }

        StoryCommandData[] commands = sequence.Commands;
        if (commands == null || commands.Length == 0)
        {
            Debug.LogError($"{nameof(StoryRunner)}: '{sequence.DisplayName}' Story에는 명령이 하나 이상 필요합니다.", sequence);
            return false;
        }

        for (int i = 0; i < commands.Length; i++)
        {
            if (!HasValidCommandData(sequence, commands[i], i))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Story 명령 하나가 해당 명령 종류에 필요한 필수 값을 갖는지 검사한다.
    /// </summary>
    private bool HasValidCommandData(StorySequenceData sequence, StoryCommandData command, int commandIndex)
    {
        if (command == null)
        {
            Debug.LogError($"{nameof(StoryRunner)}: '{sequence.DisplayName}' Story의 {commandIndex}번 명령이 비어 있습니다.", sequence);
            return false;
        }

        bool isValid = command.CommandType switch
        {
            StoryCommandType.Dialogue =>
                !string.IsNullOrWhiteSpace(command.SpeakerName) &&
                !string.IsNullOrWhiteSpace(command.DialogueText) &&
                (!command.UseTypewriter || command.CharactersPerSecond > 0f),

            StoryCommandType.ShowComicCut =>
                !string.IsNullOrWhiteSpace(command.ComicCutId) &&
                context.BackgroundPresenter.HasValidData(command.VisualSprite, command.ComicCutVertices, command.Duration),

            StoryCommandType.ChangeBackground =>
                command.VisualSprite != null,

            StoryCommandType.ShowStanding or StoryCommandType.ChangeStandingExpression =>
                command.StandingPosition != StoryStandingPosition.None &&
                command.VisualSprite != null,

            StoryCommandType.HideStanding =>
                command.StandingPosition != StoryStandingPosition.None,

            StoryCommandType.ChangeStandingFocus =>
                Enum.IsDefined(typeof(StoryStandingPosition), command.StandingPosition),

            StoryCommandType.ShowComicPanel =>
                command.ComicPanelSprite != null,

            StoryCommandType.HideComicPanel or StoryCommandType.HideDialogue =>
                true,

            StoryCommandType.Fade or StoryCommandType.Wait =>
                command.Duration > 0f,

            _ => false,
        };

        if (!isValid)
        {
            Debug.LogError(
                $"{nameof(StoryRunner)}: '{sequence.DisplayName}' Story의 {commandIndex}번 {command.CommandType} 명령 데이터가 올바르지 않습니다.",
                sequence);
        }

        return isValid;
    }
}
