using System.Collections.Generic;
using UnityEngine;

public class DialogueBubblePresenter : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private SpeechBubbleView speechBubblePrefab;

    [Header("Root")]
    [SerializeField] private RectTransform bubbleRoot;

    private readonly List<SpeechBubbleView> activeBubbles = new();
    private readonly Stack<SpeechBubbleView> pooledBubbles = new();

    public bool AreActiveBubblesComplete
    {
        get
        {
            for (int i = 0; i < activeBubbles.Count; i++)
            {
                SpeechBubbleView bubble = activeBubbles[i];
                if (bubble != null && !bubble.IsTextFullyVisible)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private void Awake()
    {
        if (bubbleRoot == null)
        {
            bubbleRoot = transform as RectTransform;
        }
    }

    public bool ShowStep(DialogueStepData step, DialogueSpeaker[] speakers, string sequenceName)
    {
        HideActiveBubbles();

        if (step == null || step.lines == null || step.lines.Length == 0)
        {
            Debug.LogError($"{nameof(DialogueBubblePresenter)} received an empty dialogue step in '{sequenceName}'.", this);
            return false;
        }

        for (int i = 0; i < step.lines.Length; i++)
        {
            DialogueLineData line = step.lines[i];
            if (line == null)
            {
                Debug.LogError($"{nameof(DialogueBubblePresenter)} received a null line in '{sequenceName}'.", this);
                HideActiveBubbles();
                return false;
            }

            DialogueSpeaker speaker = ResolveSpeaker(line.speakerTag, speakers);
            if (speaker == null)
            {
                Debug.LogError($"{nameof(DialogueBubblePresenter)} could not find speakerTag '{line.speakerTag}' in dialogue '{sequenceName}'.", this);
                HideActiveBubbles();
                return false;
            }

            SpeechBubbleView bubble = GetBubble();
            if (bubble == null)
            {
                HideActiveBubbles();
                return false;
            }

            bubble.Show(speaker.SpeechBubbleAnchor, line);
            activeBubbles.Add(bubble);
        }

        return true;
    }

    public void CompleteActiveBubbles()
    {
        for (int i = 0; i < activeBubbles.Count; i++)
        {
            SpeechBubbleView bubble = activeBubbles[i];
            if (bubble != null)
            {
                bubble.CompleteText();
            }
        }
    }

    public void HideActiveBubbles()
    {
        for (int i = 0; i < activeBubbles.Count; i++)
        {
            SpeechBubbleView bubble = activeBubbles[i];
            if (bubble == null)
            {
                continue;
            }

            bubble.Hide();
            pooledBubbles.Push(bubble);
        }

        activeBubbles.Clear();
    }

    private SpeechBubbleView GetBubble()
    {
        if (pooledBubbles.Count > 0)
        {
            return pooledBubbles.Pop();
        }

        if (speechBubblePrefab == null)
        {
            Debug.LogError($"{nameof(DialogueBubblePresenter)} on {name} requires a speech bubble prefab.", this);
            return null;
        }

        Transform parent = bubbleRoot != null ? bubbleRoot : transform;
        return Instantiate(speechBubblePrefab, parent);
    }

    private static DialogueSpeaker ResolveSpeaker(string speakerTag, DialogueSpeaker[] speakers)
    {
        if (string.IsNullOrWhiteSpace(speakerTag) || speakers == null)
        {
            return null;
        }

        for (int i = 0; i < speakers.Length; i++)
        {
            DialogueSpeaker speaker = speakers[i];
            if (speaker != null && speaker.HasTag(speakerTag))
            {
                return speaker;
            }
        }

        return null;
    }
}
