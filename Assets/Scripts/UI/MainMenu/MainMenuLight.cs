using UnityEngine;

/// <summary>
/// Controls a child Light's intensity and colour based on the parent mesh's
/// hover/selection state.  Attach this to the same GameObject as the Light component.
/// </summary>
[RequireComponent(typeof(Light))]
public class MainMenuLight : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Intensity Levels")]
    [Tooltip("Light intensity when the mesh is at a side/back position.")]
    [SerializeField] private float idleIntensity = 0.6f;

    [Tooltip("Light intensity when this mesh is the front (active) one but not hovered.")]
    [SerializeField] private float frontIdleIntensity = 2.0f;

    [Tooltip("Light intensity while the mouse is hovering over the front mesh.")]
    [SerializeField] private float hoverIntensity = 4.5f;

    [Tooltip("Light intensity burst when the player selects this option.")]
    [SerializeField] private float selectedIntensity = 12f;

    [Header("Lerp Speed")]
    [SerializeField] private float lerpSpeed = 6f;

    [Header("Colours")]
    [SerializeField] private Color idleColour    = new Color(0.55f, 0.65f, 1.00f); // cool blue-white
    [SerializeField] private Color hoverColour   = new Color(1.00f, 0.88f, 0.55f); // warm amber
    [SerializeField] private Color selectedColour = new Color(1.00f, 1.00f, 1.00f); // pure white

    // ─────────────────────────────────────────────────────────────────────────
    //  Private
    // ─────────────────────────────────────────────────────────────────────────

    private Light _light;
    private float _targetIntensity;
    private Color  _targetColour;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _light = GetComponent<Light>();
        _targetIntensity = idleIntensity;
        _targetColour    = idleColour;
        _light.intensity = idleIntensity;
        _light.color     = idleColour;
    }

    private void Update()
    {
        _light.intensity = Mathf.Lerp(_light.intensity, _targetIntensity, lerpSpeed * Time.deltaTime);
        _light.color     = Color.Lerp(_light.color,     _targetColour,    lerpSpeed * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API — called by MainMenuMesh
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Side / back position — dim, cool.</summary>
    public void SetIdleState()
    {
        _targetIntensity = idleIntensity;
        _targetColour    = idleColour;
    }

    /// <summary>Front position, mouse not hovering.</summary>
    public void SetFrontIdleState()
    {
        _targetIntensity = frontIdleIntensity;
        _targetColour    = hoverColour;
    }

    /// <summary>Front position AND mouse is hovering — brighter warm glow.</summary>
    public void SetHoverState()
    {
        _targetIntensity = hoverIntensity;
        _targetColour    = hoverColour;
    }

    /// <summary>Selection confirmed — bright flash.</summary>
    public void SetSelectedState()
    {
        _targetIntensity = selectedIntensity;
        _targetColour    = selectedColour;
    }
}
