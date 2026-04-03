using System.Collections.Generic;
using UnityEngine;

public class Workshop : MonoBehaviour
{
    [Header("Worker")]
    [SerializeField] private Transform workPoint;
    [SerializeField] private float assignCheckInterval = 1f;

    [Header("Sword Orders")]
    [SerializeField] private Transform swordPickupPoint;
    [SerializeField] private ResourceCost[] swordCost;
    [SerializeField] private float swordCraftDuration = 5f;

    [Header("Player Interaction")]
    [SerializeField] private KeyCode orderSwordKey = KeyCode.E;
    [SerializeField] private float interactionRadius = 2.5f;
    [SerializeField] private bool showInteractionUI = true;

    private static readonly List<Workshop> activeWorkshops = new List<Workshop>();

    private RecruitableNpc assignedWorker;
    private RecruitableNpc assignedWarriorCandidate;
    private Transform playerTransform;
    private float assignTimer;
    private float craftingTimer;
    private int queuedSwords;
    private int readySwords;

    public bool HasWorker => assignedWorker != null;
    public RecruitableNpc AssignedWorker => assignedWorker;
    public int QueuedSwords => queuedSwords;
    public int ReadySwords => readySwords;

    private void Awake()
    {
        EnsureDefaultSwordCost();
    }

    private void OnEnable()
    {
        if (!activeWorkshops.Contains(this))
            activeWorkshops.Add(this);
    }

    private void OnDisable()
    {
        activeWorkshops.Remove(this);
    }

    private void OnValidate()
    {
        EnsureDefaultSwordCost();
        interactionRadius = Mathf.Max(0.1f, interactionRadius);
        swordCraftDuration = Mathf.Max(0f, swordCraftDuration);
    }

    public Vector3 GetWorkPosition()
    {
        if (workPoint != null)
            return workPoint.position;

        return transform.position;
    }

    public Quaternion GetWorkRotation()
    {
        if (workPoint != null)
            return workPoint.rotation;

        return transform.rotation;
    }

    public Vector3 GetSwordPickupPosition()
    {
        if (swordPickupPoint != null)
            return swordPickupPoint.position;

        return GetWorkPosition();
    }

    public Quaternion GetSwordPickupRotation()
    {
        if (swordPickupPoint != null)
            return swordPickupPoint.rotation;

        return GetWorkRotation();
    }

    private void Update()
    {
        CleanupAssignments();
        HandlePlayerInteraction();

        if (assignedWorker == null)
        {
            assignTimer -= Time.deltaTime;
            if (assignTimer <= 0f)
            {
                assignTimer = assignCheckInterval;
                TryAssignWorker();
            }
        }

        UpdateSwordCrafting();
        TryAssignWarriorCandidate();
    }

    public bool TryGiveSwordTo(RecruitableNpc npc)
    {
        if (npc == null || readySwords <= 0)
            return false;

        if (assignedWarriorCandidate != null && assignedWarriorCandidate != npc)
            return false;

        readySwords--;

        if (assignedWarriorCandidate == npc)
            assignedWarriorCandidate = null;

        Debug.Log($"{npc.NpcName} взял меч в мастерской и готов стать воином");
        return true;
    }

    public void ClearWorker(RecruitableNpc npc)
    {
        if (assignedWorker == npc)
            assignedWorker = null;
    }

    public void ClearWarriorCandidate(RecruitableNpc npc)
    {
        if (assignedWarriorCandidate == npc)
            assignedWarriorCandidate = null;
    }

    private void CleanupAssignments()
    {
        if (assignedWorker != null && !assignedWorker.IsAssignedToWorkshop(this))
            assignedWorker = null;

        if (assignedWarriorCandidate != null && !assignedWarriorCandidate.IsAssignedToSwordPickup(this))
            assignedWarriorCandidate = null;
    }

    private void HandlePlayerInteraction()
    {
        if (!Input.GetKeyDown(orderSwordKey))
            return;

        if (PlayerTopDownController.Instance != null && !PlayerTopDownController.Instance.IsAlive)
            return;

        Transform player = GetPlayerTransform();
        if (player == null)
            return;

        if (!IsClosestWorkshopForPlayer(player))
            return;

        if (!IsPlayerInInteractionRange(player))
            return;

        OrderSword();
    }

    private void OrderSword()
    {
        if (ResourceInventory.Instance != null && !ResourceInventory.Instance.TrySpend(swordCost))
        {
            Debug.Log($"Недостаточно ресурсов для меча. Нужно: {GetSwordCostText()}");
            return;
        }

        queuedSwords++;

        if (swordCraftDuration <= 0f)
        {
            CompleteSwordCraft();
            return;
        }

        if (craftingTimer <= 0f)
            craftingTimer = swordCraftDuration;

        Debug.Log($"Заказан меч. В очереди: {queuedSwords}, готово: {readySwords}");
    }

    private void TryAssignWorker()
    {
        if (NpcManager.Instance == null)
            return;

        RecruitableNpc npc = NpcManager.Instance.GetFirstFreeCampVillager();
        if (npc == null)
            return;

        if (npc.AssignToWorkshop(this))
        {
            assignedWorker = npc;
            Debug.Log($"{npc.NpcName} назначен в мастерскую");
        }
    }

