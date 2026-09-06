using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class FreeFlyController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float movementSpeed = 10f;
    [SerializeField] private float fastMovementSpeed = 25f;
    [SerializeField] private float acceleration = 10f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 89f;
    [SerializeField] CinemachineCamera freeCamera;

    private Vector2 movementInput;
    private Vector2 lookInput;

    private float verticalInput;
    private float currentSpeed;
    private float pitch;

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleMovement()
    {
        // WASD movement
        Vector3 direction =
            transform.forward * movementInput.y +
            transform.right * movementInput.x;

        // Vertical movement
        direction += Vector3.up * verticalInput;

        // Prevent diagonal movement from being faster
        direction = Vector3.ClampMagnitude(direction, 1f);

        // Shift = faster movement
        float targetSpeed = Keyboard.current.leftShiftKey.isPressed
            ? fastMovementSpeed
            : movementSpeed;

        // Smooth acceleration
        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            acceleration * Time.deltaTime
        );

        transform.position += direction * currentSpeed * Time.deltaTime;
    }

    private void HandleLook()
    {
        float mouseX = lookInput.x * lookSensitivity;
        float mouseY = lookInput.y * lookSensitivity;

        // Horizontal rotation
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);

        freeCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnMove(InputValue inputValue)
    {
        movementInput = inputValue.Get<Vector2>();
    }

    private void OnLook(InputValue inputValue)
    {
        lookInput = inputValue.Get<Vector2>();
    }

    private void OnVertical(InputValue inputValue)
    {
        verticalInput = inputValue.Get<float>();
    }
}