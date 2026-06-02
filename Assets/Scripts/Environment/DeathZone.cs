using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to any trigger collider in the scene.
/// When the Player enters it:
///   1. A white screen-flash plays (fade in → hold → fade out).
///   2. The player is teleported to the nearest active RespawnPoint in the scene.
///   3. Player velocity is zeroed so they don't inherit momentum.
///
/// Setup:
///   • Mark the collider as "Is Trigger".
///   • Optionally assign a RespawnPoint via the Inspector; if left empty the
///     script will auto-find the closest one at runtime.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DeathZone : MonoBehaviour
{
    // ─────────────────────────────── Inspector ───────────────────────────────

    [Header("Respawn")]
    [Tooltip("Specific respawn point to use. Leave empty to use the closest RespawnPoint in the scene.")]
    [SerializeField] private RespawnPoint overrideRespawnPoint;

    [Header("Screen Flash")]
    [Tooltip("Color of the flash overlay (default: white).")]
    [SerializeField] private Color flashColor = Color.white;
    [Tooltip("How long (seconds) the flash takes to fully appear.")]
    [SerializeField] private float fadeInDuration  = 0.15f;
    [Tooltip("How long (seconds) the screen stays fully flashed before fading out.")]
    [SerializeField] private float holdDuration    = 0.1f;
    [Tooltip("How long (seconds) the flash takes to fully disappear.")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Header("Player Lock")]
    [Tooltip("Freeze the PlayerController's input during the flash sequence.")]
    [SerializeField] private bool lockPlayerDuringFlash = true;

    // ────────────────────────────── Private State ─────────────────────────────

    private Image    flashImage;
    private Canvas   flashCanvas;
    private bool     isTeleporting;

    // ───────────────────────────────── Unity ─────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        BuildFlashOverlay();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTeleporting) return;

        // Only react to the player
        if (!other.CompareTag("Player")) return;

        StartCoroutine(RespawnSequence(other.gameObject));
    }

    // ──────────────────────────── Core sequence ───────────────────────────────

    private IEnumerator RespawnSequence(GameObject player)
    {
        isTeleporting = true;

        // Lock player input
        PlayerController pc = player.GetComponent<PlayerController>();
        if (lockPlayerDuringFlash && pc != null)
            pc.SetDialogueMode(true);

        // ── Flash in ──
        yield return StartCoroutine(FadeFlash(0f, 1f, fadeInDuration));

        // ── Teleport ──
        TeleportPlayer(player);

        // ── Hold ──
        yield return new WaitForSeconds(holdDuration);

        // ── Flash out ──
        yield return StartCoroutine(FadeFlash(1f, 0f, fadeOutDuration));

        // Unlock player input
        if (lockPlayerDuringFlash && pc != null)
            pc.SetDialogueMode(false);

        isTeleporting = false;
    }

    private void TeleportPlayer(GameObject player)
    {
        // Find the respawn point to use
        RespawnPoint target = overrideRespawnPoint;

        if (target == null)
            target = FindClosestRespawnPoint(player.transform.position);

        if (target == null)
        {
            Debug.LogWarning("[DeathZone] No RespawnPoint found in the scene. " +
                             "Add a GameObject with a RespawnPoint component.");
            return;
        }

        // Zero out rigidbody velocity before moving
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        player.transform.position = target.SpawnPosition;
        player.transform.rotation = target.SpawnRotation;

        Debug.Log($"[DeathZone] Player respawned at '{target.name}'.");
    }

    private RespawnPoint FindClosestRespawnPoint(Vector3 from)
    {
        RespawnPoint[] all = FindObjectsByType<RespawnPoint>(FindObjectsSortMode.None);
        RespawnPoint   best     = null;
        float          bestDist = float.MaxValue;

        foreach (RespawnPoint rp in all)
        {
            float d = Vector3.Distance(from, rp.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best     = rp;
            }
        }

        return best;
    }

    // ───────────────────────────── Flash helpers ──────────────────────────────

    private IEnumerator FadeFlash(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            SetFlashAlpha(alpha);
            yield return null;
        }
        SetFlashAlpha(to);
    }

    private void SetFlashAlpha(float alpha)
    {
        if (flashImage == null) return;
        Color c = flashImage.color;
        c.a           = alpha;
        flashImage.color = c;
    }

    // ─────────────────────── Build UI overlay at runtime ─────────────────────

    private void BuildFlashOverlay()
    {
        // One shared canvas per scene is fine — the image lives at the top of the stack.
        GameObject canvasGO = new GameObject("DeathZone_FlashCanvas");
        flashCanvas                  = canvasGO.AddComponent<Canvas>();
        flashCanvas.renderMode       = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder     = 999; // On top of everything
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO); // Persists through respawn, destroyed on scene load below

        GameObject imgGO = new GameObject("FlashImage");
        imgGO.transform.SetParent(canvasGO.transform, false);
        flashImage               = imgGO.AddComponent<Image>();
        flashImage.color         = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        flashImage.raycastTarget = false;

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
