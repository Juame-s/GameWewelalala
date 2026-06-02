using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Attach to each cartridge mesh GameObject.
/// Handles: floating animation, twirl on mouse pass, front/side scale,
/// hover push-forward, selection slam animation, and the floating TMP label.
/// </summary>
public class MainMenuMesh : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Option
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Option")]
    [Tooltip("Display name for this menu option (Play / Options / Credits / Exit).")]
    public string optionLabel = "Play";

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Float Animation
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Mesh Orientation")]
    [Tooltip("Rotation offset applied on top of the carousel base rotation to correct " +
             "export-time tilts. \nCommon fixes:\n" +
             "  Blender lying flat → (-90, 0, 0)\n" +
             "  Maya / FBX lying flat → (90, 0, 0)\n" +
             "Adjust until your cartridge stands upright.")]
    [SerializeField] private Vector3 meshUprightOffset = new Vector3(-90f, 180f, 0f);

    [Header("Float Animation")]
    [SerializeField] private float floatAmplitude = 0.18f;
    [SerializeField] private float floatSpeed     = 1.1f;
    [SerializeField] private float tiltAmplitudeZ = 4.0f;   // degrees
    [SerializeField] private float tiltAmplitudeX = 2.0f;   // degrees
    [SerializeField] private float tiltSpeed      = 0.9f;

    /// <summary>Set by the controller so meshes don't all bob in sync.</summary>
    [HideInInspector] public float phaseOffset = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Twirl
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Twirl (mouse-pass spin)")]
    [Tooltip("How strongly horizontal mouse movement spins the cartridge.")]
    [SerializeField] private float twirlSensitivity = 5.0f;
    [Tooltip("How quickly the spin decays back to 0.")]
    [SerializeField] private float twirlDamping     = 5.0f;
    [SerializeField] private float maxTwirlVelocity = 2000f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Scale
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Scale Multipliers")]
    [Tooltip("Multipliers applied to the original scale of the mesh.")]
    [SerializeField] private float frontScaleMultiplier  = 1.20f;
    [SerializeField] private float sideScaleMultiplier   = 0.80f;
    [SerializeField] private float scaleLerp   = 6f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Position
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Position")]
    [SerializeField] private float positionLerp = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Hover Push
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Hover Push (front mesh only)")]
    [Tooltip("How far (world units) the front cartridge nudges toward the camera when hovered.")]
    [SerializeField] private float hoverPushAmount = 0.40f;
    [SerializeField] private float hoverPushLerp   = 7f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Selection Animation
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Selection Animation")]
    [SerializeField] private float riseHeight    = 2.2f;
    [SerializeField] private float riseDuration  = 0.35f;
    [SerializeField] private float holdDuration  = 0.12f;
    [SerializeField] private float slamDepth     = 4.0f;   // how far below anchor to sink
    [SerializeField] private float slamDuration  = 0.27f;
    [SerializeField] private float squishXZ      = 1.6f;   // lateral squish at slam

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Label
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Floating Label")]
    [Tooltip("Assign a child TextMeshPro (3D) object positioned above the cartridge.")]
    [SerializeField] private TextMeshPro labelTMP;
    [SerializeField] private float labelFrontFontSize = 3.5f;
    [SerializeField] private float labelSideFontSize  = 2.0f;
    [SerializeField] private Color labelFrontColour   = Color.white;
    [SerializeField] private Color labelSideColour    = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private float labelLerp          = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Light
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Light")]
    [Tooltip("Assign the MainMenuLight component from the child Light object.")]
    [SerializeField] public MainMenuLight menuLight;

    // ─────────────────────────────────────────────────────────────────────────
    //  Private state
    // ─────────────────────────────────────────────────────────────────────────

    private bool _isFront;
    private bool _isHovered;
    private bool _isAnimating;

    private Vector3    _anchorPosition;        // Target world position set by controller
    private Quaternion _baseRotation;          // Base facing direction set by controller
    private float      _twirlVelocity;         // degrees / second
    private float      _twirlAccumAngle;       // accumulated twirl Y angle
    private Vector3    _previousMousePos;
    private Vector3    _baseScale;
    private Vector3    _targetScale;

    // ─────────────────────────────────────────────────────────────────────────
    //  Properties
    // ─────────────────────────────────────────────────────────────────────────

    public bool IsFront     => _isFront;
    public bool IsHovered   => _isHovered;
    public bool IsAnimating => _isAnimating;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _baseRotation   = transform.rotation;
        _baseScale      = transform.localScale;
        _targetScale    = _baseScale;
        _anchorPosition = transform.position;

        // ── Auto-collider ────────────────────────────────────────────────────
        // The mouse raycast needs a Collider to hit.  If the imported cartridge
        // mesh has no collider, add a BoxCollider sized to its mesh bounds so
        // hover and click work out of the box.
        if (GetComponentInChildren<Collider>() == null)
        {
            MeshFilter mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                Debug.Log($"[MainMenuMesh] Auto-added MeshCollider to child '{mf.name}' for perfect mouse detection.");
            }
            else
            {
                gameObject.AddComponent<BoxCollider>();
                Debug.Log($"[MainMenuMesh] Auto-added fallback BoxCollider to '{name}' for mouse detection. " +
                           "Resize it in the Inspector if it doesn't fit well.");
            }
        }
    }

    private void Start()
    {
        _previousMousePos = Input.mousePosition;

        if (labelTMP != null)
        {
            labelTMP.text     = optionLabel;
            labelTMP.fontSize = labelSideFontSize;
            labelTMP.color    = labelSideColour;
        }
    }

    private void Update()
    {
        if (_isAnimating) return;

        HandleTwirl();
        HandleFloatAndPosition();
        HandleScale();
        HandleLabel();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Update helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleTwirl()
    {
        Vector3 mouseDelta = Input.mousePosition - _previousMousePos;
        _previousMousePos = Input.mousePosition;

        // Only accumulate twirl when the mouse actually moves over this mesh
        if (_isHovered && mouseDelta.magnitude > 1.5f)
        {
            _twirlVelocity += mouseDelta.x * twirlSensitivity;
            _twirlVelocity  = Mathf.Clamp(_twirlVelocity, -maxTwirlVelocity, maxTwirlVelocity);
        }

        // Damp spin toward 0
        _twirlVelocity   = Mathf.Lerp(_twirlVelocity, 0f, twirlDamping * Time.deltaTime);
        _twirlAccumAngle += _twirlVelocity * Time.deltaTime;
        
        // Slowly spring back to 0 so it doesn't stay facing backward
        _twirlAccumAngle = Mathf.Lerp(_twirlAccumAngle, 0f, (twirlDamping * 0.3f) * Time.deltaTime);
    }

    private void HandleFloatAndPosition()
    {
        float t = Time.time;

        // Sinusoidal Y bob
        float floatY = Mathf.Sin(t * floatSpeed + phaseOffset) * floatAmplitude;

        // Gentle tilt (X and Z axes) — purely cosmetic wobble
        float tiltZ = Mathf.Sin(t * tiltSpeed       + phaseOffset + 1.3f) * tiltAmplitudeZ;
        float tiltX = Mathf.Cos(t * tiltSpeed * 0.7f + phaseOffset + 0.5f) * tiltAmplitudeX;

        // Hover push: when front AND hovered, nudge slightly toward the camera
        Vector3 hoverOffset = Vector3.zero;
        if (_isFront && _isHovered && Camera.main != null)
        {
            Vector3 toCamera = (Camera.main.transform.position - _anchorPosition).normalized;
            hoverOffset = toCamera * hoverPushAmount;
        }

        // Smooth position
        Vector3 targetPos = _anchorPosition + Vector3.up * floatY + hoverOffset;
        transform.position = Vector3.Lerp(transform.position, targetPos, positionLerp * Time.deltaTime);

        // Rotation: upright correction is applied FIRST to the mesh, THEN the float/twirl
        // is applied on the corrected visual axes, THEN the base carousel rotation.
        Quaternion uprightFix  = Quaternion.Euler(meshUprightOffset);
        Quaternion floatTilt   = Quaternion.Euler(tiltX, _twirlAccumAngle, tiltZ);
        Quaternion targetRot   = _baseRotation * floatTilt * uprightFix;
        transform.rotation     = Quaternion.Lerp(transform.rotation, targetRot, 10f * Time.deltaTime);
    }

    private void HandleScale()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, scaleLerp * Time.deltaTime);
    }

    private void HandleLabel()
    {
        if (labelTMP == null) return;

        // Always face the camera (billboard)
        if (Camera.main != null)
        {
            labelTMP.transform.LookAt(Camera.main.transform.position);
            labelTMP.transform.Rotate(0f, 180f, 0f);
        }

        // Size and colour transition based on front/side state
        float targetSize   = _isFront ? labelFrontFontSize : labelSideFontSize;
        Color targetColour = _isFront ? labelFrontColour   : labelSideColour;

        labelTMP.fontSize = Mathf.Lerp(labelTMP.fontSize, targetSize, labelLerp * Time.deltaTime);
        labelTMP.color    = Color.Lerp(labelTMP.color,    targetColour, labelLerp * Time.deltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API — called by MainMenuController
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the display label for this mesh.  Called by the controller at startup
    /// so every cartridge shows the correct option name without needing to edit
    /// each mesh's Inspector field individually.
    /// </summary>
    public void SetLabel(string label)
    {
        optionLabel = label;
        if (labelTMP != null) labelTMP.text = label;
    }

    /// <summary>Tell the mesh where it should float toward (world space).</summary>
    public void SetAnchor(Vector3 position, Quaternion baseRotation)
    {
        _anchorPosition = position;
        _baseRotation   = baseRotation;
    }

    /// <summary>Mark this mesh as the front (active) or a side/back one.</summary>
    public void SetFront(bool isFront)
    {
        _isFront     = isFront;
        _targetScale = isFront ? _baseScale * frontScaleMultiplier : _baseScale * sideScaleMultiplier;

        if (menuLight == null) return;
        if (isFront) menuLight.SetFrontIdleState();
        else         menuLight.SetIdleState();
    }

    /// <summary>Called when the mouse enters / exits this mesh's collider (via raycast).</summary>
    public void SetHovered(bool hovered)
    {
        if (_isAnimating) return;
        _isHovered = hovered;

        if (menuLight == null || !_isFront) return;
        if (hovered) menuLight.SetHoverState();
        else         menuLight.SetFrontIdleState();
    }

    /// <summary>
    /// Play the rise → slam selection animation, then invoke <paramref name="onComplete"/>.
    /// </summary>
    public void PlaySelectAnimation(System.Action onComplete)
    {
        if (!_isAnimating)
            StartCoroutine(SelectCoroutine(onComplete));
    }

    /// <summary>Snap back to a valid state (e.g. returning from Options screen).</summary>
    public void ResetFromAnimation()
    {
        StopAllCoroutines();
        _isAnimating         = false;
        transform.position   = _anchorPosition;
        transform.localScale = _isFront ? _baseScale * frontScaleMultiplier : _baseScale * sideScaleMultiplier;
        gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Selection Coroutine
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator SelectCoroutine(System.Action onComplete)
    {
        _isAnimating = true;

        if (menuLight != null)
            menuLight.SetSelectedState();

        // ── Phase 1: Rise ───────────────────────────────────────────────────
        Vector3 startPos   = transform.position;
        Vector3 riseTarget = startPos + Vector3.up * riseHeight;
        Vector3 startScale = transform.localScale;
        
        Quaternion startRot = transform.rotation;
        Quaternion frontRot = _baseRotation * Quaternion.Euler(meshUprightOffset);

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.MoveTowards(t, 1f, Time.deltaTime / riseDuration);
            float ease = Mathf.SmoothStep(0f, 1f, t);
            transform.position   = Vector3.Lerp(startPos, riseTarget, ease);
            transform.localScale = Vector3.Lerp(startScale, startScale * 1.15f, ease);
            transform.rotation   = Quaternion.Lerp(startRot, frontRot, ease);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        // ── Phase 2: Slam ───────────────────────────────────────────────────
        Vector3 slamStart  = transform.position;
        Vector3 slamTarget = _anchorPosition + Vector3.down * slamDepth;
        Vector3 preScale   = transform.localScale;
        Vector3 squishTarget = new Vector3(preScale.x * squishXZ, preScale.y * 0.12f, preScale.z * squishXZ);

        t = 0f;
        while (t < 1f)
        {
            t = Mathf.MoveTowards(t, 1f, Time.deltaTime / slamDuration);
            float ease = t * t * t;   // Cubic ease-in — accelerates into the ground
            transform.position   = Vector3.Lerp(slamStart, slamTarget, ease);
            transform.localScale = Vector3.Lerp(preScale, squishTarget, ease);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        onComplete?.Invoke();
    }
}
