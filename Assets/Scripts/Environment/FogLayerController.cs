using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates a layered, hard-banded fog effect by placing large transparent quad planes
/// at fixed distances from the camera. Each layer steps up in opacity, producing the
/// distinct "depth layers" look visible in a blizzard / whiteout environment.
///
/// Attach this to the Camera GameObject. Assign the FogLayer material in the Inspector.
/// </summary>
[ExecuteAlways]
public class FogLayerController : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("Assign the FogLayer URP Unlit Transparent material from Assets/Materials/")]
    public Material fogBaseMaterial;

    [Header("Fog Colour")]
    [Tooltip("The tint colour of every fog layer. Keep high brightness for a blizzard look.")]
    public Color fogColor = new Color(0.88f, 0.91f, 0.94f, 1f);

    [Header("Layer Settings")]
    [Tooltip("Number of fog planes to spawn.")]
    [Range(2, 12)]
    public int layerCount = 6;

    [Tooltip("Distance (m) of the nearest fog layer from the camera.")]
    [Range(1f, 50f)]
    public float nearDistance = 8f;

    [Tooltip("Distance (m) between successive fog layers.")]
    [Range(1f, 60f)]
    public float layerSpacing = 12f;

    [Tooltip("World-size (width & height, in metres) of every fog quad. Make it very large so it covers the whole viewport.")]
    [Range(50f, 1000f)]
    public float layerSize = 500f;

    [Header("Alpha (Opacity) Settings")]
    [Tooltip("Opacity of the closest fog layer (0 = invisible, 1 = solid).")]
    [Range(0f, 1f)]
    public float nearAlpha = 0.05f;

    [Tooltip("Opacity of the farthest fog layer.")]
    [Range(0f, 1f)]
    public float farAlpha = 0.95f;

    [Tooltip("Controls how opacity ramps between near and far layers. A value > 1 means opacity builds up quickly at the far end.")]
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Billboard Axis Lock")]
    [Tooltip("When true the planes only rotate around the Y axis (horizontal fog walls). " +
             "When false they fully face the camera (sphere-like wrap).")]
    public bool lockVerticalBillboard = false;

    // ─── Runtime ─────────────────────────────────────────────────────────────

    private List<GameObject>  _planes    = new List<GameObject>();
    private List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private List<Material>     _matInstances = new List<Material>();

    private int   _cachedLayerCount;
    private float _cachedSize;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────

    void OnEnable()
    {
        BuildPlanes();
    }

    void OnDisable()
    {
        DestroyPlanes();
    }

    void Update()
    {
        // Rebuild if key settings changed in the Inspector during Play / Edit mode
        if (_cachedLayerCount != layerCount || !Mathf.Approximately(_cachedSize, layerSize))
        {
            BuildPlanes();
        }

        UpdatePlanes();
    }

    // ─── Plane Construction ───────────────────────────────────────────────────

    void BuildPlanes()
    {
        DestroyPlanes();

        _cachedLayerCount = layerCount;
        _cachedSize       = layerSize;

        for (int i = 0; i < layerCount; i++)
        {
            // Create GameObject
            var go = new GameObject($"FogLayer_{i}");
            go.hideFlags = HideFlags.HideAndDontSave; // invisible in Hierarchy clutter
            go.transform.SetParent(null);             // world-space so it's not affected by camera transform

            // Add mesh
            var mf   = go.AddComponent<MeshFilter>();
            var mr   = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildQuadMesh(layerSize);

            // Build a per-layer material instance
            Material mat = (fogBaseMaterial != null)
                ? new Material(fogBaseMaterial)
                : CreateFallbackMaterial();

            // Set colour with per-layer alpha
            float t     = (layerCount > 1) ? (float)i / (layerCount - 1) : 1f;
            float alpha = Mathf.Lerp(nearAlpha, farAlpha, alphaCurve.Evaluate(t));
            Color c     = fogColor;
            c.a         = alpha;

            mat.color = c;
            // URP Unlit / Lit transparent needs these set explicitly
            SetMaterialTransparent(mat);

            mr.sharedMaterial = mat;
            mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows     = false;

            _planes.Add(go);
            _renderers.Add(mr);
            _matInstances.Add(mat);
        }
    }

    void DestroyPlanes()
    {
        foreach (var go in _planes)
        {
            if (go != null)
            {
                if (Application.isPlaying) Destroy(go);
                else                       DestroyImmediate(go);
            }
        }
        foreach (var mat in _matInstances)
        {
            if (mat != null)
            {
                if (Application.isPlaying) Destroy(mat);
                else                       DestroyImmediate(mat);
            }
        }
        _planes.Clear();
        _renderers.Clear();
        _matInstances.Clear();
    }

    // ─── Per-Frame Update ─────────────────────────────────────────────────────

    void UpdatePlanes()
    {
        if (_planes.Count != layerCount)
        {
            BuildPlanes();
            return;
        }

        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos     = cam.transform.position;
        Vector3 camForward = cam.transform.forward;

        for (int i = 0; i < _planes.Count; i++)
        {
            if (_planes[i] == null) continue;

            float dist = nearDistance + i * layerSpacing;

            // Position plane in front of camera along its forward axis
            Vector3 planePos = camPos + camForward * dist;
            _planes[i].transform.position = planePos;

            // Billboard: face the camera
            if (lockVerticalBillboard)
            {
                // Only rotate around Y — flat vertical fog walls
                Vector3 flat = camForward;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.001f)
                    _planes[i].transform.rotation = Quaternion.LookRotation(flat);
            }
            else
            {
                // Fully face camera (sphere billboard)
                _planes[i].transform.rotation = Quaternion.LookRotation(camForward);
            }

            // Keep material colour/alpha in sync with Inspector tweaks
            float t     = (layerCount > 1) ? (float)i / (layerCount - 1) : 1f;
            float alpha = Mathf.Lerp(nearAlpha, farAlpha, alphaCurve.Evaluate(t));
            Color c     = fogColor;
            c.a         = alpha;

            if (_matInstances[i] != null)
                _matInstances[i].color = c;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a simple flat quad mesh of the given world size.</summary>
    static Mesh BuildQuadMesh(float size)
    {
        float h = size * 0.5f;
        var mesh = new Mesh { name = "FogQuad" };

        mesh.vertices = new Vector3[]
        {
            new Vector3(-h, -h, 0),
            new Vector3( h, -h, 0),
            new Vector3( h,  h, 0),
            new Vector3(-h,  h, 0),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>
    /// Configures all the URP Lit/Unlit keywords and blend settings needed
    /// for proper alpha-transparent rendering. Works whether the base material
    /// is URP/Lit or URP/Unlit.
    /// </summary>
    static void SetMaterialTransparent(Material mat)
    {
        // Surface type = Transparent (1)
        if (mat.HasProperty("_Surface"))        mat.SetFloat("_Surface", 1f);
        // Blend mode = Alpha (0)
        if (mat.HasProperty("_Blend"))          mat.SetFloat("_Blend", 0f);
        // Disable Z-write so layers don't occlude each other
        if (mat.HasProperty("_ZWrite"))         mat.SetFloat("_ZWrite", 0f);
        mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        // Enable alpha-transparent render queue and keywords
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");

        // Cull off — visible from both sides so the camera inside a fog layer still sees it
        if (mat.HasProperty("_Cull"))           mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
    }

    /// <summary>
    /// Fallback: creates a bare-bones URP Unlit transparent material at runtime
    /// if no fogBaseMaterial was assigned in the Inspector.
    /// </summary>
    static Material CreateFallbackMaterial()
    {
        // Try URP Unlit first, then legacy fallback
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader);
        mat.name = "FogLayer_Fallback";
        return mat;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Rebuild immediately when Inspector values change in Edit mode
        if (!Application.isPlaying && _planes.Count > 0)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) BuildPlanes();
            };
        }
    }
#endif
}
