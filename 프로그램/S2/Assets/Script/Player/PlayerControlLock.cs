using System.Collections.Generic;
using UnityEngine;

public enum PlayerControlLockReason
{
    Dialogue,
}

public enum PlayerControlLockType
{
    Movement,
    MeleeAttack,
    WeaponThrow,
    Dodge,
}

public class PlayerControlLock : MonoBehaviour
{
    private readonly HashSet<PlayerControlLockReason> movementLocks = new();
    private readonly HashSet<PlayerControlLockReason> meleeAttackLocks = new();
    private readonly HashSet<PlayerControlLockReason> weaponThrowLocks = new();
    private readonly HashSet<PlayerControlLockReason> dodgeLocks = new();

    public bool IsMovementLocked => movementLocks.Count > 0;
    public bool IsMeleeAttackLocked => meleeAttackLocks.Count > 0;
    public bool IsWeaponThrowLocked => weaponThrowLocks.Count > 0;
    public bool IsDodgeLocked => dodgeLocks.Count > 0;

    private void OnEnable()
    {
        DialogueManager.GlobalDialogueStarted += HandleDialogueStarted;
        DialogueManager.GlobalDialogueFinished += HandleDialogueFinished;
    }

    private void OnDisable()
    {
        DialogueManager.GlobalDialogueStarted -= HandleDialogueStarted;
        DialogueManager.GlobalDialogueFinished -= HandleDialogueFinished;

        UnlockAll(PlayerControlLockReason.Dialogue);
    }

    public void Lock(PlayerControlLockType lockType, PlayerControlLockReason reason)
    {
        GetLocks(lockType).Add(reason);
    }

    public void Unlock(PlayerControlLockType lockType, PlayerControlLockReason reason)
    {
        GetLocks(lockType).Remove(reason);
    }

    private void HandleDialogueStarted(DialoguePlaybackContext context)
    {
        if (context.CanPlayerControlDuringDialogue)
        {
            return;
        }

        if (!context.CanPlayerMoveDuringDialogue)
        {
            Lock(PlayerControlLockType.Movement, PlayerControlLockReason.Dialogue);
        }

        Lock(PlayerControlLockType.MeleeAttack, PlayerControlLockReason.Dialogue);
        Lock(PlayerControlLockType.WeaponThrow, PlayerControlLockReason.Dialogue);
        Lock(PlayerControlLockType.Dodge, PlayerControlLockReason.Dialogue);
    }

    private void HandleDialogueFinished(DialoguePlaybackContext context)
    {
        UnlockAll(PlayerControlLockReason.Dialogue);
    }

    private void UnlockAll(PlayerControlLockReason reason)
    {
        movementLocks.Remove(reason);
        meleeAttackLocks.Remove(reason);
        weaponThrowLocks.Remove(reason);
        dodgeLocks.Remove(reason);
    }

    private HashSet<PlayerControlLockReason> GetLocks(PlayerControlLockType lockType)
    {
        return lockType switch
        {
            PlayerControlLockType.Movement => movementLocks,
            PlayerControlLockType.MeleeAttack => meleeAttackLocks,
            PlayerControlLockType.WeaponThrow => weaponThrowLocks,
            PlayerControlLockType.Dodge => dodgeLocks,
            _ => movementLocks,
        };
    }
}
