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

    private bool isPlacementMode;
    private BuildType selectedBuildType = BuildType.None;

    private GameObject previewInstance;
    private readonly List<Material> previewMaterials = new List<Material>();
    private GameObject previewSourcePrefab;

    private Vector3 currentPreviewPoint;
    private bool hasPreviewPoint;
    private bool canPlaceHere;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
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

        if (!CanAfford(buildType))
        {
            ShowNotEnoughResourcesMessage(buildType);
            Debug.Log($"Недостаточно ресурсов для постройки: {GetBuildDisplayName(buildType)}");
            return;
        }

        selectedBuildType = buildType;
        isPlacementMode = true;

        EnsurePreviewExistsForSelectedBuild();
        SetPreviewVisible(true);

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
        UpdateBuildModeUI(false, string.Empty);
    }

    private void TryPlaceSelectedBuild()
    {
        if (selectedBuildType == BuildType.None)
            return;

        if (!hasPreviewPoint)
            return;

        if (!canPlaceHere)
        {
            UpdateBuildModeUI(
                true,
                "Слишком далеко от игрока\n" +
                $"Стоимость: {GetCostText(selectedBuildType)}\n" +
                "Подойди ближе\n" +
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
        canPlaceHere = IsInsidePlacementRadius(point);

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
                "Нельзя поставить: слишком далеко\n" +
                $"Стоимость: {GetCostText(selectedBuildType)}\n" +
                "Подойди ближе к точке\n" +
                "ПКМ / Esc — отмена"
            );
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
            case BuildType.Campfire: return campfirePrefab;
            case BuildType.Workshop: return workshopPrefab;
            default: return null;
        }
    }

    private ResourceCost[] GetCostForBuildType(BuildType buildType)
    {
        switch (buildType)
        {
            case BuildType.Campfire: return campfireCost;
            case BuildType.Workshop: return workshopCost;
            default: return null;
        }
    }

    private string GetBuildDisplayName(BuildType buildType)
    {
        switch (buildType)
        {
            case BuildType.Campfire: return "Костёр";
            case BuildType.Workshop: return "Мастерская";
            default: return "Неизвестно";
        }
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

    private void OnDisable()
    {
        CancelPlacement();
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null)
            return;

        Gizmos.color = Color.yellow;
        Vector3 center = player.position;
        center.y += 0.1f;

        Gizmos.DrawWireSphere(center, placementRadius);
    }
}