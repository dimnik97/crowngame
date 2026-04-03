using System.Collections.Generic;
using UnityEngine;

public class PlayerRecruitShout : MonoBehaviour
{
    [Header("Recruit")]
    [SerializeField] private KeyCode recruitKey = KeyCode.F;
    [SerializeField] private float recruitRadius = 5f;

    [Header("Warrior Commands")]
    [SerializeField] private KeyCode warriorFollowKey = KeyCode.G;
    [SerializeField] private KeyCode warriorReleaseKey = KeyCode.H;
    [SerializeField] private float warriorCommandRadius = 8f;

    [Header("Timing")]
    [SerializeField] private float shoutCooldown = 0.5f;

    private PlayerTopDownController playerController;
    private float cooldownTimer;

    private void Awake()
    {
        playerController = GetComponent<PlayerTopDownController>();
    }

    private void Update()
    {
        if (playerController != null && !playerController.IsAlive)
            return;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(recruitKey))
            DoRecruitShout();
        else if (Input.GetKeyDown(warriorFollowKey))
            DoWarriorFollowShout();
        else if (Input.GetKeyDown(warriorReleaseKey))
            DoWarriorReleaseShout();
    }

    public void DoRecruitShout()
    {
        if (!TryUseShoutCooldown())
            return;

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

    public void DoWarriorFollowShout()
    {
        if (!TryUseShoutCooldown())
            return;

        if (NpcManager.Instance == null)
            return;

        int assignedCount = 0;
        IReadOnlyList<RecruitableNpc> npcs = NpcManager.Instance.RecruitedNpcs;

        for (int i = 0; i < npcs.Count; i++)
        {
            RecruitableNpc npc = npcs[i];
            if (npc == null || !npc.IsAlive || !npc.IsWarrior)
                continue;

            if (GetFlatDistanceTo(npc.transform.position) > warriorCommandRadius)
                continue;

            if (npc.TryAssignFollowPlayer(transform))
                assignedCount++;
        }

        Debug.Log($"Боевой клич: за игроком следуют {assignedCount} воинов");
    }

    public void DoWarriorReleaseShout()
    {
        if (!TryUseShoutCooldown())
            return;

        if (NpcManager.Instance == null)
            return;

        int releasedCount = 0;
        IReadOnlyList<RecruitableNpc> npcs = NpcManager.Instance.RecruitedNpcs;

        for (int i = 0; i < npcs.Count; i++)
        {
            RecruitableNpc npc = npcs[i];
            if (npc == null || !npc.IsAlive || !npc.IsWarrior || !npc.IsFollowingPlayer)
                continue;

            if (npc.ReleasePlayerFollowCommand())
                releasedCount++;
        }

        Debug.Log($"Боевой клич: освобождено {releasedCount} воинов");
    }

    private bool TryUseShoutCooldown()
    {
        if (cooldownTimer > 0f)
            return false;

        cooldownTimer = shoutCooldown;
        return true;
    }

    private float GetFlatDistanceTo(Vector3 worldPoint)
    {
        Vector3 delta = worldPoint - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, recruitRadius);

        Gizmos.color = new Color(1f, 0.7f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, warriorCommandRadius);
    }
}
