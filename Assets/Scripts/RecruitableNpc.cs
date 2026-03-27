using System.Collections.Generic;
using UnityEngine;

public class RecruitableNpc : MonoBehaviour
{
    private enum NpcState
    {
        IdleAtHome,
        WanderingHome,
        GoingToCamp,
        IdleAtCamp,
        WanderingCamp,
        GoingToWorkshop,
        WorkingAtWorkshop
    }

    [Header("Identity")]
    [SerializeField] private string npcName = "NPC";

    [Header("Home Wander")]
    [SerializeField] private float homeWanderRadius = 6f;

    [Header("Movement")]
    [SerializeField] private Vector2 wanderSpeedRange = new Vector2(1.2f, 2.2f);
    [SerializeField] private Vector2 idleTimeRange = new Vector2(1f, 3f);
    [SerializeField] private float moveReachDistance = 0.15f;
    [SerializeField] private float campReachDistance = 0.4f;
    [SerializeField] private float workshopReachDistance = 0.2f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float recruitedSpeedMultiplier = 1.2f;

    [Header("Visual")]
    [SerializeField] private Renderer[] renderersToTint;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color friendlyColor = new Color(0.4f, 1f, 0.4f, 1f);

    private Transform homeTransform;
    private Vector3 fallbackHomePosition;

    private Vector3 currentTarget;
    private float currentSpeed;
    private float idleTimer;

    private bool isRecruited;
    private NpcState state;
    private Camp targetCamp;
    private Workshop assignedWorkshop;

    private readonly List<Material> materials = new List<Material>();

    public string NpcName => npcName;
    public bool IsRecruited => isRecruited;

    private void Awake()
    {
        CacheMaterials();
        SetTint(neutralColor);
        AssignRandomNameIfNeeded();
    }

    private void Start()
    {
        if (homeTransform == null)
        {
            fallbackHomePosition = transform.position;
            EnterIdleAtHome();
        }
    }

    public void Initialize(Transform home)
    {
        homeTransform = home;
        fallbackHomePosition = home != null ? home.position : transform.position;
        EnterIdleAtHome();
    }

    private void Update()
    {
        if (assignedWorkshop != null)
        {
            if (state != NpcState.GoingToWorkshop && state != NpcState.WorkingAtWorkshop)
                state = NpcState.GoingToWorkshop;
        }
        else if (isRecruited)
        {
            RefreshCampTargetIfNeeded();
        }

        switch (state)
        {
            case NpcState.IdleAtHome:
                UpdateIdleAtHome();
                break;

            case NpcState.WanderingHome:
                UpdateMove(currentTarget, moveReachDistance, OnReachedHomeTarget);
                break;

            case NpcState.GoingToCamp:
                UpdateMove(currentTarget, campReachDistance, OnReachedCampTarget);
                break;

            case NpcState.IdleAtCamp:
                UpdateIdleAtCamp();
                break;

            case NpcState.WanderingCamp:
                UpdateMove(currentTarget, campReachDistance, OnReachedCampTarget);
                break;

            case NpcState.GoingToWorkshop:
                UpdateGoingToWorkshop();
                break;

            case NpcState.WorkingAtWorkshop:
                UpdateWorkingAtWorkshop();
                break;
        }
    }

    public bool TryRecruit()
    {
        if (isRecruited)
            return false;

        isRecruited = true;
        SetTint(friendlyColor);

        if (NpcManager.Instance != null)
            NpcManager.Instance.RegisterRecruited(this);

        RefreshCampTarget(true);
        return true;
    }

    public bool IsAvailableForWork()
    {
        return isRecruited
            && assignedWorkshop == null
            && (state == NpcState.IdleAtCamp || state == NpcState.WanderingCamp);
    }

    public bool AssignToWorkshop(Workshop workshop)
    {
        if (!isRecruited || workshop == null)
            return false;

        if (assignedWorkshop == workshop)
            return true;

        if (assignedWorkshop != null)
            assignedWorkshop.ClearWorker(this);

        assignedWorkshop = workshop;
        currentSpeed = Random.Range(wanderSpeedRange.x, wanderSpeedRange.y) * recruitedSpeedMultiplier;
        state = NpcState.GoingToWorkshop;
        return true;
    }

    public void ReleaseWorkshopAssignment()
    {
        if (assignedWorkshop != null)
        {
            Workshop oldWorkshop = assignedWorkshop;
            assignedWorkshop = null;
            oldWorkshop.ClearWorker(this);
        }

        if (isRecruited)
            RefreshCampTarget(true);
    }

    public bool IsAssignedToWorkshop(Workshop workshop)
    {
        return assignedWorkshop == workshop;
    }

    public string GetDebugStateText()
    {
        switch (state)
        {
            case NpcState.IdleAtHome: return "дома стоит";
            case NpcState.WanderingHome: return "бродит у лачуги";
            case NpcState.GoingToCamp: return "идёт к лагерю";
            case NpcState.IdleAtCamp: return "отдыхает у костра";
            case NpcState.WanderingCamp: return "гуляет у костра";
            case NpcState.GoingToWorkshop: return "идёт в мастерскую";
            case NpcState.WorkingAtWorkshop: return "работает в мастерской";
            default: return "неизвестно";
        }
    }

