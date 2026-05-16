public class DialoguePlaybackSession
{
    public DialoguePlaybackSession(DialoguePlaybackContext context, DialogueSpeaker[] speakers, int startedFrame)
    {
        Context = context;
        Speakers = speakers;
        StartedFrame = startedFrame;
        CurrentLineIndex = -1;
        AutoAdvanceTimer = 0f;
    }

    public DialoguePlaybackContext Context { get; }
    public DialogueSpeaker[] Speakers { get; }
    public int StartedFrame { get; }
    public int CurrentLineIndex { get; set; }
    public float AutoAdvanceTimer { get; set; }
    public DialogueSpeaker CurrentSpeaker { get; set; }
}
