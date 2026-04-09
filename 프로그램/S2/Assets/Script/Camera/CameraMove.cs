using UnityEngine;

public class CameraMove : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new(0f, 0f, -6f);
    [SerializeField] private float smoothTime = 0.08f;
    [SerializeField] private float snapDistance = 0.01f;

    private Vector3 currentVelocity;

    private void Awake()
    {
        if (target == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
            {
                target = playerObject.transform;
            }
        }

        if (target == null)
        {
            Debug.LogError($"{nameof(CameraMove)} could not find a target with the Player tag.", this);
            enabled = false;
            return;
        }

        transform.position = target.position + offset;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + offset;
        float distance = Vector3.Distance(transform.position, targetPosition);

        if (distance <= snapDistance)
        {
            transform.position = targetPosition;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref currentVelocity,
            smoothTime);
    }
}
