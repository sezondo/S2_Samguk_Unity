using System;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [SerializeField] private DialogueBubblePresenter bubblePresenter;

    private DialogueSequenceData activeSequence;
    private DialogueSpeaker[] activeSpeakers;
    private int currentStepIndex = -1;
    private float autoAdvanceTimer;

    public bool IsPlaying => activeSequence != null;

    public event Action<DialogueSequenceData> DialogueStarted;
    public event Action<DialogueSequenceData, DialogueStepData, int> DialogueStepChanged;
    public event Action<DialogueSequenceData> DialogueFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"{nameof(DialogueManager)} already exists. Only one dialogue manager is allowed.", this);
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
        if (!IsPlaying)
        {
            return;
        }

        DialogueStepData step = GetCurrentStep();
        if (step == null)
        {
            FinishActiveSequence();
            return;
        }

        if (bubblePresenter != null && !bubblePresenter.AreActiveBubblesComplete)
        {
            return;
        }

        if (CanAdvanceByTime(step))
        {
            Advance();
        }
    }

    public bool TryPlay(DialogueSequenceData sequence, DialogueSpeaker[] speakers)
    {
        if (IsPlaying)
        {
            Debug.LogWarning($"{nameof(DialogueManager)} rejected dialogue '{GetSequenceName(sequence)}' because another dialogue is already playing.", this);
            return false;
        }

        if (!HasValidRequest(sequence, speakers))
        {
            return false;
        }

        activeSequence = sequence;
        activeSpeakers = speakers;
        currentStepIndex = -1;
        autoAdvanceTimer = 0f;

        DialogueStarted?.Invoke(activeSequence);
        Advance();
        return true;
    }

    public void Advance()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (bubblePresenter != null && !bubblePresenter.AreActiveBubblesComplete)
        {
            bubblePresenter.CompleteActiveBubbles();
            autoAdvanceTimer = 0f;
            return;
        }

        currentStepIndex++;
        autoAdvanceTimer = 0f;

        DialogueStepData step = GetCurrentStep();
        if (step == null)
        {
            FinishActiveSequence();
            return;
        }

        if (bubblePresenter == null)
        {
            Debug.LogError($"{nameof(DialogueManager)} requires {nameof(DialogueBubblePresenter)} to show dialogue bubbles.", this);
            FinishActiveSequence();
            return;
        }

        if (!bubblePresenter.ShowStep(step, activeSpeakers, activeSequence.name))
        {
            FinishActiveSequence();
            return;
        }

        DialogueStepChanged?.Invoke(activeSequence, step, currentStepIndex);
    }

    public void Stop()
    {
        FinishActiveSequence();
    }

    private void FinishActiveSequence()
    {
        if (!IsPlaying)
        {
            return;
        }

        DialogueSequenceData finishedSequence = activeSequence;
        bubblePresenter?.HideActiveBubbles();
        activeSequence = null;
        activeSpeakers = null;
        currentStepIndex = -1;
        autoAdvanceTimer = 0f;

        DialogueFinished?.Invoke(finishedSequence);
    }

    private bool CanAdvanceByTime(DialogueStepData step)
    {
        if (!step.advanceByTime)
        {
            return false;
        }

        autoAdvanceTimer += Time.deltaTime;
        return autoAdvanceTimer >= Mathf.Max(0f, step.autoAdvanceTime);
    }

    private DialogueStepData GetCurrentStep()
    {
        if (activeSequence == null || activeSequence.steps == null || currentStepIndex < 0 || currentStepIndex >= activeSequence.steps.Length)
        {
            return null;
        }

        return activeSequence.steps[currentStepIndex];
    }

    private bool HasValidRequest(DialogueSequenceData sequence, DialogueSpeaker[] speakers)
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

        return true;
    }

    private static string GetSequenceName(DialogueSequenceData sequence)
    {
        return sequence != null ? sequence.name : "null";
    }
}
