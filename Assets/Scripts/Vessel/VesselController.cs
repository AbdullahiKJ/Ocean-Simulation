using UnityEngine;
using UnityEngine.InputSystem;

public class VesselController : MonoBehaviour
{

    [SerializeField] float acceleration = 10f;
    [SerializeField] float turnSpeed = 60f;
    [SerializeField] UIManager uiManager;

    Rigidbody rb;

    float movementX;
    float movementY;
    private Vector3 propulsionForce;
    public Vector3 PropulsionForce => propulsionForce;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (uiManager != null)
        {
            // Ignore input if the menu is open
            if (uiManager.IsMenuOpen)
                return;
        }

        // Calculate the propulsion force from player input
        propulsionForce = transform.forward * movementY * acceleration;
    }

    void FixedUpdate()
    {
        // Steering
        if (Mathf.Abs(movementY) > 0.01f)
        {
            float turn = movementX * turnSpeed * Time.fixedDeltaTime;

            rb.MoveRotation(
                rb.rotation * Quaternion.Euler(0f, turn, 0f)
            );
        }
    }

    void OnMove(InputValue inputValue)
    {
        Vector2 movementVector = inputValue.Get<Vector2>();

        movementX = movementVector.x;
        movementY = movementVector.y;
    }
}
