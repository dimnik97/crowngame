using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public class ResourceInventory : MonoBehaviour
{
    public static ResourceInventory Instance { get; private set; }

    [Header("Debug UI")]
    [SerializeField] private bool showDebugUI = true;

    private readonly Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            resources[type] = 0;
    }

    public void Add(ResourceType type, int amount)
    {
        if (amount <= 0)
            return;

        if (!resources.ContainsKey(type))
            resources[type] = 0;

        resources[type] += amount;

        Debug.Log($"+{amount} {GetResourceDisplayName(type)}. Теперь: {resources[type]}");
    }

    public int GetAmount(ResourceType type)
    {
        if (!resources.ContainsKey(type))
            return 0;

        return resources[type];
    }

    public bool HasEnough(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
            return true;

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null)
                continue;

            if (GetAmount(cost.resourceType) < cost.amount)
                return false;
        }

        return true;
    }

    public bool TrySpend(ResourceCost[] costs)
    {
        if (!HasEnough(costs))
            return false;

        if (costs == null || costs.Length == 0)
            return true;

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null)
                continue;

            resources[cost.resourceType] -= cost.amount;
        }

        Debug.Log("Ресурсы списаны: " + GetCostText(costs));
        return true;
    }

    public string GetCostText(ResourceCost[] costs)
    {
        if (costs == null || costs.Length == 0)
            return "Бесплатно";

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < costs.Length; i++)
        {
            ResourceCost cost = costs[i];
            if (cost == null)
                continue;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(GetResourceDisplayName(cost.resourceType));
            sb.Append(": ");
            sb.Append(cost.amount);
        }

        return sb.ToString();
    }

    private string GetResourceDisplayName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "Дерево";
            case ResourceType.Stone: return "Камень";
            case ResourceType.Iron: return "Железо";
            default: return type.ToString();
        }
    }

    private void OnGUI()
    {
        if (!showDebugUI)
            return;

        Rect boxRect = new Rect(Screen.width - 220, 10, 200, 110);

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.Box(boxRect, "");

        GUI.color = Color.white;
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 10, 180, 20), "Ресурсы");

        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 35, 180, 20), $"Дерево: {GetAmount(ResourceType.Wood)}");
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 55, 180, 20), $"Камень: {GetAmount(ResourceType.Stone)}");
        GUI.Label(new Rect(boxRect.x + 10, boxRect.y + 75, 180, 20), $"Железо: {GetAmount(ResourceType.Iron)}");
    }
}