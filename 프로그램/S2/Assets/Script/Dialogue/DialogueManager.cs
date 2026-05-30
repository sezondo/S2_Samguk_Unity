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
            Debug.LogError($"{nameof(DialogueManager)}: 이미 인스턴스가 있습니다. 대사 매니저는 씬에 하나만 둘 수 있습니다.", this);
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
            Debug.LogWarning($"{nameof(DialogueManager)}: 이미 다른 대사가 재생 중이라 '{GetSequenceName(sequence)}' 대사를 시작하지 못했습니다.", this);
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
            Debug.LogError($"{nameof(DialogueManager)}: 말풍선을 표시하려면 {nameof(DialogueBubblePresenter)}가 필요합니다.", this);
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
            Debug.LogError($"{nameof(DialogueManager)}: 비어 있는 대사 시퀀스를 받았습니다.", this);
            return false;
        }

        if (sequence.steps == null || sequence.steps.Length == 0)
        {
            Debug.LogError($"{nameof(DialogueManager)}: '{sequence.name}' 대사 시퀀스에 스텝이 없습니다.", this);
            return false;
        }

        if (speakers == null || speakers.Length == 0)
        {
            Debug.LogError($"{nameof(DialogueManager)}: '{sequence.name}' 대사에 사용할 발화자 목록이 없습니다.", this);
            return false;
        }

        return true;
    }

    private static string GetSequenceName(DialogueSequenceData sequence)
    {
        return sequence != null ? sequence.name : "null";
    }
}
