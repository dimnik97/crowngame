using UnityEngine;

public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private Health targetHealth;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(56f, 7f);
    [SerializeField] private Color fillColor = new Color(0.25f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Color borderColor = Color.black;

    private static Texture2D fillTexture;
    private static Texture2D backgroundTexture;
    private static Texture2D borderTexture;

    public void Configure(Health health, Vector3 offset)
    {
        targetHealth = health;
        worldOffset = offset;
    }

    public void Configure(Health health, Vector3 offset, Color newFillColor, Color newBackgroundColor, Color newBorderColor)
    {
        Configure(health, offset);
        fillColor = newFillColor;
        backgroundColor = newBackgroundColor;
        borderColor = newBorderColor;
    }

    private void Awake()
    {
        if (targetHealth == null)
            targetHealth = GetComponent<Health>();

        EnsureTextures();
    }

    private void OnGUI()
    {
        if (targetHealth == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 screenPoint = cam.WorldToScreenPoint(transform.position + worldOffset);
        if (screenPoint.z <= 0f)
            return;

        float normalized = Mathf.Clamp01((float)targetHealth.CurrentHealth / targetHealth.MaxHealth);
        float x = screenPoint.x - barSize.x * 0.5f;
        float y = Screen.height - screenPoint.y - barSize.y - 4f;

        Rect borderRect = new Rect(x - 1f, y - 1f, barSize.x + 2f, barSize.y + 2f);
        Rect backgroundRect = new Rect(x, y, barSize.x, barSize.y);
        Rect fillRect = new Rect(x, y, barSize.x * normalized, barSize.y);

        Color previousColor = GUI.color;

        GUI.color = borderColor;
        GUI.DrawTexture(borderRect, borderTexture);

        GUI.color = backgroundColor;
        GUI.DrawTexture(backgroundRect, backgroundTexture);

        GUI.color = fillColor;
        GUI.DrawTexture(fillRect, fillTexture);

        GUI.color = previousColor;
    }

    private static void EnsureTextures()
    {
        if (fillTexture == null)
            fillTexture = CreateTexture();

        if (backgroundTexture == null)
            backgroundTexture = CreateTexture();

        if (borderTexture == null)
            borderTexture = CreateTexture();
    }

    private static Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return texture;
    }
}
