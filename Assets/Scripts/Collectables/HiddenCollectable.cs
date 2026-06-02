using System.Collections;
using UnityEngine;

public class HiddenCollectable : MonoBehaviour
{
    [Header("Reveal Animation Settings")]
    [Tooltip("How far below the actual position it starts when revealing.")]
    [SerializeField] private float popUpDistance = 2f;
    [Tooltip("How long the reveal animation takes.")]
    [SerializeField] private float revealDuration = 1f;
    [Tooltip("How fast it spins while revealing.")]
    [SerializeField] private float rapidSpinSpeed = 1440f;

    private Renderer[] renderers;
    private Collider[] colliders;
    private SpinAndFloat spinAndFloat;

    private bool isRevealed = false;

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        spinAndFloat = GetComponent<SpinAndFloat>();

        // Hide object
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = false;
        }

        foreach (var c in colliders)
        {
            if (c != null) c.enabled = false;
        }

        if (spinAndFloat != null)
        {
            spinAndFloat.enabled = false;
        }
    }

    private void Update()
    {
        if (isRevealed) return;

        if (PlayerController.Instance != null && PlayerController.Instance.IsChargingMana)
        {
            float dist = Vector3.Distance(transform.position, PlayerController.Instance.transform.position);
            if (dist <= PlayerController.Instance.CurrentMeltRadius)
            {
                Reveal();
            }
        }
    }

    private void Reveal()
    {
        if (isRevealed) return;
        isRevealed = true;

        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        Vector3 targetPos = transform.position;
        Vector3 startPos = targetPos + Vector3.down * popUpDistance;
        transform.position = startPos;

        // Enable renderers so it becomes visible
        foreach (var r in renderers)
        {
            if (r != null) r.enabled = true;
        }

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / revealDuration;

            // easeOutBack function for overshoot
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float easeT = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

            transform.position = Vector3.LerpUnclamped(startPos, targetPos, easeT);
            transform.Rotate(Vector3.up * rapidSpinSpeed * Time.deltaTime, Space.World);

            yield return null;
        }

        transform.position = targetPos;

        // Enable interaction and normal idle animation
        foreach (var c in colliders)
        {
            if (c != null) c.enabled = true;
        }

        if (spinAndFloat != null)
        {
            spinAndFloat.enabled = true;
        }
    }
}
