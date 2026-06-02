using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Attach to any trigger collider in the scene.
/// When the Player enters it, a smooth fade-to-black plays, the scene loads,
/// and then the overlay fades back out automatically in the new scene.
///
/// Setup:
///   • Mark the collider as "Is Trigger".
///   • The player GameObject must have its Tag set to "Player".
///   • The scene must be added to File → Build Settings.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MainMenuZone : MonoBehaviour
{
    // ─────────────────────────────── Inspector ───────────────────────────────

    [Header("Scene")]
    [Tooltip("Exact name of the Main Menu scene (must be in Build Settings).")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Fade Transition")]
    [Tooltip("Colour to fade through (default: black).")]
    [SerializeField] private Color fadeColor = Color.black;
    [Tooltip("How long (seconds) the fade-to-colour takes.")]
    [SerializeField] private float fadeInDuration  = 0.6f;
    [Tooltip("How long the screen stays fully covered before loading.")]
    [SerializeField] private float holdDuration    = 0.2f;
    [Tooltip("How long the fade-back-out takes after the new scene loads.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("Player Lock")]
    [Tooltip("Freeze the player's input while the transition plays.")]
    [SerializeField] private bool lockPlayerDuringTransition = true;

    [Header("Debug")]
    [Tooltip("Print detailed logs to the Console.")]
    [SerializeField] private bool debugLogs = true;

    // ────────────────────────────── Private State ─────────────────────────────

    private bool isTransitioning;

    // ───────────────────────────────── Unity ─────────────────────────────────

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[MainMenuZone] '{name}': Collider was not IsTrigger — fixed automatically.", this);
        }

        // Validate scene is in Build Settings
        bool found = false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneName == mainMenuSceneName) { found = true; break; }
        }
        if (!found)
            Debug.LogError($"[MainMenuZone] Scene '{mainMenuSceneName}' not found in Build Settings! " +
                           "Open File → Build Settings and add it.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (debugLogs)
            Debug.Log($"[MainMenuZone] OnTriggerEnter: '{other.name}' (tag: '{other.tag}')", this);

        if (isTransitioning) return;
        if (!other.CompareTag("Player"))
        {
            if (debugLogs) Debug.Log($"[MainMenuZone] Ignored — tag '{other.tag}' is not 'Player'.", this);
            return;
        }

        if (debugLogs) Debug.Log("[MainMenuZone] Starting transition to Main Menu...", this);
        StartCoroutine(TransitionToMainMenu(other.gameObject));
    }

    // ──────────────────────────── Core sequence ───────────────────────────────

    private IEnumerator TransitionToMainMenu(GameObject player)
    {
        isTransitioning = true;

        // Lock player input
        PlayerController pc = player.GetComponent<PlayerController>();
        if (lockPlayerDuringTransition && pc != null)
            pc.SetDialogueMode(true);

        // Build the persistent fader — it will outlive this script
        SceneFader fader = SceneFader.Create(fadeColor, fadeOutDuration);

        // Fade in (to black)
        yield return StartCoroutine(fader.FadeIn(fadeInDuration));

        // Hold
        yield return new WaitForSeconds(holdDuration);

        // Load the scene — this object will be destroyed, but SceneFader persists
        if (debugLogs) Debug.Log($"[MainMenuZone] Loading '{mainMenuSceneName}'...", this);
        SceneManager.LoadScene(mainMenuSceneName);

        // Execution stops here because this MonoBehaviour is destroyed with the scene.
        // SceneFader takes over and fades out in the new scene.
    }
}
