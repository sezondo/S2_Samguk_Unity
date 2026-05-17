using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Bubble")]
    [SerializeField] private DialogueBubblePresenter bubblePresenter;

    private DialoguePlaybackSession activeSession;
    private PlayerInput activeInput;
    private int nextSessionId = 1;

    public bool IsPlaying => activeSession != null;
    
    // 대사 시작됨
    public static event Action<DialoguePlaybackContext> GlobalDialogueStarted;
    // 대사 스텝 바뀜
    public static event Action<DialoguePlaybackContext, DialogueStepData, int> GlobalDialogueStepChanged;
    //대사 끝남
    public static event Action<DialoguePlaybackContext> GlobalDialogueFinished;

    public event Action<DialoguePlaybackContext> DialogueStarted;
    public event Action<DialoguePlaybackContext, DialogueStepData, int> DialogueStepChanged;
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

        if (bubblePresenter == null)
        {
            bubblePresenter = GetComponentInChildren<DialogueBubblePresenter>(true);
        }
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

        DialogueStepData step = GetCurrentStep();
        if (step == null)
        {
            FinishActiveSession();
            return;
        }

        if (CanAdvanceByInput(step) || CanAdvanceByTime(step))
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

        activeSession.CurrentStepIndex++;
        activeSession.AutoAdvanceTimer = 0f;

        DialogueStepData step = GetCurrentStep();
        if (step == null)
        {
            FinishActiveSession();
            return;
        }

        if (bubblePresenter == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} requires {nameof(DialogueBubblePresenter)} to show dialogue bubbles.", this);
            FinishActiveSession();
            return;
        }

        if (!bubblePresenter.ShowStep(step, activeSession.Speakers, activeSession.Context.Sequence.name))
        {
            FinishActiveSession();
            return;
        }

        DialogueStepChanged?.Invoke(activeSession.Context, step, activeSession.CurrentStepIndex);
        GlobalDialogueStepChanged?.Invoke(activeSession.Context, step, activeSession.CurrentStepIndex);
    }

    private void FinishActiveSession()
    {
        if (activeSession == null)
        {
            return;
        }

        DialoguePlaybackContext context = activeSession.Context;
        bubblePresenter?.HideActiveBubbles();
        activeSession = null;
        activeInput = null;

        DialogueFinished?.Invoke(context);
        GlobalDialogueFinished?.Invoke(context);
    }

    private bool CanAdvanceByInput(DialogueStepData step)
    {
        if (!step.advanceByInput || activeInput == null)
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

    private bool CanAdvanceByTime(DialogueStepData step)
    {
        if (!step.advanceByTime)
        {
            return false;
        }

        activeSession.AutoAdvanceTimer += Time.deltaTime;
        return activeSession.AutoAdvanceTimer >= Mathf.Max(0f, step.autoAdvanceTime);
    }

    private DialogueStepData GetCurrentStep()
    {
        if (activeSession == null)
        {
            return null;
        }

        DialogueSequenceData sequence = activeSession.Context.Sequence;
        int index = activeSession.CurrentStepIndex;
        if (sequence == null || sequence.steps == null || index < 0 || index >= sequence.steps.Length)
        {
            return null;
        }

        return sequence.steps[index];
    }

    private bool HasValidRequest(DialogueSequenceData sequence, DialogueSpeaker[] speakers, PlayerInput input)
    {
        if (sequence == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} received a null dialogue sequence.", this);
            return false;
        }

        if (sequence.steps == null || sequence.steps.Length == 0)
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
