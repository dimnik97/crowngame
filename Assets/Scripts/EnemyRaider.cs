using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HostileTarget))]
public class EnemyRaider : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.8f;
    [SerializeField] private float objectiveReachDistance = 0.45f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Combat")]
    [SerializeField] private float detectionRadius = 3.2f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float loseTargetDistanceMultiplier = 1.8f;
    [SerializeField] private LayerMask targetMask = ~0;

    private Health health;
    private Health currentCombatTarget;
    private float attackTimer;

    private void Start()
    {
        health = GetComponent<Health>();
    }

    private void Update()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health != null && !health.IsAlive)
            return;

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        RefreshCombatTarget();

        if (currentCombatTarget != null)
        {
            UpdateCombat();
            return;
        }

        if (TryGetObjectivePosition(out Vector3 objectivePosition))
            MoveTowards(objectivePosition, objectiveReachDistance);
    }

    private void RefreshCombatTarget()
    {
        if (IsValidCombatTarget(currentCombatTarget))
        {
            float keepDistance = detectionRadius * loseTargetDistanceMultiplier;
            if (GetFlatDistanceTo(currentCombatTarget.transform.position) <= keepDistance)
                return;
        }

        currentCombatTarget = null;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            detectionRadius,
            targetMask,
            QueryTriggerInteraction.Collide
        );

        Health bestTarget = null;
        float bestDistance = float.PositiveInfinity;
        HashSet<Health> uniqueTargets = new HashSet<Health>();

        for (int i = 0; i < hits.Length; i++)
        {
            Health candidate = hits[i].GetComponentInParent<Health>();
            if (!IsValidCombatTarget(candidate) || !uniqueTargets.Add(candidate))
                continue;

            float sqrDistance = GetFlatSqrDistanceTo(candidate.transform.position);
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                bestTarget = candidate;
            }
        }

        currentCombatTarget = bestTarget;
    }

    private void UpdateCombat()
    {
        if (!IsValidCombatTarget(currentCombatTarget))
        {
            currentCombatTarget = null;
            return;
        }

        Vector3 targetPosition = currentCombatTarget.transform.position;
        float distance = GetFlatDistanceTo(targetPosition);

        if (distance > attackRange)
        {
            MoveTowards(targetPosition, attackRange * 0.85f);
            return;
        }

        RotateTowards(targetPosition - transform.position);

        if (attackTimer > 0f)
            return;

        currentCombatTarget.TakeDamage(attackDamage);
        attackTimer = attackCooldown;

        if (!IsValidCombatTarget(currentCombatTarget))
            currentCombatTarget = null;
    }

    private bool IsValidCombatTarget(Health candidate)
    {
        if (candidate == null || candidate == health || !candidate.IsAlive)
            return false;

        if (candidate.GetComponent<HostileTarget>() != null)
            return false;

        return candidate.GetComponent<PlayerTopDownController>() != null ||
               candidate.GetComponent<RecruitableNpc>() != null;
    }

    private bool TryGetObjectivePosition(out Vector3 objectivePosition)
    {
        objectivePosition = Vector3.zero;

        if (CampManager.Instance != null && CampManager.Instance.HasCamp())
        {
            objectivePosition = CampManager.Instance.CurrentCamp.WaitingCenter;
            return true;
        }

        if (PlayerTopDownController.Instance != null && PlayerTopDownController.Instance.IsAlive)
        {
            objectivePosition = PlayerTopDownController.Instance.transform.position;
            return true;
        }

        return false;
    }

    private void MoveTowards(Vector3 targetPosition, float stopDistance)
    {
        Vector3 flatTarget = new Vector3(targetPosition.x, transform.position.y, targetPosition.z);
        Vector3 toTarget = flatTarget - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= stopDistance)
            return;

        Vector3 direction = toTarget / distance;
        transform.position += direction * moveSpeed * Time.deltaTime;
        RotateTowards(direction);
    }

    private void RotateTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private float GetFlatDistanceTo(Vector3 worldPoint)
    {
        return Mathf.Sqrt(GetFlatSqrDistanceTo(worldPoint));
    }

    private float GetFlatSqrDistanceTo(Vector3 worldPoint)
    {
        Vector3 delta = worldPoint - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = new Color(1f, 0.6f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
