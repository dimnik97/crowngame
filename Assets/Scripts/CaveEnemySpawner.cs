using System.Collections.Generic;
using UnityEngine;

public class CaveEnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnRadius = 0.75f;

    [Header("Timing")]
    [SerializeField] private Vector2 initialDelayRange = new Vector2(15f, 20f);
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private int enemiesPerSpawn = 1;
    [SerializeField] private int maxAliveEnemies = 8;
    [SerializeField] private bool autoStart = true;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private readonly List<GameObject> aliveEnemies = new List<GameObject>();

    private float initialDelayTimer;
    private float spawnTimer;
    private bool started;

    private void Start()
    {
        ResetSpawner();
    }

    private void Update()
    {
        CleanupDeadEnemies();

        if (!autoStart && !started)
            return;

        if (!started)
        {
            initialDelayTimer -= Time.deltaTime;
            if (initialDelayTimer > 0f)
                return;

            started = true;
            spawnTimer = 0f;
        }

        if (enemyPrefab == null)
            return;

        if (aliveEnemies.Count >= maxAliveEnemies)
            return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f)
            return;

        SpawnEnemies();
        spawnTimer = Mathf.Max(0.1f, spawnInterval);
    }

    [ContextMenu("Start Enemy Waves")]
    public void BeginSpawning()
    {
        started = true;
        spawnTimer = 0f;
    }

    [ContextMenu("Reset Enemy Spawner")]
    public void ResetSpawner()
    {
        started = !autoStart;
        spawnTimer = Mathf.Max(0.1f, spawnInterval);
        initialDelayTimer = Random.Range(initialDelayRange.x, initialDelayRange.y);
    }

    private void SpawnEnemies()
    {
        int freeSlots = Mathf.Max(0, maxAliveEnemies - aliveEnemies.Count);
        int spawnCount = Mathf.Min(enemiesPerSpawn, freeSlots);

        for (int i = 0; i < spawnCount; i++)
            SpawnSingleEnemy(i);
    }

    private void SpawnSingleEnemy(int index)
    {
        Transform spawnOrigin = GetSpawnPoint(index);
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = spawnOrigin.position + new Vector3(offset.x, 0f, offset.y);

        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, spawnOrigin.rotation);

        if (enemy.GetComponent<HostileTarget>() == null)
            enemy.AddComponent<HostileTarget>();

        if (enemy.GetComponent<EnemyRaider>() == null)
            enemy.AddComponent<EnemyRaider>();

        if (enemy.GetComponentInChildren<Collider>() == null)
            enemy.AddComponent<SphereCollider>();

        aliveEnemies.Add(enemy);
    }

    private Transform GetSpawnPoint(int index)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[index % spawnPoints.Length];

        return transform;
    }

    private void CleanupDeadEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            if (aliveEnemies[i] == null)
                aliveEnemies.RemoveAt(i);
        }
    }

    private void OnValidate()
    {
        initialDelayRange.x = Mathf.Max(0f, initialDelayRange.x);
        initialDelayRange.y = Mathf.Max(initialDelayRange.x, initialDelayRange.y);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        enemiesPerSpawn = Mathf.Max(1, enemiesPerSpawn);
        maxAliveEnemies = Mathf.Max(1, maxAliveEnemies);
        spawnRadius = Mathf.Max(0f, spawnRadius);
    }
}
