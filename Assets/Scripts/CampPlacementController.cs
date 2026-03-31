using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CampPlacementController : MonoBehaviour
{
    private enum BuildType
    {
        None,
        Campfire,
        Workshop
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask groundMask;

    [Header("Prefabs")]
    [SerializeField] private GameObject campfirePrefab;
    [SerializeField] private GameObject workshopPrefab;

    [Header("Placement Settings")]
    [SerializeField] private float placementRadius = 6f;
    [SerializeField] private float campBuildRadius = 8f;
    [SerializeField] private float previewYOffset = 0.05f;
    [SerializeField] private float spawnYOffset = 0f;
    [SerializeField] private KeyCode selectCampfireKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode selectWorkshopKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
    [SerializeField] private KeyCode cancelMouseKey = KeyCode.Mouse1;

    [Header("Costs")]
    [SerializeField] private ResourceCost[] campfireCost;
    [SerializeField] private ResourceCost[] workshopCost;

    [Header("Preview Colors")]
    [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("UI")]
    [SerializeField] private GameObject buildModePanel;
    [SerializeField] private Text buildModeText;

    [Header("World Radius Visualization")]
    [SerializeField] private bool showWorldRadii = true;
    [SerializeField] private int radiusSegments = 64;
    [SerializeField] private float radiusLineWidth = 0.08f;
    [SerializeField] private float radiusYOffset = 0.08f;
    [SerializeField] private Color playerRadiusColor = new Color(1f, 0.92f, 0.25f, 0.95f);
    [SerializeField] private Color campRadiusColor = new Color(0.25f, 0.9f, 1f, 0.95f);

    private bool isPlacementMode;
    private BuildType selectedBuildType = BuildType.None;

    private GameObject previewInstance;
    private readonly List<Material> previewMaterials = new List<Material>();
    private GameObject previewSourcePrefab;

    private Vector3 currentPreviewPoint;
    private bool hasPreviewPoint;
    private bool canPlaceHere;

    private GameObject playerRadiusVisual;
    private GameObject campRadiusVisual;
    private LineRenderer playerRadiusRenderer;
    private LineRenderer campRadiusRenderer;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        EnsureRadiusVisualsExist();
        SetRadiusVisualsVisible(false, false);
    }

    private void Start()
    {
        CancelPlacement();
    }

    private void Update()
    {
        if (Input.GetKeyDown(selectCampfireKey))
        {
            if (isPlacementMode && selectedBuildType == BuildType.Campfire)
                CancelPlacement();
            else
                EnterPlacementMode(BuildType.Campfire);
        }

        if (Input.GetKeyDown(selectWorkshopKey))
        {
            if (isPlacementMode && selectedBuildType == BuildType.Workshop)
                CancelPlacement();
            else
                EnterPlacementMode(BuildType.Workshop);
        }

        if (!isPlacementMode)
            return;

        UpdatePreview();
        UpdateRadiusVisuals();

        if (Input.GetKeyDown(cancelKey) || Input.GetKeyDown(cancelMouseKey))
        {
            CancelPlacement();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (PointerIsOverUI())
                return;

            TryPlaceSelectedBuild();
        }
    }

    public void SelectCampfireFromUI()
    {
        EnterPlacementMode(BuildType.Campfire);
    }

    public void SelectWorkshopFromUI()
    {
        EnterPlacementMode(BuildType.Workshop);
    }

    private void EnterPlacementMode(BuildType buildType)
    {
        GameObject prefab = GetPrefabForBuildType(buildType);
        if (prefab == null)
        {
            Debug.LogWarning($"Для {GetBuildDisplayName(buildType)} не назначен prefab");
            return;
        }

        selectedBuildType = buildType;

        if (!HasCampForSelectedBuild())
        {
            UpdateBuildModeUI(
                true,
                $"Нельзя строить: {GetBuildDisplayName(buildType)}\n" +
                "Сначала поставь костёр"
            );

            isPlacementMode = false;
            SetPreviewVisible(false);
            SetRadiusVisualsVisible(false, false);
            return;
        }

        if (!CanAfford(buildType))
        {
            ShowNotEnoughResourcesMessage(buildType);

            isPlacementMode = false;
            SetPreviewVisible(false);
            SetRadiusVisualsVisible(false, false);
            return;
        }

        isPlacementMode = true;

        EnsurePreviewExistsForSelectedBuild();
        SetPreviewVisible(true);
        UpdateRadiusVisuals();

        UpdateBuildModeUI(
            true,
            $"Режим строительства: {GetBuildDisplayName(selectedBuildType)}\n" +
            $"Стоимость: {GetCostText(selectedBuildType)}\n" +
            "ЛКМ — поставить\n" +
            "ПКМ / Esc — отмена"
        );
    }

    public void CancelPlacement()
    {
        isPlacementMode = false;
        hasPreviewPoint = false;
        canPlaceHere = false;
        selectedBuildType = BuildType.None;

        SetPreviewVisible(false);
        SetRadiusVisualsVisible(false, false);
        UpdateBuildModeUI(false, string.Empty);
    }

    private void TryPlaceSelectedBuild()
    {
        if (selectedBuildType == BuildType.None)
            return;

        if (!hasPreviewPoint)
            return;

        string blockReason = GetPlacementBlockReason(currentPreviewPoint);
        if (!string.IsNullOrEmpty(blockReason))
        {
            UpdateBuildModeUI(
                true,
                $"{blockReason}\n" +
                $"Стоимость: {GetCostText(selectedBuildType)}\n" +
                "ЛКМ — поставить\n" +
                "ПКМ / Esc — отмена"
            );
            return;
        }

        if (!CanAfford(selectedBuildType))
        {
            ShowNotEnoughResourcesMessage(selectedBuildType);
            CancelPlacement();
            return;
        }

        ResourceCost[] cost = GetCostForBuildType(selectedBuildType);
        if (ResourceInventory.Instance != null && !ResourceInventory.Instance.TrySpend(cost))
        {
            ShowNotEnoughResourcesMessage(selectedBuildType);
            CancelPlacement();
            return;
        }

        Vector3 spawnPoint = currentPreviewPoint + Vector3.up * spawnYOffset;
        GameObject prefab = GetPrefabForBuildType(selectedBuildType);
        GameObject spawnedObject = Instantiate(prefab, spawnPoint, Quaternion.identity);

        if (selectedBuildType == BuildType.Campfire)
        {
            Camp camp = spawnedObject.GetComponent<Camp>();
            if (camp == null)
                camp = spawnedObject.AddComponent<Camp>();

            if (CampManager.Instance != null)
                CampManager.Instance.SetCamp(camp);
        }
        else if (selectedBuildType == BuildType.Workshop)
        {
            Workshop workshop = spawnedObject.GetComponent<Workshop>();
            if (workshop == null)
                spawnedObject.AddComponent<Workshop>();
        }

        CancelPlacement();
    }

    private void UpdatePreview()
    {
        EnsurePreviewExistsForSelectedBuild();

        if (!TryGetGroundPoint(out Vector3 point))
        {
            hasPreviewPoint = false;
            canPlaceHere = false;
            SetPreviewVisible(false);

            UpdateBuildModeUI(
                true,
                "Наведи курсор на землю\n" +
                $"Стоимость: {GetCostText(selectedBuildType)}\n" +
                "ЛКМ — поставить\n" +
                "ПКМ / Esc — отмена"
            );

            return;
        }

        hasPreviewPoint = true;
        currentPreviewPoint = point;

        string blockReason = GetPlacementBlockReason(point);
        canPlaceHere = string.IsNullOrEmpty(blockReason);

        if (previewInstance != null)
        {
            previewInstance.transform.position = point + Vector3.up * previewYOffset;
            SetPreviewVisible(true);
            SetPreviewColor(canPlaceHere ? validColor : invalidColor);
        }

        if (canPlaceHere)
        {
            UpdateBuildModeUI(
                true,
                $"Режим строительства: {GetBuildDisplayName(selectedBuildType)}\n" +
                $"Стоимость: {GetCostText(selectedBuildType)}\n" +
                "ЛКМ — поставить\n" +
                "ПКМ / Esc — отмена"
            );
        }
        else
        {
            UpdateBuildModeUI(
                true,
                $"{blockReason}\n" +
                $"Стоимость: {GetCostText(selectedBuildType)}\n" +
                "ЛКМ — поставить\n" +
                "ПКМ / Esc — отмена"
            );
        }
    }

    private bool SelectedBuildRequiresCamp()
    {
        return selectedBuildType != BuildType.None &&
               selectedBuildType != BuildType.Campfire;
    }

    private bool HasCampForSelectedBuild()
    {
        if (!SelectedBuildRequiresCamp())
            return true;

        return CampManager.Instance != null && CampManager.Instance.HasCamp();
    }

    private bool IsInsideCampBuildRadius(Vector3 worldPoint)
    {
        if (CampManager.Instance == null || !CampManager.Instance.HasCamp())
            return false;

        Vector3 campCenter = CampManager.Instance.CurrentCamp.WaitingCenter;

        Vector3 campFlat = new Vector3(campCenter.x, 0f, campCenter.z);
        Vector3 pointFlat = new Vector3(worldPoint.x, 0f, worldPoint.z);

        return Vector3.Distance(campFlat, pointFlat) <= campBuildRadius;
    }

    private string GetPlacementBlockReason(Vector3 point)
    {
        if (!IsInsidePlacementRadius(point))
            return "Слишком далеко от игрока";

        if (SelectedBuildRequiresCamp() && !HasCampForSelectedBuild())
            return "Сначала поставь костёр";

        if (SelectedBuildRequiresCamp() && !IsInsideCampBuildRadius(point))
            return "Нужно строить рядом с лагерем";

        return string.Empty;
    }

    private bool TryGetGroundPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (mainCamera == null)
            return false;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundMask))
        {
            point = hit.point;
            return true;
        }

        return false;
    }

    private bool IsInsidePlacementRadius(Vector3 worldPoint)
    {
        if (player == null)
            return false;

        Vector3 playerFlat = new Vector3(player.position.x, 0f, player.position.z);
        Vector3 pointFlat = new Vector3(worldPoint.x, 0f, worldPoint.z);

        return Vector3.Distance(playerFlat, pointFlat) <= placementRadius;
    }

    private bool CanAfford(BuildType buildType)
    {
        if (ResourceInventory.Instance == null)
            return true;

        return ResourceInventory.Instance.HasEnough(GetCostForBuildType(buildType));
    }

    private string GetCostText(BuildType buildType)
    {
        if (ResourceInventory.Instance == null)
            return "Неизвестно";

        return ResourceInventory.Instance.GetCostText(GetCostForBuildType(buildType));
    }

    private void ShowNotEnoughResourcesMessage(BuildType buildType)
    {
        UpdateBuildModeUI(
            true,
            $"Недостаточно ресурсов для: {GetBuildDisplayName(buildType)}\n" +
            $"Нужно: {GetCostText(buildType)}\n" +
            "Собери ресурсы поблизости"
        );
    }

    private GameObject GetPrefabForBuildType(BuildType buildType)
    {
        switch (buildType)
        {
            case BuildType.Campfire:
                return campfirePrefab;
            case BuildType.Workshop:
                return workshopPrefab;
            default:
                return null;
        }
    }

    private ResourceCost[] GetCostForBuildType(BuildType buildType)
    {
        switch (buildType)
        {
            case BuildType.Campfire:
                return campfireCost;
            case BuildType.Workshop:
                return workshopCost;
            default:
                return null;
        }
    }

    private string GetBuildDisplayName(BuildType buildType)
    {
        switch (buildType)
        {
            case BuildType.Campfire:
                return "Костёр";
            case BuildType.Workshop:
                return "Мастерская";
            default:
                return "Неизвестно";
        }
    }

    private void EnsurePreviewExistsForSelectedBuild()
    {
        GameObject selectedPrefab = GetPrefabForBuildType(selectedBuildType);
        if (selectedPrefab == null)
            return;

        if (previewInstance != null && previewSourcePrefab == selectedPrefab)
            return;

        if (previewInstance != null)
            Destroy(previewInstance);

        previewSourcePrefab = selectedPrefab;
        previewInstance = Instantiate(selectedPrefab);
        previewInstance.name = selectedPrefab.name + "_Preview";

        PreparePreviewObject(previewInstance);
        CachePreviewMaterials();
        SetPreviewVisible(false);
    }

    private void PreparePreviewObject(GameObject previewObject)
    {
        SetLayerRecursively(previewObject, LayerMask.NameToLayer("Ignore Raycast"));

        Collider[] colliders = previewObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
            col.enabled = false;

        Rigidbody[] rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
            Destroy(rb);

        Camp camp = previewObject.GetComponent<Camp>();
        if (camp != null)
            Destroy(camp);

        Workshop workshop = previewObject.GetComponent<Workshop>();
        if (workshop != null)
            Destroy(workshop);
    }

    private void CachePreviewMaterials()
    {
        previewMaterials.Clear();

        if (previewInstance == null)
            return;

        Renderer[] renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            Material[] mats = rend.materials;
            foreach (Material mat in mats)
            {
                if (mat != null)
                    previewMaterials.Add(mat);
            }
        }
    }

    private void SetPreviewColor(Color color)
    {
        for (int i = 0; i < previewMaterials.Count; i++)
        {
            Material mat = previewMaterials[i];
            if (mat == null)
                continue;

            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.2f);
            }
        }
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewInstance != null)
            previewInstance.SetActive(visible);
    }

    private void UpdateBuildModeUI(bool visible, string message)
    {
        if (buildModePanel != null)
            buildModePanel.SetActive(visible);

        if (buildModeText != null)
            buildModeText.text = message;
    }

    private bool PointerIsOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null || layer < 0)
            return;

        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void EnsureRadiusVisualsExist()
    {
        if (playerRadiusRenderer == null)
        {
            playerRadiusVisual = new GameObject("PlayerBuildRadius");
            playerRadiusVisual.transform.SetParent(transform);
            playerRadiusRenderer = playerRadiusVisual.AddComponent<LineRenderer>();
            SetupRadiusRenderer(playerRadiusRenderer, playerRadiusColor);
        }

        if (campRadiusRenderer == null)
        {
            campRadiusVisual = new GameObject("CampBuildRadius");
            campRadiusVisual.transform.SetParent(transform);
            campRadiusRenderer = campRadiusVisual.AddComponent<LineRenderer>();
            SetupRadiusRenderer(campRadiusRenderer, campRadiusColor);
        }
    }

    private void SetupRadiusRenderer(LineRenderer lr, Color color)
    {
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = radiusSegments;
        lr.widthMultiplier = radiusLineWidth;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        lr.material = mat;
        lr.startColor = color;
        lr.endColor = color;
    }

    private void UpdateRadiusVisuals()
    {
        if (!showWorldRadii || !isPlacementMode)
        {
            SetRadiusVisualsVisible(false, false);
            return;
        }

        bool showPlayer = player != null;
        bool showCamp = SelectedBuildRequiresCamp() &&
                        CampManager.Instance != null &&
                        CampManager.Instance.HasCamp();

        SetRadiusVisualsVisible(showPlayer, showCamp);

        if (showPlayer)
            DrawCircle(playerRadiusRenderer, player.position, placementRadius);

        if (showCamp)
            DrawCircle(campRadiusRenderer, CampManager.Instance.CurrentCamp.WaitingCenter, campBuildRadius);
    }

    private void DrawCircle(LineRenderer lr, Vector3 center, float radius)
    {
        if (lr == null)
            return;

        if (radiusSegments < 3)
            radiusSegments = 3;

        if (lr.positionCount != radiusSegments)
            lr.positionCount = radiusSegments;

        Vector3 drawCenter = center + Vector3.up * radiusYOffset;

        float step = 2f * Mathf.PI / radiusSegments;
        for (int i = 0; i < radiusSegments; i++)
        {
            float angle = i * step;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lr.SetPosition(i, drawCenter + new Vector3(x, 0f, z));
        }
    }

    private void SetRadiusVisualsVisible(bool showPlayer, bool showCamp)
    {
        if (playerRadiusVisual != null)
            playerRadiusVisual.SetActive(showPlayer);

        if (campRadiusVisual != null)
            campRadiusVisual.SetActive(showCamp);
    }

    private void OnDisable()
    {
        CancelPlacement();
    }

    private void OnDestroy()
    {
        if (playerRadiusRenderer != null && playerRadiusRenderer.material != null)
            Destroy(playerRadiusRenderer.material);

        if (campRadiusRenderer != null && campRadiusRenderer.material != null)
            Destroy(campRadiusRenderer.material);
    }

    private void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 playerCenter = player.position + Vector3.up * 0.1f;
            Gizmos.DrawWireSphere(playerCenter, placementRadius);
        }

        if (CampManager.Instance != null && CampManager.Instance.HasCamp())
        {
            Gizmos.color = Color.cyan;
            Vector3 campCenter = CampManager.Instance.CurrentCamp.WaitingCenter + Vector3.up * 0.15f;
            Gizmos.DrawWireSphere(campCenter, campBuildRadius);
        }
    }
}