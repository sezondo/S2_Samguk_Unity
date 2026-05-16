using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DialogueEventTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueSequenceData dialogueSequence;
    [SerializeField] private DialogueSpeaker[] speakers;

    [Header("Start")]
    [SerializeField] private DialogueStartMode startMode = DialogueStartMode.Interact;
    [SerializeField] private bool allowRepeat;

    private PlayerInput playerInput;
    private bool playerInside;
    private bool played;

    private void Awake()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning($"{nameof(DialogueEventTrigger)} on {name} expects its Collider2D to be Trigger.", this);
        }
    }

    private void Update()
    {
        if (startMode != DialogueStartMode.Interact || !playerInside || playerInput == null)
        {
            return;
        }

        if (playerInput.InteractPressedThisFrame)
        {
            TryPlay();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerInput foundInput = other.GetComponentInParent<PlayerInput>();
        if (foundInput == null)
        {
            return;
        }

        playerInput = foundInput;
        playerInside = true;

        if (startMode == DialogueStartMode.Touch)
        {
            TryPlay();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerInput foundInput = other.GetComponentInParent<PlayerInput>();
        if (foundInput == null || foundInput != playerInput)
        {
            return;
        }

        playerInput = null;
        playerInside = false;
    }

    private bool TryPlay()
    {
        if (!allowRepeat && played)
        {
            return false;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogError($"{nameof(DialogueEventTrigger)} on {name} requires a scene {nameof(DialogueManager)}.", this);
            return false;
        }

        if (DialogueManager.Instance.TryPlay(dialogueSequence, speakers, this, playerInput))
        {
            played = true;
            return true;
        }

        return false;
    }
}
