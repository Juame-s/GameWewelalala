using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// The brain of the main menu.  Manages the 4-mesh carousel, keyboard/mouse input,
/// UI arrow buttons, and triggers the appropriate action when an option is selected.
///
/// Scene setup:
///   - Add this component to an empty "MainMenuController" GameObject.
///   - Create 4 empty "Anchor_*" GameObjects and position them in the Inspector.
///   - Drag the 4 cartridge meshes into the Meshes array (index order: Play, Options, Credits, Exit).
///   - Drag the 4 Anchor transforms into the Anchors array (index order: Front, Left, Right, Back).
///   - Wire the left/right Canvas Buttons.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Meshes & Anchors
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Mesh References  (index 0=Play, 1=Options, 2=Credits, 3=Exit)")]
    [SerializeField] private MainMenuMesh[] meshes = new MainMenuMesh[4];

    [Header("Option Labels  (must match mesh array order)")]
    [Tooltip("Labels are pushed to the meshes at runtime — you do NOT need to set optionLabel on each mesh.")]
    [SerializeField] private string[] optionLabels = { "Play", "Options", "Credits", "Exit" };

    [Header("Anchor Transforms  (index 0=Front, 1=Left, 2=Right, 3=Back)")]
    [Tooltip("Position empty GameObjects in the scene and drag them here. " +
             "Suggested: Front=(0,0,2), Left=(-3,0,-1), Right=(3,0,-1), Back=(0,0,-4)")]
    [SerializeField] private Transform[] anchors = new Transform[4];

    [Header("Base Rotations (Y axis, degrees) per slot")]
    [SerializeField] private float frontFacingY = 0f;
    [SerializeField] private float leftFacingY  = 30f;
    [SerializeField] private float rightFacingY = -30f;
    [SerializeField] private float backFacingY  = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Cycling
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Cycle Animation")]
    [SerializeField] private float cycleAnimDuration = 0.42f;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — UI
    // ─────────────────────────────────────────────────────────────────────────

    [Header("UI Arrow Buttons")]
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;

    // ─────────────────────────────────────────────────────────────────────────
    //  Inspector — Scenes
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Scene Names")]
    [SerializeField] private string gameSceneName    = "SampleScene";
    [SerializeField] private string optionsSceneName = "";  // leave blank to use a panel instead
    [SerializeField] private string creditsSceneName = "";  // leave blank to use a panel instead

    // ─────────────────────────────────────────────────────────────────────────
    //  Private state
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Index into <see cref="meshes"/> of the mesh currently at the Front slot.</summary>
    private int  _currentIndex    = 0;
    private bool _isTransitioning = false;

    private MainMenuMesh _hoveredMesh;

    // ─────────────────────────────────────────────────────────────────────────
    //  Slot mapping helpers
    // ─────────────────────────────────────────────────────────────────────────
    // Slot 0 = Front, 1 = Left, 2 = Right, 3 = Back
    // The mesh at meshes[i] occupies slot  (i - _currentIndex + 4) % 4

    private int GetSlotForMesh(int meshIndex) => (meshIndex - _currentIndex + 4) % 4;

    private void GetAnchorForSlot(int slot, out Vector3 position, out Quaternion rotation)
    {
        position = anchors[slot].position;
        float yAngle = slot switch
        {
            0 => frontFacingY,
            1 => leftFacingY,
            2 => rightFacingY,
            _ => backFacingY,
        };
        rotation = Quaternion.Euler(0f, yAngle, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity
    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (meshes.Length != 4 || anchors.Length != 4)
        {
            Debug.LogError("[MainMenuController] Requires exactly 4 meshes and 4 anchors!");
            enabled = false;
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            if (meshes[i] == null)  { Debug.LogError($"[MainMenuController] meshes[{i}] is null!"); enabled = false; return; }
            if (anchors[i] == null) { Debug.LogError($"[MainMenuController] anchors[{i}] is null!"); enabled = false; return; }
        }

        // Push the correct label to every mesh so none accidentally shows "Play"
        for (int i = 0; i < 4; i++)
            meshes[i].SetLabel(i < optionLabels.Length ? optionLabels[i] : $"Option {i}");

        // Stagger the float animation so the cartridges bob independently
        float[] phases = { 0f, 1.57f, 3.14f, 4.71f };
        for (int i = 0; i < 4; i++)
            meshes[i].phaseOffset = phases[i];

        // Snap meshes to their starting anchors immediately
        RefreshMeshAnchors(instant: true);

        // Hook up arrow buttons
        if (leftArrowButton  != null) leftArrowButton.onClick.AddListener(CycleLeft);
        if (rightArrowButton != null) rightArrowButton.onClick.AddListener(CycleRight);
    }

    private void Update()
    {
        if (_isTransitioning) return;

        // ── Keyboard ─────────────────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.LeftArrow))                               CycleLeft();
        if (Input.GetKeyDown(KeyCode.RightArrow))                              CycleRight();
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ActivateFrontMesh();

        // ── Mouse hover (raycast) ─────────────────────────────────────────────
        HandleMouseHover();

        // ── Mouse click ───────────────────────────────────────────────────────
        if (Input.GetMouseButtonDown(0) && _hoveredMesh != null)
        {
            if (_hoveredMesh == meshes[_currentIndex])
            {
                // Clicking the front mesh → confirm selection
                ActivateFrontMesh();
            }
            else
            {
                // Clicking a side mesh → cycle to it
                CycleToMesh(System.Array.IndexOf(meshes, _hoveredMesh));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mouse hover
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleMouseHover()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        MainMenuMesh newHovered = null;

        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            newHovered = hit.collider.GetComponentInParent<MainMenuMesh>();

        if (newHovered == _hoveredMesh) return;

        _hoveredMesh?.SetHovered(false);
        _hoveredMesh = newHovered;
        _hoveredMesh?.SetHovered(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cycling
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Bring the previous option to the front.</summary>
    public void CycleLeft()
    {
        if (_isTransitioning) return;
        _currentIndex = (_currentIndex - 1 + 4) % 4;
        StartCoroutine(AnimateCycle());
    }

    /// <summary>Bring the next option to the front.</summary>
    public void CycleRight()
    {
        if (_isTransitioning) return;
        _currentIndex = (_currentIndex + 1) % 4;
        StartCoroutine(AnimateCycle());
    }

    /// <summary>Cycle until <paramref name="meshIndex"/> is at the front, choosing the shortest path.</summary>
    private void CycleToMesh(int meshIndex)
    {
        if (meshIndex < 0 || meshIndex >= 4 || meshIndex == _currentIndex) return;
        if (_isTransitioning) return;

        // Determine shortest direction
        int diff = (meshIndex - _currentIndex + 4) % 4;
        if (diff == 1 || diff == 2) CycleRight();
        else                        CycleLeft();
    }

    private IEnumerator AnimateCycle()
    {
        _isTransitioning = true;

        // Snapshot current world positions/rotations before the transition
        Vector3[]    startPositions = new Vector3[4];
        Quaternion[] startRotations = new Quaternion[4];
        for (int i = 0; i < 4; i++)
        {
            startPositions[i] = meshes[i].transform.position;
            startRotations[i] = meshes[i].transform.rotation;
        }

        // Immediately update IsFront flags so lights and scales start changing
        for (int i = 0; i < 4; i++)
            meshes[i].SetFront(i == _currentIndex);

        // Smoothly move every mesh toward its new anchor
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.MoveTowards(t, 1f, Time.deltaTime / cycleAnimDuration);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < 4; i++)
            {
                int slot = GetSlotForMesh(i);
                GetAnchorForSlot(slot, out Vector3 targetPos, out Quaternion targetRot);

                // Push the anchor so the float animation re-centres there immediately
                meshes[i].SetAnchor(targetPos, targetRot);

                // Override position during the transition itself
                meshes[i].transform.position = Vector3.Lerp(startPositions[i], targetPos, ease);
            }

            yield return null;
        }

        RefreshMeshAnchors(instant: false);
        _isTransitioning = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Selection
    // ─────────────────────────────────────────────────────────────────────────

    private void ActivateFrontMesh()
    {
        if (_isTransitioning) return;

        _isTransitioning = true;
        meshes[_currentIndex].PlaySelectAnimation(() => ExecuteOption(_currentIndex));
    }

    private void ExecuteOption(int meshIndex)
    {
        string label = meshes[meshIndex].optionLabel.Trim().ToLower();

        switch (label)
        {
            case "play":
                SceneManager.LoadScene(gameSceneName);
                break;

            case "options":
                if (!string.IsNullOrEmpty(optionsSceneName))
                    SceneManager.LoadScene(optionsSceneName);
                else
                {
                    Debug.Log("[MainMenu] Options selected — wire up your options panel here.");
                    meshes[meshIndex].ResetFromAnimation();
                    _isTransitioning = false;
                }
                break;

            case "credits":
                if (!string.IsNullOrEmpty(creditsSceneName))
                    SceneManager.LoadScene(creditsSceneName);
                else
                {
                    Debug.Log("[MainMenu] Credits selected — wire up your credits panel here.");
                    meshes[meshIndex].ResetFromAnimation();
                    _isTransitioning = false;
                }
                break;

            case "exit":
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                break;

            default:
                Debug.LogWarning($"[MainMenu] Unknown option label: '{label}'");
                meshes[meshIndex].ResetFromAnimation();
                _isTransitioning = false;
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tells every mesh where its anchor is and whether it is the front one.
    /// If <paramref name="instant"/> is true, also teleports the mesh there immediately.
    /// </summary>
    private void RefreshMeshAnchors(bool instant)
    {
        for (int i = 0; i < 4; i++)
        {
            int slot = GetSlotForMesh(i);
            GetAnchorForSlot(slot, out Vector3 pos, out Quaternion rot);
            meshes[i].SetAnchor(pos, rot);
            meshes[i].SetFront(i == _currentIndex);

            if (instant)
                meshes[i].transform.position = pos;
        }
    }
}
