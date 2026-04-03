using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private bool destroyOnDeath = true;

    private int currentHealth;
    private bool initialized;

    public int MaxHealth => Mathf.Max(1, maxHealth);
    public int CurrentHealth => currentHealth;
    public bool DestroyOnDeath => destroyOnDeath;
    public bool IsAlive => currentHealth > 0;

    public event Action<Health> Changed;
    public event Action<Health> Died;

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);

        if (!Application.isPlaying)
            return;

        currentHealth = Mathf.Clamp(currentHealth, 0, MaxHealth);
    }

    public void Configure(int newMaxHealth, bool shouldDestroyOnDeath, bool healToFull)
    {
        maxHealth = Mathf.Max(1, newMaxHealth);
        destroyOnDeath = shouldDestroyOnDeath;

        if (!initialized || healToFull || currentHealth <= 0)
            currentHealth = MaxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 1, MaxHealth);

        initialized = true;
        Changed?.Invoke(this);
    }

    public bool TakeDamage(int damage)
    {
        InitializeIfNeeded();

        if (!IsAlive || damage <= 0)
            return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Changed?.Invoke(this);

        if (currentHealth > 0)
            return true;

        Died?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject);

        return true;
    }

    public void HealFull()
    {
        InitializeIfNeeded();
        currentHealth = MaxHealth;
        Changed?.Invoke(this);
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = MaxHealth;
        initialized = true;
    }
}
