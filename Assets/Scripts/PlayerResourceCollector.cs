using System.Collections.Generic;
using UnityEngine;

public class PlayerResourceCollector : MonoBehaviour
{
    [SerializeField] private float pickupRadius = 1.6f;
    [SerializeField] private LayerMask resourceMask;
    [SerializeField] private float scanInterval = 0.1f;

    private float scanTimer;

    private void Update()
    {
        scanTimer -= Time.deltaTime;

        if (scanTimer > 0f)
            return;

        scanTimer = scanInterval;
        ScanForResources();
    }

    private void ScanForResources()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            pickupRadius,
            resourceMask,
            QueryTriggerInteraction.Collide
        );

        HashSet<ResourcePickup> uniqueResources = new HashSet<ResourcePickup>();

        foreach (Collider hit in hits)
        {
            ResourcePickup pickup = hit.GetComponentInParent<ResourcePickup>();
            if (pickup == null)
                continue;

            if (!uniqueResources.Add(pickup))
                continue;

            pickup.TryCollect();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}