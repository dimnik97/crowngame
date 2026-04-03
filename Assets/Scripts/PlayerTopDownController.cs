using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerTopDownController : MonoBehaviour
{
    public static PlayerTopDownController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;

    [Header("Health")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2.4f, 0f);

    private CharacterController controller;
    private Health health;
    private Vector3 velocity;

    public bool IsAlive => health == null || health.IsAlive;
    public Health Health => health;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        controller = GetComponent<CharacterController>();
        EnsureHealthSetup();
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Died -= OnDied;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsAlive)
            return;

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

    private void EnsureHealthSetup()
    {
        health = GetComponent<Health>();
        if (health == null)
            health = gameObject.AddComponent<Health>();

        health.Configure(maxHealth, false, true);
        health.Died += OnDied;

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<WorldHealthBar>();

        healthBar.Configure(health, healthBarOffset);
    }

    private void OnDied(Health deadHealth)
    {
        velocity = Vector3.zero;
        Debug.Log("Игрок погиб");
    }
}
