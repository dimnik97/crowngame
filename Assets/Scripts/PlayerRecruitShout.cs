using System.Collections.Generic;
using UnityEngine;

public class PlayerRecruitShout : MonoBehaviour
{
    [SerializeField] private KeyCode recruitKey = KeyCode.F;
    [SerializeField] private float recruitRadius = 5f;
    [SerializeField] private float shoutCooldown = 0.5f;

    private float cooldownTimer;

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(recruitKey))
            DoRecruitShout();
    }

    public void DoRecruitShout()
    {
        if (cooldownTimer > 0f)
            return;

        cooldownTimer = shoutCooldown;

        Collider[] hits = Physics.OverlapSphere(transform.position, recruitRadius);
        HashSet<RecruitableNpc> uniqueNpcs = new HashSet<RecruitableNpc>();

        int recruitedCount = 0;

        foreach (Collider hit in hits)
        {
            RecruitableNpc npc = hit.GetComponentInParent<RecruitableNpc>();
            if (npc == null)
                continue;

            if (!uniqueNpcs.Add(npc))
                continue;

            if (npc.TryRecruit())
                recruitedCount++;
        }

        Debug.Log($"Клич: завербовано {recruitedCount}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, recruitRadius);
    }
}