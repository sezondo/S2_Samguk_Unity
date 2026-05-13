using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerFSMManager))]
[RequireComponent(typeof(PlayerLoadout))]
[RequireComponent(typeof(PlayerContext))]
public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerInput input;
    private PlayerFSMManager fsm;
    private PlayerLoadout loadout;

    private void Awake()
    {
        PlayerContext context = GetComponent<PlayerContext>();
        context.ResolveReferences();

        rb = context.Body;
        input = context.Input;
        fsm = context.Fsm;
        loadout = context.Loadout;

        if (rb == null || input == null || fsm == null || loadout == null)
        {
            Debug.LogError($"{nameof(PlayerMovement)} on {name} is missing a required component.", this);
            enabled = false;
            return;
        }

        if (!HasValidData())
        {
            enabled = false;
            return;
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

        Vector2 nextPosition = rb.position + move * ResolveMoveSpeed() * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }

    private float ResolveMoveSpeed()
    {
        return loadout.PlayerData.moveSpeed;
    }

    private bool HasValidData()
    {
        PlayerData playerData = loadout.PlayerData;
        if (playerData == null)
        {
            Debug.LogError($"{nameof(PlayerMovement)} on {name} requires {nameof(PlayerData)}.", this);
            return false;
        }

        if (playerData.moveSpeed <= 0f)
        {
            Debug.LogError($"{nameof(PlayerMovement)} on {name} requires moveSpeed greater than 0 in {playerData.name}.", this);
            return false;
        }

        return true;
    }
}
