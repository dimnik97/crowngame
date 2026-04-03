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
        WorkingAtWorkshop,
        GoingToArmory,
        WarriorPatrolling,
        WarriorFollowingPlayer,
        WarriorChasingEnemy
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
    [SerializeField] private float swordPickupReachDistance = 0.25f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float recruitedSpeedMultiplier = 1.2f;

    [Header("Warrior")]
    [SerializeField] private float warriorPatrolRadius = 8f;
    [SerializeField] private float warriorFollowRadius = 3.5f;
    [SerializeField] private float warriorFollowCatchUpMultiplier = 1.6f;
    [SerializeField] private float warriorDetectionRadius = 6f;
    [SerializeField] private float warriorAttackRange = 1.35f;
    [SerializeField] private float warriorAttackCooldown = 0.9f;
    [SerializeField] private int warriorDamage = 1;
    [SerializeField] private float warriorMoveSpeedMultiplier = 1.45f;
    [SerializeField] private float enemyLoseDistanceMultiplier = 1.5f;
    [SerializeField] private LayerMask hostileMask = ~0;

    [Header("Visual")]
    [SerializeField] private Renderer[] renderersToTint;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color friendlyColor = new Color(0.4f, 1f, 0.4f, 1f);
    [SerializeField] private Color warriorColor = new Color(1f, 0.75f, 0.35f, 1f);

    [Header("Health")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2.1f, 0f);

    private Transform homeTransform;
    private Vector3 fallbackHomePosition;

    private Vector3 currentTarget;
    private float currentSpeed;
    private float idleTimer;
    private float warriorAttackTimer;

    private bool isRecruited;
    private bool isWarrior;
    private bool isFollowingPlayer;
    private NpcState state;
    private Camp targetCamp;
    private Workshop assignedWorkshop;
    private Workshop swordWorkshop;
    private Transform followedPlayer;
    private HostileTarget currentEnemy;
    private Health health;

    private readonly List<Material> materials = new List<Material>();

    public string NpcName => npcName;
    public bool IsRecruited => isRecruited;
    public bool IsWarrior => isWarrior;
    public bool IsFollowingPlayer => isFollowingPlayer;
    public bool IsAlive => health == null || health.IsAlive;
    public Health Health => health;

    private void Awake()
    {
        EnsureHealthSetup();
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
        if (!IsAlive)
            return;

        if (warriorAttackTimer > 0f)
            warriorAttackTimer -= Time.deltaTime;

        if (assignedWorkshop != null)
        {
            if (state != NpcState.GoingToWorkshop && state != NpcState.WorkingAtWorkshop)
                state = NpcState.GoingToWorkshop;
        }
        else if (swordWorkshop != null)
        {
            if (state != NpcState.GoingToArmory)
                state = NpcState.GoingToArmory;
        }
        else if (isWarrior)
        {
            RefreshCampTargetIfNeeded();

            if (isFollowingPlayer)
            {
                if (!HasActiveFollowLeader())
                    ReleasePlayerFollowCommand();
                else if (state != NpcState.WarriorFollowingPlayer && state != NpcState.WarriorChasingEnemy)
                    EnterWarriorFollow(true);
            }
            else if (state != NpcState.WarriorPatrolling && state != NpcState.WarriorChasingEnemy)
            {
                EnterWarriorPatrol(true);
            }
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

            case NpcState.GoingToArmory:
                UpdateGoingToArmory();
                break;

            case NpcState.WarriorPatrolling:
                UpdateWarriorPatrol();
                break;

            case NpcState.WarriorFollowingPlayer:
                UpdateWarriorFollowingPlayer();
                break;

            case NpcState.WarriorChasingEnemy:
                UpdateWarriorChasingEnemy();
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
            && !isWarrior
            && assignedWorkshop == null
            && swordWorkshop == null
            && (state == NpcState.IdleAtCamp || state == NpcState.WanderingCamp);
    }

    public bool AssignToWorkshop(Workshop workshop)
    {
        if (!isRecruited || isWarrior || workshop == null || swordWorkshop != null)
            return false;

        if (assignedWorkshop == workshop)
            return true;

        if (assignedWorkshop != null)
            assignedWorkshop.ClearWorker(this);

        assignedWorkshop = workshop;
        currentSpeed = GetRecruitedMoveSpeed();
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

        if (isWarrior)
            ResumeWarriorDuty(true);
        else if (isRecruited)
            RefreshCampTarget(true);
    }

    public bool AssignToCollectSword(Workshop workshop)
    {
        if (!IsAvailableForWork() || workshop == null)
            return false;

        if (swordWorkshop == workshop)
            return true;

        if (swordWorkshop != null)
            swordWorkshop.ClearWarriorCandidate(this);

        swordWorkshop = workshop;
        currentEnemy = null;
        currentSpeed = GetRecruitedMoveSpeed();
        state = NpcState.GoingToArmory;
        return true;
    }

    public void ReleaseSwordAssignment()
    {
        if (swordWorkshop != null)
        {
            Workshop oldWorkshop = swordWorkshop;
            swordWorkshop = null;
            oldWorkshop.ClearWarriorCandidate(this);
        }

        if (isWarrior)
            ResumeWarriorDuty(true);
        else if (isRecruited)
            RefreshCampTarget(true);
    }

    public bool TryAssignFollowPlayer(Transform player)
    {
        if (!isWarrior || !IsAlive || player == null)
            return false;

        isFollowingPlayer = true;
        followedPlayer = player;

        if (state != NpcState.WarriorChasingEnemy)
            EnterWarriorFollow(true);

        return true;
    }

    public bool ReleasePlayerFollowCommand()
    {
        if (!isWarrior || !isFollowingPlayer)
            return false;

        isFollowingPlayer = false;
        followedPlayer = null;

        if (state != NpcState.WarriorChasingEnemy)
            EnterWarriorPatrol(true);

        return true;
    }

    public bool IsAssignedToWorkshop(Workshop workshop)
    {
        return assignedWorkshop == workshop;
    }

    public bool IsAssignedToSwordPickup(Workshop workshop)
    {
        return swordWorkshop == workshop;
    }

    public bool IsWorkingAtWorkshop(Workshop workshop)
    {
        return assignedWorkshop == workshop && state == NpcState.WorkingAtWorkshop;
    }

    public string GetDebugStateText()
    {
        switch (state)
        {
            case NpcState.IdleAtHome: return "дома стоит";
            case NpcState.WanderingHome: return "бродит у дома";
            case NpcState.GoingToCamp: return "идёт к лагерю";
            case NpcState.IdleAtCamp: return "отдыхает у лагеря";
            case NpcState.WanderingCamp: return "гуляет у лагеря";
            case NpcState.GoingToWorkshop: return "идёт в мастерскую";
            case NpcState.WorkingAtWorkshop: return "работает в мастерской";
            case NpcState.GoingToArmory: return "идёт за мечом";
            case NpcState.WarriorPatrolling: return "патрулирует лагерь";
            case NpcState.WarriorFollowingPlayer: return "следует за игроком";
            case NpcState.WarriorChasingEnemy: return "атакует врага";
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

    private void EnterWarriorPatrol(bool pickNewTarget)
    {
        currentEnemy = null;
        state = NpcState.WarriorPatrolling;
        currentSpeed = GetWarriorMoveSpeed();

        if (pickNewTarget)
            PickNewWarriorPatrolTarget();
    }

    private void EnterWarriorFollow(bool pickNewTarget)
    {
        currentEnemy = null;
        state = NpcState.WarriorFollowingPlayer;
        currentSpeed = GetWarriorMoveSpeed();

        if (pickNewTarget)
            PickNewWarriorFollowTarget();
    }

    private void ResumeWarriorDuty(bool pickNewTarget)
    {
        if (isFollowingPlayer && HasActiveFollowLeader())
        {
            EnterWarriorFollow(pickNewTarget);
            return;
        }

        isFollowingPlayer = false;
        followedPlayer = null;
        EnterWarriorPatrol(pickNewTarget);
    }

    private void PickNewHomeTarget()
    {
        Vector3 center = GetHomeCenter();
        Vector2 offset = Random.insideUnitCircle * homeWanderRadius;

        currentTarget = center + new Vector3(offset.x, 0f, offset.y);
        currentSpeed = GetBaseMoveSpeed();
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
        currentSpeed = GetRecruitedMoveSpeed();
        state = NpcState.WanderingCamp;
    }

    private void PickNewWarriorPatrolTarget()
    {
        if (targetCamp == null)
        {
            RefreshCampTarget(true);
            return;
        }

        currentTarget = targetCamp.GetRandomPatrolPosition(warriorPatrolRadius);
        currentSpeed = GetWarriorMoveSpeed();
    }

    private void PickNewWarriorFollowTarget()
    {
        if (!HasActiveFollowLeader())
        {
            ReleasePlayerFollowCommand();
            return;
        }

        Vector2 offset = Random.insideUnitCircle * warriorFollowRadius;
        Vector3 center = followedPlayer.position;
        currentTarget = center + new Vector3(offset.x, 0f, offset.y);
        currentSpeed = GetWarriorMoveSpeed();
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
            if (isWarrior)
                ResumeWarriorDuty(true);
            else if (isRecruited)
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
            if (isWarrior)
                ResumeWarriorDuty(true);
            else if (isRecruited)
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

    private void UpdateGoingToArmory()
    {
        if (swordWorkshop == null)
        {
            if (isWarrior)
                ResumeWarriorDuty(true);
            else if (isRecruited)
                RefreshCampTarget(true);

            return;
        }

        Vector3 swordTarget = swordWorkshop.GetSwordPickupPosition();
        UpdateMove(swordTarget, swordPickupReachDistance, OnReachedSwordPickup);
    }

    private void OnReachedSwordPickup()
    {
        if (swordWorkshop == null)
        {
            if (isRecruited)
                RefreshCampTarget(true);

            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            swordWorkshop.GetSwordPickupRotation(),
            rotationSpeed * Time.deltaTime
        );

        Workshop sourceWorkshop = swordWorkshop;

        if (!sourceWorkshop.TryGiveSwordTo(this))
        {
            ReleaseSwordAssignment();
            return;
        }

        swordWorkshop = null;
        BecomeWarrior();
    }

    private void BecomeWarrior()
    {
        isWarrior = true;
        currentEnemy = null;
        SetTint(warriorColor);
        warriorAttackTimer = 0f;
        ResumeWarriorDuty(true);
        Debug.Log($"{npcName} стал воином");
    }

    private void UpdateWarriorPatrol()
    {
        if (TryAcquireEnemyTarget())
            return;

        if (targetCamp == null)
            return;

        UpdateMove(currentTarget, campReachDistance, PickNewWarriorPatrolTarget);
    }

    private void UpdateWarriorFollowingPlayer()
    {
        if (!HasActiveFollowLeader())
        {
            ReleasePlayerFollowCommand();
            return;
        }

        if (TryAcquireEnemyTarget())
            return;

        float distanceToLeader = GetFlatDistanceTo(followedPlayer.position);
        float catchUpDistance = Mathf.Max(
            warriorFollowRadius + campReachDistance,
            warriorFollowRadius * warriorFollowCatchUpMultiplier
        );

        if (distanceToLeader > catchUpDistance)
        {
            currentSpeed = GetWarriorMoveSpeed();
            UpdateMove(followedPlayer.position, warriorFollowRadius * 0.55f, PickNewWarriorFollowTarget);
            return;
        }

        if (ShouldRefreshFollowTarget())
            PickNewWarriorFollowTarget();

        UpdateMove(currentTarget, campReachDistance, PickNewWarriorFollowTarget);
    }

    private void UpdateWarriorChasingEnemy()
    {
        if (!IsEnemyValid(currentEnemy))
        {
            LoseEnemyAndResumeDuty();
            return;
        }

        Vector3 enemyPosition = currentEnemy.transform.position;
        Vector3 toEnemy = enemyPosition - transform.position;
        toEnemy.y = 0f;

        float distance = toEnemy.magnitude;
        float loseDistance = Mathf.Max(warriorDetectionRadius, warriorAttackRange) * enemyLoseDistanceMultiplier;

        if (distance > loseDistance)
        {
            LoseEnemyAndResumeDuty();
            return;
        }

        if (distance > warriorAttackRange)
        {
            UpdateMove(enemyPosition, warriorAttackRange * 0.85f, null);
            return;
        }

        if (toEnemy.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toEnemy.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        TryAttackEnemy();
    }

    private bool TryAcquireEnemyTarget()
    {
        if (warriorDetectionRadius <= 0f)
            return false;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            warriorDetectionRadius,
            hostileMask,
            QueryTriggerInteraction.Collide
        );

        HostileTarget nearestEnemy = null;
        float nearestDistance = float.PositiveInfinity;
        HashSet<HostileTarget> uniqueTargets = new HashSet<HostileTarget>();

        for (int i = 0; i < hits.Length; i++)
        {
            HostileTarget enemy = hits[i].GetComponentInParent<HostileTarget>();
            if (!IsEnemyValid(enemy) || !uniqueTargets.Add(enemy))
                continue;

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;

            float sqrDistance = toEnemy.sqrMagnitude;
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestEnemy = enemy;
            }
        }

        if (nearestEnemy == null)
            return false;

        currentEnemy = nearestEnemy;
        currentSpeed = GetWarriorMoveSpeed();
        state = NpcState.WarriorChasingEnemy;
        return true;
    }

    private void TryAttackEnemy()
    {
        if (!IsEnemyValid(currentEnemy))
        {
            LoseEnemyAndResumeDuty();
            return;
        }

        if (warriorAttackTimer > 0f)
            return;

        currentEnemy.TakeDamage(warriorDamage);
        warriorAttackTimer = warriorAttackCooldown;

        if (!IsEnemyValid(currentEnemy))
            LoseEnemyAndResumeDuty();
    }

    private void LoseEnemyAndResumeDuty()
    {
        currentEnemy = null;
        ResumeWarriorDuty(true);
    }

    private bool IsEnemyValid(HostileTarget enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && enemy.IsAlive;
    }

    private bool ShouldRefreshFollowTarget()
    {
        if (!HasActiveFollowLeader())
            return false;

        Vector3 leaderOffset = currentTarget - followedPlayer.position;
        leaderOffset.y = 0f;
        if (leaderOffset.sqrMagnitude > warriorFollowRadius * warriorFollowRadius)
            return true;

        return GetFlatSqrDistanceTo(currentTarget) <= campReachDistance * campReachDistance;
    }

    private bool HasActiveFollowLeader()
    {
        if (!isFollowingPlayer || followedPlayer == null)
            return false;

        PlayerTopDownController leaderController = followedPlayer.GetComponent<PlayerTopDownController>();
        return leaderController == null || leaderController.IsAlive;
    }

    private void RefreshCampTargetIfNeeded()
    {
        Camp currentCamp = CampManager.Instance != null ? CampManager.Instance.CurrentCamp : null;

        if (currentCamp == null)
        {
            targetCamp = null;
            currentEnemy = null;
            return;
        }

        if (currentCamp == targetCamp)
            return;

        targetCamp = currentCamp;

        if (isWarrior)
        {
            ResumeWarriorDuty(true);
            return;
        }

        RefreshCampTarget(true);
    }

    private void RefreshCampTarget(bool forceNewPoint = false)
    {
        Camp currentCamp = CampManager.Instance != null ? CampManager.Instance.CurrentCamp : null;
        targetCamp = currentCamp;

        if (targetCamp == null)
            return;

        if (isWarrior)
        {
            ResumeWarriorDuty(forceNewPoint);
            return;
        }

        if (forceNewPoint || state != NpcState.GoingToCamp)
        {
            currentTarget = targetCamp.GetRandomWaitingPosition();
            currentSpeed = GetRecruitedMoveSpeed();
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

    private float GetBaseMoveSpeed()
    {
        return Random.Range(wanderSpeedRange.x, wanderSpeedRange.y);
    }

    private float GetRecruitedMoveSpeed()
    {
        return GetBaseMoveSpeed() * recruitedSpeedMultiplier;
    }

    private float GetWarriorMoveSpeed()
    {
        return GetBaseMoveSpeed() * warriorMoveSpeedMultiplier;
    }

    private Vector3 GetHomeCenter()
    {
        if (homeTransform != null)
            return homeTransform.position;

        return fallbackHomePosition;
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

        if (swordWorkshop != null)
            swordWorkshop.ClearWarriorCandidate(this);

        if (NpcManager.Instance != null)
            NpcManager.Instance.UnregisterRecruited(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isRecruited ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, homeWanderRadius);

        if (!isWarrior)
            return;

        Gizmos.color = new Color(1f, 0.65f, 0.2f, 1f);

        if (isFollowingPlayer && followedPlayer != null)
            Gizmos.DrawWireSphere(followedPlayer.position, warriorFollowRadius);
        else
        {
            Vector3 center = targetCamp != null ? targetCamp.WaitingCenter : transform.position;
            Gizmos.DrawWireSphere(center, warriorPatrolRadius);
        }

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, warriorDetectionRadius);
    }

    private void EnsureHealthSetup()
    {
        health = GetComponent<Health>();
        if (health == null)
            health = gameObject.AddComponent<Health>();

        health.Configure(maxHealth, true, true);

        WorldHealthBar healthBar = GetComponent<WorldHealthBar>();
        if (healthBar == null)
            healthBar = gameObject.AddComponent<WorldHealthBar>();

        healthBar.Configure(health, healthBarOffset);
    }
}
