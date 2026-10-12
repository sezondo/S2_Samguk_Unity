using System.Collections.Generic;
using UnityEngine;

public class DialogueBubblePresenter : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private SpeechBubbleView speechBubblePrefab;

    [Header("Root")]
    [SerializeField] private RectTransform bubbleRoot;

    // 말풍선이 사용할 카메라와 Canvas를 명시적으로 전달한다.
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Canvas rootCanvas;

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
        if (bubbleRoot == null || worldCamera == null || rootCanvas == null || speechBubblePrefab == null)
        { Debug.LogError("말풍선 Presenter에 루트·Canvas·카메라·말풍선 참조가 필요합니다.", this); enabled = false; }
    }

    public bool ShowStep(DialogueStepData step, DialogueSpeaker[] speakers, string sequenceName)
    {
        HideActiveBubbles();

        if (step == null || step.lines == null || step.lines.Length == 0)
        {
            Debug.LogError($"{nameof(DialogueBubblePresenter)}: '{sequenceName}' 대사에서 비어 있는 스텝을 받았습니다.", this);
            return false;
        }

        for (int i = 0; i < step.lines.Length; i++)
        {
            DialogueLineData line = step.lines[i];
            if (line == null)
            {
                Debug.LogError($"{nameof(DialogueBubblePresenter)}: '{sequenceName}' 대사에 비어 있는 대사 줄이 있습니다.", this);
                HideActiveBubbles();
                return false;
            }

            DialogueSpeaker speaker = ResolveSpeaker(line.speakerTag, speakers);
            if (speaker == null)
            {
                Debug.LogError($"{nameof(DialogueBubblePresenter)}: '{sequenceName}' 대사에서 speakerTag '{line.speakerTag}'에 해당하는 발화자를 찾지 못했습니다.", this);
                HideActiveBubbles();
                return false;
            }

            SpeechBubbleView bubble = GetBubble();
            if (bubble == null)
            {
                HideActiveBubbles();
                return false;
            }

            bubble.Initialize(worldCamera, rootCanvas);
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
            Debug.LogError($"{nameof(DialogueBubblePresenter)}: {name} 오브젝트에 말풍선 프리팹이 필요합니다.", this);
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
