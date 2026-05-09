using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private PlayerInput input;
    private PlayerFSMManager fsm;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        rb = context.Body;
        input = context.Input;
        fsm = context.Fsm;

        if (rb == null || input == null || fsm == null)
        {
            Debug.LogError($"{nameof(PlayerMovement)} on {name} is missing a required component.", this);
            enabled = false;
        }

        // Render frame between physics steps is smoothed through interpolation.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void FixedUpdate()
    {
        if (!fsm.CanMoveInMainState())
        {
            return;
        }

        Vector2 move = input.Move;

        Vector2 nextPosition = rb.position + move * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }
}
