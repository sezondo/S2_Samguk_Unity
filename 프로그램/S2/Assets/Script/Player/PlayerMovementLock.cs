using System.Collections.Generic;
using UnityEngine;

public enum PlayerMovementLockReason
{
    Dialogue,
}

public class PlayerMovementLock : MonoBehaviour
{
    private readonly HashSet<PlayerMovementLockReason> lockReasons = new();

    public bool IsLocked => lockReasons.Count > 0;

    private void OnEnable()
    {
        DialogueManager.GlobalDialogueStarted += HandleDialogueStarted;
        DialogueManager.GlobalDialogueFinished += HandleDialogueFinished;
    }

    private void OnDisable()
    {
        DialogueManager.GlobalDialogueStarted -= HandleDialogueStarted;
        DialogueManager.GlobalDialogueFinished -= HandleDialogueFinished;

        Unlock(PlayerMovementLockReason.Dialogue);
    }

    public void Lock(PlayerMovementLockReason reason)
    {
        lockReasons.Add(reason);
    }

    public void Unlock(PlayerMovementLockReason reason)
    {
        lockReasons.Remove(reason);
    }

    private void HandleDialogueStarted(DialoguePlaybackContext context)
    {
        if (!context.CanPlayerMoveDuringDialogue)
        {
            Lock(PlayerMovementLockReason.Dialogue);
        }
    }

    private void HandleDialogueFinished(DialoguePlaybackContext context)
    {
        Unlock(PlayerMovementLockReason.Dialogue);
    }
}
