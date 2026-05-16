using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    private DialoguePlaybackSession activeSession;
    private PlayerInput activeInput;
    private int nextSessionId = 1;

    public bool IsPlaying => activeSession != null;

    public static event Action<DialoguePlaybackContext> GlobalDialogueStarted;
    public static event Action<DialoguePlaybackContext, DialogueLineData, int> GlobalDialogueLineChanged;
    public static event Action<DialoguePlaybackContext> GlobalDialogueFinished;

    public event Action<DialoguePlaybackContext> DialogueStarted;
    public event Action<DialoguePlaybackContext, DialogueLineData, int> DialogueLineChanged;
    public event Action<DialoguePlaybackContext> DialogueFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{nameof(DialogueManager)} already exists. Only one global dialogue manager is allowed.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (activeSession == null)
        {
            return;
        }

        DialogueLineData line = GetCurrentLine();
        if (line == null)
        {
            FinishActiveSession();
            return;
        }

        if (CanAdvanceByInput(line) || CanAdvanceByTime(line))
        {
            AdvanceActiveSession();
        }
    }

    public bool TryPlay(DialogueSequenceData sequence, DialogueSpeaker[] speakers, DialogueEventTrigger source, PlayerInput input)
    {
        if (activeSession != null)
        {
            Debug.LogWarning($"{nameof(DialogueManager)} rejected dialogue '{GetSequenceName(sequence)}' because another dialogue is already playing.", this);
            return false;
        }

        if (!HasValidRequest(sequence, speakers, input))
        {
            return false;
        }

        DialoguePlaybackContext context = new(nextSessionId++, sequence, source);
        activeSession = new DialoguePlaybackSession(context, speakers, Time.frameCount);
        activeInput = input;

        DialogueStarted?.Invoke(context);
        GlobalDialogueStarted?.Invoke(context);
        AdvanceActiveSession();
        return true;
    }

    private void AdvanceActiveSession()
    {
        if (activeSession == null)
        {
            return;
        }

        activeSession.CurrentSpeaker?.HideLine();
        activeSession.CurrentLineIndex++;
        activeSession.AutoAdvanceTimer = 0f;

        DialogueLineData line = GetCurrentLine();
        if (line == null)
        {
            FinishActiveSession();
            return;
        }

        DialogueSpeaker speaker = ResolveSpeaker(line.speakerTag);
        if (speaker == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} could not find speakerTag '{line.speakerTag}' in dialogue '{activeSession.Context.Sequence.name}'. Dialogue stopped.", this);
            FinishActiveSession();
            return;
        }

        activeSession.CurrentSpeaker = speaker;
        speaker.ShowLine(line.text);
        DialogueLineChanged?.Invoke(activeSession.Context, line, activeSession.CurrentLineIndex);
        GlobalDialogueLineChanged?.Invoke(activeSession.Context, line, activeSession.CurrentLineIndex);
    }

    private void FinishActiveSession()
    {
        if (activeSession == null)
        {
            return;
        }

        DialoguePlaybackContext context = activeSession.Context;
        activeSession.CurrentSpeaker?.HideLine();
        activeSession = null;
        activeInput = null;

        DialogueFinished?.Invoke(context);
        GlobalDialogueFinished?.Invoke(context);
    }

    private bool CanAdvanceByInput(DialogueLineData line)
    {
        if (!line.advanceByInput || activeInput == null)
        {
            return false;
        }

        // 대사를 시작한 F 입력이 같은 프레임에 첫 줄 넘김으로 소비되지 않게 막는다.
        if (Time.frameCount == activeSession.StartedFrame)
        {
            return false;
        }

        return activeInput.InteractPressedThisFrame;
    }

    private bool CanAdvanceByTime(DialogueLineData line)
    {
        if (!line.advanceByTime)
        {
            return false;
        }

        activeSession.AutoAdvanceTimer += Time.deltaTime;
        return activeSession.AutoAdvanceTimer >= Mathf.Max(0f, line.autoAdvanceTime);
    }

    private DialogueLineData GetCurrentLine()
    {
        if (activeSession == null)
        {
            return null;
        }

        DialogueSequenceData sequence = activeSession.Context.Sequence;
        int index = activeSession.CurrentLineIndex;
        if (sequence == null || sequence.lines == null || index < 0 || index >= sequence.lines.Length)
        {
            return null;
        }

        return sequence.lines[index];
    }

    private DialogueSpeaker ResolveSpeaker(string speakerTag)
    {
        if (string.IsNullOrWhiteSpace(speakerTag) || activeSession.Speakers == null)
        {
            return null;
        }

        for (int i = 0; i < activeSession.Speakers.Length; i++)
        {
            DialogueSpeaker speaker = activeSession.Speakers[i];
            if (speaker != null && speaker.HasTag(speakerTag))
            {
                return speaker;
            }
        }

        return null;
    }

    private bool HasValidRequest(DialogueSequenceData sequence, DialogueSpeaker[] speakers, PlayerInput input)
    {
        if (sequence == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} received a null dialogue sequence.", this);
            return false;
        }

        if (sequence.lines == null || sequence.lines.Length == 0)
        {
            Debug.LogError($"{nameof(DialogueManager)} received empty dialogue sequence '{sequence.name}'.", this);
            return false;
        }

        if (speakers == null || speakers.Length == 0)
        {
            Debug.LogError($"{nameof(DialogueManager)} received dialogue '{sequence.name}' without speakers.", this);
            return false;
        }

        if (input == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} received dialogue '{sequence.name}' without player input provider.", this);
            return false;
        }

        return true;
    }

    private static string GetSequenceName(DialogueSequenceData sequence)
    {
        return sequence != null ? sequence.name : "null";
    }
}
