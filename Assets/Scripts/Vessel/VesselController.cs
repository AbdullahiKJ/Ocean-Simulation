using UnityEngine;
using UnityEngine.InputSystem;

public class VesselController : MonoBehaviour
{

    [SerializeField] float acceleration = 10f;
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float turnSpeed = 60f;

    Rigidbody rb;

    float movementX;
    float movementY;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // Forward/backward movement
        Vector3 forwardForce = transform.forward * movementY * acceleration;
        rb.AddForce(forwardForce, ForceMode.Acceleration);

        // Limit horizontal speed
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (horizontalVelocity.magnitude > maxSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * maxSpeed;

            rb.linearVelocity = new Vector3(
                horizontalVelocity.x,
                rb.linearVelocity.y,
                horizontalVelocity.z
            );
        }

        // Steering
        if (Mathf.Abs(movementY) > 0.01f)
        {
            float turn = movementX * turnSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
        }
    }

    void OnMove(InputValue inputValue)
    {
        Vector2 movementVector = inputValue.Get<Vector2>();

        movementX = movementVector.x;
        movementY = movementVector.y;
    }
}
