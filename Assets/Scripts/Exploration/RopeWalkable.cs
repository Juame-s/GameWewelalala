using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(LineRenderer))]
public class RopeWalkable : MonoBehaviour
{
    [Tooltip("Start point of the rope")]
    public Transform startPoint;
    [Tooltip("End point of the rope")]
    public Transform endPoint;

    [Tooltip("Base instability factor. Longer ropes multiply this value.")]
    public float baseInstability = 15f;

    [Header("Visuals")]
    [Tooltip("How much the rope sags in the middle")]
    public float sagAmount = 0.5f;
    [Tooltip("Thickness of the rope visual")]
    public float ropeWidth = 0.05f;
    [Tooltip("Number of segments to draw the rope curve")]
    public int resolution = 20;

    private LineRenderer lineRenderer;

    public float RopeLength
    {
        get
        {
            if (startPoint != null && endPoint != null)
                return Vector3.Distance(startPoint.position, endPoint.position);
            return 0f;
        }
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        UpdateRopeVisuals();
    }

    private void OnValidate()
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        UpdateRopeVisuals();
    }

    public void UpdateRopeVisuals()
    {
        if (startPoint == null || endPoint == null || lineRenderer == null) return;

        lineRenderer.positionCount = resolution + 1;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        for (int i = 0; i <= resolution; i++)
        {
            float t = (float)i / resolution;
            Vector3 point = GetRopePoint(t * RopeLength);
            lineRenderer.SetPosition(i, point);
        }
    }

    /// <summary>
    /// Gets the world position on the rope at a specific distance from the start point.
    /// Includes the sagging calculation.
    /// </summary>
    public Vector3 GetRopePoint(float distanceFromStart)
    {
        if (startPoint == null || endPoint == null) return transform.position;

        float length = RopeLength;
        if (length == 0) return startPoint.position;

        // Normalized progress (0 to 1)
        float t = Mathf.Clamp01(distanceFromStart / length);

        // Linear interpolation
        Vector3 linearPos = Vector3.Lerp(startPoint.position, endPoint.position, t);

        // Calculate sag using a simple parabola: y = 4 * sag * t * (1 - t)
        float currentSag = 4f * sagAmount * t * (1f - t);

        // Apply sag downwards
        return linearPos - new Vector3(0, currentSag, 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null && !pc.IsRopeWalking)
            {
                pc.StartRopeWalking(this);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (startPoint != null && endPoint != null)
        {
            Gizmos.color = new Color(0.6f, 0.3f, 0.1f);
            Vector3 prev = GetRopePoint(0);
            for (int i = 1; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 current = GetRopePoint(t * RopeLength);
                Gizmos.DrawLine(prev, current);
                prev = current;
            }
            Gizmos.DrawSphere(startPoint.position, 0.2f);
            Gizmos.DrawSphere(endPoint.position, 0.2f);
        }
    }
}
