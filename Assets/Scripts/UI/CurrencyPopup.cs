using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
[RequireComponent(typeof(RectTransform))]
public class CurrencyPopup : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float floatSpeed = 100f; // Speed of upward movement
    [SerializeField] private float fadeDuration = 1.5f; // How long it takes to fade and destroy

    private TextMeshProUGUI textMesh;
    private RectTransform rectTransform;

    public void Setup(int amount)
    {
        textMesh = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();

        if (textMesh != null)
        {
            textMesh.text = amount.ToString();
            StartCoroutine(AnimatePopup());
        }
    }

    private IEnumerator AnimatePopup()
    {
        float timer = 0f;
        Color startColor = textMesh.color;
        Vector3 startPos = rectTransform.anchoredPosition;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / fadeDuration;

            // Move upwards
            rectTransform.anchoredPosition = startPos + new Vector3(0, floatSpeed * timer, 0);

            // Fade out alpha
            Color newColor = startColor;
            newColor.a = Mathf.Lerp(1f, 0f, normalizedTime);
            textMesh.color = newColor;

            yield return null; // Wait for next frame
        }

        // Destroy the popup once animation completes
        Destroy(gameObject);
    }
}
