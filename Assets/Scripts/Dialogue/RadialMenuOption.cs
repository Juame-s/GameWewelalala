using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class RadialMenuOption : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Hover Settings")]
    public float hoverScale = 1.1f;
    public float scaleSpeed = 10f;
    
    [Header("Animation Settings")]
    public float animationDuration = 0.3f;
    public AnimationCurve appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Idle Animation Settings")]
    public float floatAmplitude = 5f;
    public float floatSpeed = 2f;
    private Vector3 idleOffset;
    private float floatTimer;

    private Vector3 targetScale;
    private Vector3 originalScale;
    private Vector3 basePosition;
    private bool isAppeared = false;

    private void Awake()
    {
        originalScale = transform.localScale;
        targetScale = originalScale;
        floatTimer = Random.Range(0f, 10f); // Randomize start phase
    }

    private void Update()
    {
        // Smooth scale for hover
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);

        // Floaty idle animation
        if (isAppeared)
        {
            floatTimer += Time.deltaTime * floatSpeed;
            idleOffset = new Vector3(0, Mathf.Sin(floatTimer) * floatAmplitude, 0);
            transform.localPosition = basePosition + idleOffset;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
    }

    /// <summary>
    /// Animates the option appearing from the center
    /// </summary>
    /// <param name="targetLocalPosition">The final position on the radial wheel</param>
    /// <param name="delay">Delay before starting the animation</param>
    public void AnimateAppearance(Vector3 targetLocalPosition, float delay)
    {
        StartCoroutine(AppearanceCoroutine(targetLocalPosition, delay));
    }

    private IEnumerator AppearanceCoroutine(Vector3 targetPos, float delay)
    {
        transform.localPosition = Vector3.zero;
        targetScale = Vector3.zero;
        transform.localScale = Vector3.zero;
        
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        targetScale = originalScale; // Start expanding to normal size
        
        float time = 0f;
        while (time < animationDuration)
        {
            time += Time.deltaTime;
            float t = appearCurve.Evaluate(time / animationDuration);
            
            transform.localPosition = Vector3.LerpUnclamped(Vector3.zero, targetPos, t);
            
            yield return null;
        }

        transform.localPosition = targetPos;
        basePosition = targetPos;
        isAppeared = true;
    }
}
