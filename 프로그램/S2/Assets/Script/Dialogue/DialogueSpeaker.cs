using UnityEngine;

public class DialogueSpeaker : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string speakerTag;

    [Header("Anchor")]
    [SerializeField] private Transform speechBubbleAnchor;

    public string SpeakerTag => speakerTag;
    public Transform SpeechBubbleAnchor => speechBubbleAnchor != null ? speechBubbleAnchor : transform;

    private void Awake()
    {
        if (speechBubbleAnchor == null)
        {
            speechBubbleAnchor = transform;
        }
    }

    public bool HasTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(speakerTag) && speakerTag == tag;
    }
}
