using UnityEngine;

public class DialogueSpeaker : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string speakerTag;

    [Header("View")]
    [SerializeField] private SpeechBubbleView speechBubbleView;
    [SerializeField] private Transform speechBubbleAnchor;

    public string SpeakerTag => speakerTag;

    private void Awake()
    {
        if (speechBubbleView == null)
        {
            speechBubbleView = GetComponentInChildren<SpeechBubbleView>(true);
        }

        if (speechBubbleAnchor == null)
        {
            speechBubbleAnchor = transform;
        }

        HideLine();
    }

    public bool HasTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(speakerTag) && speakerTag == tag;
    }

    public void ShowLine(string text)
    {
        if (speechBubbleView == null)
        {
            Debug.LogError($"{nameof(DialogueSpeaker)} on {name} requires {nameof(SpeechBubbleView)}.", this);
            return;
        }

        speechBubbleView.Show(speechBubbleAnchor, text);
    }

    public void HideLine()
    {
        speechBubbleView?.Hide();
    }
}
