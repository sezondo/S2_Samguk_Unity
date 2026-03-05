using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 move;
    private PlayerInput input;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        input = GetComponent<PlayerInput>();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        if (input == null) return;

        move = input.move;

        Vector2 nextPosition = rb.position + move * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(nextPosition);
    }
}