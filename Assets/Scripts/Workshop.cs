using UnityEngine;

public class Workshop : MonoBehaviour
{
    [SerializeField] private Transform workPoint;
    [SerializeField] private float assignCheckInterval = 1f;

    private RecruitableNpc assignedWorker;
    private float assignTimer;

    public bool HasWorker => assignedWorker != null;
    public RecruitableNpc AssignedWorker => assignedWorker;

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

    private void Update()
    {
        if (assignedWorker != null && !assignedWorker.IsAssignedToWorkshop(this))
        {
            assignedWorker = null;
        }

        if (assignedWorker != null)
            return;

        assignTimer -= Time.deltaTime;
        if (assignTimer > 0f)
            return;

        assignTimer = assignCheckInterval;
        TryAssignWorker();
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

    public void ClearWorker(RecruitableNpc npc)
    {
        if (assignedWorker == npc)
            assignedWorker = null;
    }

    private void OnDestroy()
    {
        if (assignedWorker != null)
        {
            RecruitableNpc worker = assignedWorker;
            assignedWorker = null;
            worker.ReleaseWorkshopAssignment();
        }
    }
}