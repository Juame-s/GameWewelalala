using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A self-contained, DontDestroyOnLoad screen fader.
/// Created at runtime by MainMenuZone (or any other transition script).
/// Automatically fades back out once the new scene has finished loading,
/// then destroys itself.
/// </summary>
public class SceneFader : MonoBehaviour
{
    // ─────────────────────────────── Factory ─────────────────────────────────

    /// <summary>
    /// Creates a new SceneFader that will persist into the next scene and
    /// fade the overlay back out automatically.
    /// </summary>
    public static SceneFader Create(Color color, float fadeOutDuration)
    {
        // Canvas
        GameObject go     = new GameObject("SceneFader_Canvas");
        Canvas canvas     = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(go);

        // Full-screen image
        GameObject imgGO = new GameObject("FadeImage");
        imgGO.transform.SetParent(go.transform, false);
        Image img       = imgGO.AddComponent<Image>();
        img.color       = new Color(color.r, color.g, color.b, 0f);
        img.raycastTarget = false;

        RectTransform rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Fader component
        SceneFader fader          = go.AddComponent<SceneFader>();
        fader.fadeImage           = img;
        fader.autoFadeOutDuration = fadeOutDuration;

        return fader;
    }

    // ─────────────────────────────── State ───────────────────────────────────

    private Image  fadeImage;
    private float  autoFadeOutDuration;
    private bool   fadeOutStarted;

    // ───────────────────────────────── Unity ─────────────────────────────────

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (fadeOutStarted) return;
        fadeOutStarted = true;
        StartCoroutine(FadeOutAndDestroy());
    }

    // ─────────────────────────── Public helpers ───────────────────────────────

    /// <summary>Fades the overlay from transparent to fully opaque.</summary>
    public IEnumerator FadeIn(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    // ─────────────────────────── Internal helpers ─────────────────────────────

    private IEnumerator FadeOutAndDestroy()
    {
        yield return Fade(1f, 0f, autoFadeOutDuration);
        Destroy(gameObject); // Clean up canvas + image + this component
    }

    public IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        if (fadeImage == null) return;
        Color c  = fadeImage.color;
        c.a      = alpha;
        fadeImage.color = c;
    }
}
