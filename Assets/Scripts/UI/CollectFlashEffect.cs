using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton UI manager that plays a collect feedback sequence:
///   1. White flash fades in quickly.
///   2. White flash fades out while an icon image + label text fade in.
///   3. Icon + text hold for a moment, then fade out.
///
/// Setup:
///   - Add this component to a Canvas GameObject (e.g. "CollectCanvas") set to Screen Space - Overlay.
///   - Assign the four UI children in the Inspector:
///       • flashImage    — a full-screen Image (white, stretch to fill).
///       • collectImage  — an Image for the collected item icon (centre of screen).
///       • collectText   — a TextMeshProUGUI for the label.
///   - Call CollectFlashEffect.Instance.Play(sprite, label) from any collectable.
/// </summary>
public class CollectFlashEffect : MonoBehaviour
{
    public static CollectFlashEffect Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("Full-screen white Image used for the flash.")]
    [SerializeField] private Image flashImage;

    [Tooltip("Image that shows the collected item's icon.")]
    [SerializeField] private Image collectImage;

    [Tooltip("TextMeshPro label that shows the collection message.")]
    [SerializeField] private TextMeshProUGUI collectText;

    [Header("Timing")]
    [Tooltip("How fast the white flash fades IN (seconds).")]
    [SerializeField] private float flashFadeInDuration  = 0.08f;

    [Tooltip("How fast the white flash fades OUT (seconds).")]
    [SerializeField] private float flashFadeOutDuration = 0.3f;

    [Tooltip("How fast the icon + text fade IN (seconds).")]
    [SerializeField] private float contentFadeInDuration  = 0.25f;

    [Tooltip("How long the icon + text stay fully visible (seconds).")]
    [SerializeField] private float holdDuration = 1.4f;

    [Tooltip("How fast the icon + text fade OUT (seconds).")]
    [SerializeField] private float contentFadeOutDuration = 0.5f;

    // ---------------------------------------------------------------

    private Coroutine _activeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Keep alive across scene loads (remove if unwanted)
        DontDestroyOnLoad(gameObject);

        // Start hidden
        SetAlpha(flashImage,   0f);
        SetAlpha(collectImage, 0f);
        SetAlpha(collectText,  0f);
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Trigger the collect flash sequence.
    /// </summary>
    /// <param name="icon">Sprite to display. Pass null to hide the image.</param>
    /// <param name="label">Text to display below the icon.</param>
    public void Play(Sprite icon, string label)
    {
        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        collectImage.sprite  = icon;
        collectImage.enabled = (icon != null);
        collectText.text     = label ?? string.Empty;

        _activeRoutine = StartCoroutine(Sequence());
    }

    // ---------------------------------------------------------------
    // Internal
    // ---------------------------------------------------------------

    private IEnumerator Sequence()
    {
        // 1 — Flash IN
        yield return FadeTo(flashImage, 1f, flashFadeInDuration);

        // 2 — Flash OUT + content IN simultaneously
        Coroutine flashOut    = StartCoroutine(FadeTo(flashImage,   0f, flashFadeOutDuration));
        Coroutine contentIn   = StartCoroutine(FadeGroupTo(1f, contentFadeInDuration));
        yield return flashOut;
        yield return contentIn;

        // 3 — Hold
        yield return new WaitForSeconds(holdDuration);

        // 4 — Content OUT
        yield return FadeGroupTo(0f, contentFadeOutDuration);

        _activeRoutine = null;
    }

    private IEnumerator FadeTo(Graphic graphic, float target, float duration)
    {
        float start   = graphic.color.a;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(graphic, Mathf.Lerp(start, target, elapsed / duration));
            yield return null;
        }
        SetAlpha(graphic, target);
    }

    private IEnumerator FadeGroupTo(float target, float duration)
    {
        float startImg  = collectImage.color.a;
        float startTxt  = collectText.color.a;
        float elapsed   = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            SetAlpha(collectImage, Mathf.Lerp(startImg, target, t));
            SetAlpha(collectText,  Mathf.Lerp(startTxt, target, t));
            yield return null;
        }
        SetAlpha(collectImage, target);
        SetAlpha(collectText,  target);
    }

    private static void SetAlpha(Graphic graphic, float a)
    {
        if (graphic == null) return;
        Color c = graphic.color;
        c.a = a;
        graphic.color = c;
    }

    private static void SetAlpha(TextMeshProUGUI tmp, float a)
    {
        if (tmp == null) return;
        Color c = tmp.color;
        c.a = a;
        tmp.color = c;
    }
}
