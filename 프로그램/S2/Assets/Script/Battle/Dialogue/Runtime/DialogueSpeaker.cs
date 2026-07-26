using UnityEngine;

public class DialogueSpeaker : MonoBehaviour
{
    [SerializeField] private string speakerTag;
    [SerializeField] private Transform speechBubbleAnchor;

    public string SpeakerTag => speakerTag;
    public Transform SpeechBubbleAnchor => speechBubbleAnchor != null ? speechBubbleAnchor : transform;

    public bool HasTag(string targetTag)
    {
        return !string.IsNullOrWhiteSpace(speakerTag) && speakerTag == targetTag;
    }
}
