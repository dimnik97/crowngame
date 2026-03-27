using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerTopDownController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(x, 0f, z).normalized;
        Vector3 move = input * moveSpeed;

        // Простая гравитация для CharacterController
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = new Vector3(move.x, velocity.y, move.z);
        controller.Move(finalMove * Time.deltaTime);

        // Поворот персонажа по направлению движения
        if (input.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(input, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }
}