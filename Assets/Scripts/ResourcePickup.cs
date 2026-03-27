using UnityEngine;

public class ResourcePickup : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int amount = 1;

    private bool isCollected;

    public ResourceType ResourceType => resourceType;
    public int Amount => amount;

    public bool TryCollect()
    {
        if (isCollected)
            return false;

        if (ResourceInventory.Instance == null)
        {
            Debug.LogWarning("На сцене нет ResourceInventory");
            return false;
        }

        isCollected = true;
        ResourceInventory.Instance.Add(resourceType, amount);
        Destroy(gameObject);

        return true;
    }
}