using UnityEngine;

public class CameraMove : MonoBehaviour
{
    private Transform player;
    private Vector3 offset;
    [SerializeField]
    private float followSpeed = 5f;
    [SerializeField]
    private float followThreshold = 0.5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player = GameObject.FindWithTag("Player").transform;
        transform.position = Vector3.zero;

        Vector3 InitialPositionValue = new Vector3(0,0,-6f);
        offset = InitialPositionValue + player.position;
        
    }

    // Update is called once per frame
    void Update()
    {
        

    }

    
    void FixedUpdate()
    {
        Vector3 targetPos = player.position + offset;
        float distance = Vector3.Distance(transform.position, targetPos);
        
        if (distance > followThreshold)
        {
            transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            followSpeed * Time.deltaTime
            );
        }
        
        Vector3 moveDir = player.forward;
        moveDir.y = 0;
        moveDir.Normalize();

        

        

    }

}