    private void UpdateSwordCrafting()
    {
        if (queuedSwords <= 0)
        {
            craftingTimer = 0f;
            return;
        }

        if (assignedWorker == null || !assignedWorker.IsWorkingAtWorkshop(this))
            return;

        if (swordCraftDuration <= 0f)
        {
            CompleteSwordCraft();
            return;
        }

        if (craftingTimer <= 0f)
            craftingTimer = swordCraftDuration;

        craftingTimer -= Time.deltaTime;

        while (queuedSwords > 0 && craftingTimer <= 0f)
        {
            CompleteSwordCraft();

            if (queuedSwords > 0)
                craftingTimer += swordCraftDuration;
            else
                craftingTimer = 0f;
        }
    }

    private void CompleteSwordCraft()
    {
        if (queuedSwords <= 0)
            return;

        queuedSwords--;
        readySwords++;

        Debug.Log($"Меч готов в мастерской. В наличии: {readySwords}");
    }

    private void TryAssignWarriorCandidate()
    {
        if (readySwords <= 0 || assignedWarriorCandidate != null)
            return;

        if (NpcManager.Instance == null)
            return;

        RecruitableNpc npc = NpcManager.Instance.GetFirstFreeCampVillager();
        if (npc == null)
            return;

        if (npc.AssignToCollectSword(this))
        {
            assignedWarriorCandidate = npc;
            Debug.Log($"{npc.NpcName} идёт за мечом в мастерскую");
        }
    }

    private Transform GetPlayerTransform()
    {
        if (PlayerTopDownController.Instance != null)
            return PlayerTopDownController.Instance.transform;

        if (playerTransform != null)
            return playerTransform;

        PlayerTopDownController player = FindObjectOfType<PlayerTopDownController>();
        if (player != null)
            playerTransform = player.transform;

        return playerTransform;
    }

    private bool IsPlayerInInteractionRange(Transform player)
    {
        Vector3 workshopFlat = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
        return Vector3.Distance(workshopFlat, playerFlat) <= interactionRadius;
    }

    private bool IsClosestWorkshopForPlayer(Transform player)
    {
        Workshop closestWorkshop = null;
        float bestDistance = float.PositiveInfinity;
        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);

        for (int i = 0; i < activeWorkshops.Count; i++)
        {
            Workshop workshop = activeWorkshops[i];
            if (workshop == null || !workshop.isActiveAndEnabled)
                continue;

            Vector3 workshopFlat = new Vector3(workshop.transform.position.x, 0f, workshop.transform.position.z);
            float sqrDistance = (workshopFlat - playerFlat).sqrMagnitude;

            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                closestWorkshop = workshop;
            }
        }

        return closestWorkshop == this;
    }

    private string GetSwordCostText()
    {
        if (ResourceInventory.Instance == null)
            return "бесплатно";

        return ResourceInventory.Instance.GetCostText(swordCost);
    }

    private void EnsureDefaultSwordCost()
    {
        if (swordCost != null)
            return;

        swordCost = new[]
        {
            new ResourceCost { resourceType = ResourceType.Wood, amount = 1 },
            new ResourceCost { resourceType = ResourceType.Iron, amount = 2 }
        };
    }

    private void OnGUI()
    {
        if (!showInteractionUI)
            return;

        if (PlayerTopDownController.Instance != null && !PlayerTopDownController.Instance.IsAlive)
            return;

        Transform player = GetPlayerTransform();
        if (player == null)
            return;

        if (!IsClosestWorkshopForPlayer(player) || !IsPlayerInInteractionRange(player))
            return;

        Rect boxRect = new Rect(Screen.width * 0.5f - 170f, Screen.height - 130f, 340f, 100f);

        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.Box(boxRect, "");

        GUI.color = Color.white;
        GUI.Label(new Rect(boxRect.x + 12f, boxRect.y + 10f, 320f, 20f), "Мастерская");
        GUI.Label(
            new Rect(boxRect.x + 12f, boxRect.y + 32f, 320f, 20f),
            $"{orderSwordKey} - заказать меч ({GetSwordCostText()})"
        );
        GUI.Label(
            new Rect(boxRect.x + 12f, boxRect.y + 54f, 320f, 20f),
            $"В очереди: {queuedSwords} | Готово: {readySwords}"
        );
        GUI.Label(
            new Rect(boxRect.x + 12f, boxRect.y + 76f, 320f, 20f),
            assignedWorker != null ? $"Кузнец: {assignedWorker.NpcName}" : "Кузнец ещё не назначен"
        );
    }

    private void OnDestroy()
    {
        if (assignedWorker != null)
        {
            RecruitableNpc worker = assignedWorker;
            assignedWorker = null;
            worker.ReleaseWorkshopAssignment();
        }

        if (assignedWarriorCandidate != null)
        {
            RecruitableNpc candidate = assignedWarriorCandidate;
            assignedWarriorCandidate = null;
            candidate.ReleaseSwordAssignment();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