    private void UpdateIdleAtHome()
    {
        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
            PickNewHomeTarget();
    }

    private void UpdateIdleAtCamp()
    {
        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
            PickNewCampTarget();
    }

    private void EnterIdleAtHome()
    {
        state = NpcState.IdleAtHome;
        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
    }

    private void EnterIdleAtCamp()
    {
        state = NpcState.IdleAtCamp;
        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
    }

    private void PickNewHomeTarget()
    {
        Vector3 center = GetHomeCenter();
        Vector2 offset = Random.insideUnitCircle * homeWanderRadius;

        currentTarget = center + new Vector3(offset.x, 0f, offset.y);
        currentSpeed = Random.Range(wanderSpeedRange.x, wanderSpeedRange.y);
        state = NpcState.WanderingHome;
    }

    private void PickNewCampTarget()
    {
        if (targetCamp == null)
        {
            EnterIdleAtCamp();
            return;
        }

        currentTarget = targetCamp.GetRandomWaitingPosition();
        currentSpeed = Random.Range(wanderSpeedRange.x, wanderSpeedRange.y);
        state = NpcState.WanderingCamp;
    }

    private void OnReachedHomeTarget()
    {
        EnterIdleAtHome();
    }

    private void OnReachedCampTarget()
    {
        EnterIdleAtCamp();
    }

    private void UpdateGoingToWorkshop()
    {
        if (assignedWorkshop == null)
        {
            if (isRecruited)
                RefreshCampTarget(true);

            return;
        }

        Vector3 workshopTarget = assignedWorkshop.GetWorkPosition();
        UpdateMove(workshopTarget, workshopReachDistance, OnReachedWorkshop);
    }

    private void OnReachedWorkshop()
    {
        state = NpcState.WorkingAtWorkshop;
    }

    private void UpdateWorkingAtWorkshop()
    {
        if (assignedWorkshop == null)
        {
            if (isRecruited)
                RefreshCampTarget(true);

            return;
        }

        Vector3 workPos = assignedWorkshop.GetWorkPosition();
        Vector3 targetPos = new Vector3(workPos.x, transform.position.y, workPos.z);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            currentSpeed * Time.deltaTime
        );

        Quaternion targetRotation = assignedWorkshop.GetWorkRotation();
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void RefreshCampTargetIfNeeded()
    {
        Camp currentCamp = CampManager.Instance != null ? CampManager.Instance.CurrentCamp : null;

        if (currentCamp == null)
        {
            targetCamp = null;
            return;
        }

        if (currentCamp != targetCamp)
            RefreshCampTarget(true);
    }

    private void RefreshCampTarget(bool forceNewPoint = false)
    {
        Camp currentCamp = CampManager.Instance != null ? CampManager.Instance.CurrentCamp : null;
        targetCamp = currentCamp;

        if (targetCamp == null)
            return;

        if (forceNewPoint || state != NpcState.GoingToCamp)
        {
            currentTarget = targetCamp.GetRandomWaitingPosition();
            currentSpeed = Random.Range(wanderSpeedRange.x, wanderSpeedRange.y) * recruitedSpeedMultiplier;
            state = NpcState.GoingToCamp;
        }
    }

    private void UpdateMove(Vector3 target, float stopDistance, System.Action onReached)
    {
        Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
        Vector3 toTarget = flatTarget - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= stopDistance)
        {
            onReached?.Invoke();
            return;
        }

        Vector3 direction = toTarget / distance;
        transform.position += direction * currentSpeed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private Vector3 GetHomeCenter()
    {
        if (homeTransform != null)
            return homeTransform.position;

        return fallbackHomePosition;
    }

    private void AssignRandomNameIfNeeded()
    {
        if (!string.IsNullOrWhiteSpace(npcName) && npcName != "NPC")
            return;

        if (NpcManager.Instance != null)
            npcName = NpcManager.Instance.GetRandomName();

        gameObject.name = $"NPC_{npcName}";
    }

    private void CacheMaterials()
    {
        materials.Clear();

        if (renderersToTint == null || renderersToTint.Length == 0)
            renderersToTint = GetComponentsInChildren<Renderer>();

        foreach (Renderer rend in renderersToTint)
        {
            Material[] mats = rend.materials;
            foreach (Material mat in mats)
            {
                if (mat != null)
                    materials.Add(mat);
            }
        }
    }

    private void SetTint(Color color)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material mat = materials[i];
            if (mat == null)
                continue;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
        }
    }

    private void OnDestroy()
    {
        if (assignedWorkshop != null)
            assignedWorkshop.ClearWorker(this);

        if (NpcManager.Instance != null)
            NpcManager.Instance.UnregisterRecruited(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isRecruited ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, homeWanderRadius);
    }
}