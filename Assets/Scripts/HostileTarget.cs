using UnityEngine;

public class HostileTarget : MonoBehaviour
{
    [SerializeField] private string displayName = "Enemy";
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2.1f, 0f);
    [SerializeField] private Color healthBarFillColor = new Color(0.92f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color healthBarBackgroundColor = new Color(0.18f, 0.05f, 0.05f, 0.92f);
    [SerializeField] private Color healthBarBorderColor = Color.black;

    private Health health;

    public string DisplayName => displayName;
    public Health Health => health;
    public bool IsAlive => health != null && health.IsAlive;

    private void Awake()
    {
        EnsureHealthSetup();
    }

    public void TakeDamage(int damage)
    {
        EnsureHealthSetup();

        if (!health.TakeDamage(damage))
            return;

        if (!health.IsAlive)
            Debug.Log($"{displayName} defeated");
    }

    private void EnsureHealthSetup()
    {
        if (health != null)
            return;

        health = GetComponent<Health>();
        if (health == null)
            health = gameObject.AddComponent<Health>();

        health.Configure(maxHealth, destroyOnDeath, true);

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<WorldHealthBar>();

        healthBar.Configure(
            health,
            healthBarOffset,
            healthBarFillColor,
            healthBarBackgroundColor,
            healthBarBorderColor
        );
    }
}
