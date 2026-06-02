using UnityEngine;

/// <summary>
/// Attach this component to any GameObject you want to appear visible
/// but ghostly / ethereal — pale, semi-transparent, with a slight blue tint.
///
/// Works by modifying the materials on every Renderer in the hierarchy.
/// The original materials are restored if you call Restore() or disable the component.
///
/// Setup:
///   - Add the component to the root of the object you want to ghost.
///   - Tune "Ghost Color", "Ghost Alpha", and "Shimmer Speed" in the Inspector.
///   - Check "Apply On Start" to ghost immediately, or call SetGhost(true) from code.
///
/// NOTE: The GameObjects must use a shader that supports the _Color / _BaseColor
///       property and transparency (e.g. URP/Lit with Surface Type = Transparent,
///       or the legacy Standard shader with Fade / Transparent mode).
///       A simple workaround for opaque shaders is included: the script will
///       attempt to switch the material to Transparent mode automatically.
/// </summary>
public class GhostObject : MonoBehaviour
{
    [Header("Ghost Settings")]
    [Tooltip("The tint applied to every renderer while in ghost mode. Pale blue-white works well.")]
    [SerializeField] private Color ghostColor = new Color(0.75f, 0.88f, 1f, 1f);

    [Range(0f, 1f)]
    [Tooltip("Overall alpha (transparency) while in ghost mode.")]
    [SerializeField] private float ghostAlpha = 0.35f;

    [Tooltip("Slow pulsing shimmer speed (set to 0 to disable).")]
    [SerializeField] private float shimmerSpeed = 1.2f;

    [Tooltip("How much the alpha pulses (+/- around ghostAlpha).")]
    [Range(0f, 0.4f)]
    [SerializeField] private float shimmerAmount = 0.1f;

    [Tooltip("If true, the ghost effect is applied as soon as the scene starts.")]
    [SerializeField] private bool applyOnStart = true;

    [Tooltip("If true, the object's renderers are completely disabled (invisible) when not in ghost mode.")]
    [SerializeField] private bool hideWhenNotGhosted = true;

    // ---------------------------------------------------------------

    private Renderer[]  _renderers;
    private Collider[]  _colliders;
    private MonoBehaviour[] _interactables;

    private Material[]  _originalMaterials;  // shared reference copies
    private Material[]  _ghostMaterials;     // instanced copies we can safely modify
    private bool        _isGhosted = false;

    // Standard / URP shader property names
    private static readonly int PropColor      = Shader.PropertyToID("_Color");
    private static readonly int PropBaseColor  = Shader.PropertyToID("_BaseColor");
    private static readonly int PropSurface    = Shader.PropertyToID("_Surface");
    private static readonly int PropSrcBlend   = Shader.PropertyToID("_SrcBlend");
    private static readonly int PropDstBlend   = Shader.PropertyToID("_DstBlend");
    private static readonly int PropZWrite     = Shader.PropertyToID("_ZWrite");
    private static readonly int PropAlpha      = Shader.PropertyToID("_Alpha");

    // ---------------------------------------------------------------

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _colliders = GetComponentsInChildren<Collider>(includeInactive: true);

        // Find any IInteractable scripts so we can disable them too
        var monos = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        var interactablesList = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (var m in monos)
        {
            if (m is IInteractable)
                interactablesList.Add(m);
        }
        _interactables = interactablesList.ToArray();

        CacheAndCloneMaterials();

        // Hide immediately to prevent 1-frame flash if it's supposed to start hidden
        if (!applyOnStart && hideWhenNotGhosted)
        {
            SetObjectActiveState(false);
        }
    }

    private void Start()
    {
        if (applyOnStart)
            SetGhost(true);
    }

    private void Update()
    {
        if (!_isGhosted || shimmerSpeed <= 0f || _ghostMaterials == null)
            return;

        float pulse = ghostAlpha + Mathf.Sin(Time.time * shimmerSpeed * Mathf.PI * 2f) * shimmerAmount;
        pulse = Mathf.Clamp01(pulse);

        Color c = ghostColor;
        c.a = pulse;
        ApplyColorToMaterials(c);
    }

    private void OnDisable()
    {
        Restore();
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>Toggle the ghost appearance on or off.</summary>
    public void SetGhost(bool ghost)
    {
        if (ghost)
        {
            SetObjectActiveState(true);
            ApplyGhost();
        }
        else
        {
            Restore();
            if (hideWhenNotGhosted)
                SetObjectActiveState(false);
        }
    }

    // ---------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------

    private void SetObjectActiveState(bool isEnabled)
    {
        if (_renderers != null)
        {
            foreach (var r in _renderers)
                if (r != null) r.enabled = isEnabled;
        }

        if (_colliders != null)
        {
            foreach (var c in _colliders)
                if (c != null) c.enabled = isEnabled;
        }

        if (_interactables != null)
        {
            foreach (var i in _interactables)
                if (i != null) i.enabled = isEnabled;
        }
    }

    private void CacheAndCloneMaterials()
    {
        // Count total materials across all renderers
        int total = 0;
        foreach (var r in _renderers)
            total += r.sharedMaterials.Length;

        _originalMaterials = new Material[total];
        _ghostMaterials    = new Material[total];

        int idx = 0;
        foreach (var r in _renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                _originalMaterials[idx] = mat;
                _ghostMaterials[idx]    = new Material(mat); // instance copy
                idx++;
            }
        }
    }

    private void ApplyGhost()
    {
        if (_ghostMaterials == null) return;
        _isGhosted = true;

        // Enable transparency on every instanced material
        foreach (var mat in _ghostMaterials)
            EnableTransparency(mat);

        // Assign ghost materials to renderers
        AssignMaterials(_ghostMaterials);

        // Set initial ghost color
        Color c = ghostColor;
        c.a = ghostAlpha;
        ApplyColorToMaterials(c);
    }

    private void Restore()
    {
        _isGhosted = false;
        if (_originalMaterials == null) return;
        AssignMaterials(_originalMaterials);
    }

    private void AssignMaterials(Material[] pool)
    {
        int idx = 0;
        foreach (var r in _renderers)
        {
            int count = r.sharedMaterials.Length;
            Material[] slice = new Material[count];
            for (int i = 0; i < count; i++)
                slice[i] = pool[idx++];
            r.materials = slice; // sets instanced copies
        }
    }

    private void ApplyColorToMaterials(Color c)
    {
        foreach (var mat in _ghostMaterials)
        {
            if (mat.HasProperty(PropColor))     mat.SetColor(PropColor,     c);
            if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, c);
        }
    }

    /// <summary>
    /// Attempt to switch a material to transparent/fade rendering
    /// for both Standard and URP/Lit shaders.
    /// </summary>
    private static void EnableTransparency(Material mat)
    {
        // URP Lit — Surface Type 1 = Transparent
        if (mat.HasProperty(PropSurface))
        {
            mat.SetFloat(PropSurface, 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt(PropSrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt(PropDstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt(PropZWrite, 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            // Standard shader — Fade mode
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt(PropSrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt(PropDstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt(PropZWrite, 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
