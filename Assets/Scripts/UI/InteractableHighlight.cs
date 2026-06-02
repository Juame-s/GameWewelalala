using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to any interactable object to give it a visual highlight
/// and screen-space text prompt when the player is near.
/// </summary>
public class InteractableHighlight : MonoBehaviour
{
    [Header("Prompt Settings")]
    [Tooltip("The text to show on screen when in range.")]
    public string promptText = "Press E to Interact";
    [Tooltip("The font to use for the prompt text. If null, uses TMP default.")]
    public TMP_FontAsset customFont;

    [Header("Highlight Settings")]
    [Tooltip("Renderers to highlight. If empty, tries to find one on this object.")]
    public Renderer[] targetRenderers;
    [Tooltip("Color of the outline.")]
    public Color outlineColor = Color.white;
    [Tooltip("Thickness of the outline.")]
    public float outlineWidth = 2.0f;

    private GameObject promptCanvasObj;
    private TextMeshProUGUI promptTMP;
    
    private Material outlineMaterialInstance;
    private bool isHighlighted = false;

    void Start()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) targetRenderers = new Renderer[] { r };
        }

        // Create the outline material at runtime using the custom shader
        Shader outlineShader = Shader.Find("Custom/URP_Outline");
        if (outlineShader != null)
        {
            outlineMaterialInstance = new Material(outlineShader);
            outlineMaterialInstance.SetColor("_OutlineColor", outlineColor);
            outlineMaterialInstance.SetFloat("_OutlineWidth", outlineWidth);
        }
        else
        {
            Debug.LogError("InteractableHighlight: Could not find 'Custom/URP_Outline' shader. Make sure it's created!");
        }

        CreatePromptUI();
    }

    void CreatePromptUI()
    {
        // Create a screen-space canvas for the prompt
        promptCanvasObj = new GameObject("InteractPromptCanvas");
        // Parent to this object so it gets destroyed if the collectible is destroyed, 
        // but ScreenSpaceOverlay ignores parent scaling/rotation.
        promptCanvasObj.transform.SetParent(transform);

        Canvas canvas = promptCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = promptCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Add text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(promptCanvasObj.transform, false);
        
        promptTMP = textObj.AddComponent<TextMeshProUGUI>();
        promptTMP.text = promptText;
        if (customFont != null)
        {
            promptTMP.font = customFont;
        }
        promptTMP.fontSize = 48;
        promptTMP.alignment = TextAlignmentOptions.Center;
        
        // Add outline to make it readable anywhere
        promptTMP.outlineWidth = 0.2f;
        promptTMP.outlineColor = Color.black;

        // Position at bottom center of the screen
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0f);
        textRt.anchorMax = new Vector2(0.5f, 0f);
        textRt.pivot = new Vector2(0.5f, 0f);
        textRt.sizeDelta = new Vector2(800, 100);
        textRt.anchoredPosition = new Vector2(0, 150); // 150 pixels from the bottom

        promptCanvasObj.SetActive(false);
    }

    public void SetHighlight(bool active)
    {
        if (isHighlighted == active) return;
        isHighlighted = active;

        // Toggle UI
        if (promptCanvasObj != null)
        {
            promptCanvasObj.SetActive(active);
        }

        // Toggle Outline Material
        if (targetRenderers != null && outlineMaterialInstance != null)
        {
            foreach (var r in targetRenderers)
            {
                if (r == null) continue;

                Material[] mats = r.sharedMaterials;
                
                if (active)
                {
                    // Append outline material to the end
                    Material[] newMats = new Material[mats.Length + 1];
                    for (int i = 0; i < mats.Length; i++) newMats[i] = mats[i];
                    newMats[mats.Length] = outlineMaterialInstance;
                    r.sharedMaterials = newMats;
                }
                else
                {
                    // Remove the outline material if it's the last one
                    if (mats.Length > 0 && mats[mats.Length - 1] == outlineMaterialInstance)
                    {
                        Material[] newMats = new Material[mats.Length - 1];
                        for (int i = 0; i < newMats.Length; i++) newMats[i] = mats[i];
                        r.sharedMaterials = newMats;
                    }
                }
            }
        }
    }

    void OnDisable()
    {
        SetHighlight(false);
    }
}
