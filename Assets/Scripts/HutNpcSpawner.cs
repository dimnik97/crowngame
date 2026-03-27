using UnityEngine;

public class HutNpcSpawner : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private int minNpcCount = 1;
    [SerializeField] private int maxNpcCount = 3;
    [SerializeField] private float minSpawnRadius = 1.5f;
    [SerializeField] private float maxSpawnRadius = 4f;

    private void Start()
    {
        SpawnNpcGroup();
    }

    [ContextMenu("Spawn NPC Group")]
    public void SpawnNpcGroup()
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning("NPC Prefab не назначен", this);
            return;
        }

        int count = Random.Range(minNpcCount, maxNpcCount + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPoint = GetSpawnPoint();
            GameObject npcObject = Instantiate(npcPrefab, spawnPoint, Quaternion.identity);

            RecruitableNpc npc = npcObject.GetComponent<RecruitableNpc>();
            if (npc == null)
                npc = npcObject.AddComponent<RecruitableNpc>();

            npc.Initialize(transform);
        }
    }

    private Vector3 GetSpawnPoint()
    {
        Vector2 dir = Random.insideUnitCircle;

        if (dir.sqrMagnitude < 0.001f)
            dir = Vector2.right;

        dir.Normalize();

        float distance = Random.Range(minSpawnRadius, maxSpawnRadius);

        return transform.position + new Vector3(dir.x, 0f, dir.y) * distance;
    }
}