public readonly struct DialoguePlaybackContext
{
    public DialoguePlaybackContext(int sessionId, DialogueSequenceData sequence, DialogueEventTrigger source)
    {
        SessionId = sessionId;
        Sequence = sequence;
        Source = source;
    }

    public int SessionId { get; }
    public DialogueSequenceData Sequence { get; }
    public DialogueEventTrigger Source { get; }
    public bool CanPlayerMoveDuringDialogue => Sequence != null && Sequence.canPlayerMoveDuringDialogue;
    public bool CanPlayerControlDuringDialogue => Sequence != null && Sequence.canPlayerControlDuringDialogue;
}
