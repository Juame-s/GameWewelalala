using UnityEngine;

/// <summary>
/// Place this component on any GameObject to mark it as a respawn location.
/// DeathZone will automatically find and use the closest active RespawnPoint.
///
/// Setup:
///   • Add this component to an empty GameObject at the desired spawn position.
///   • Optionally rotate the object so the player spawns facing that direction.
///   • You can have multiple RespawnPoints; DeathZone picks the nearest one
///     (or you can override which point to use in the DeathZone Inspector).
/// </summary>
public class RespawnPoint : MonoBehaviour
{
    // ─────────────────────────────── Inspector ───────────────────────────────

    [Tooltip("Colour used to draw this point's gizmo in the Scene view.")]
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0.4f, 0.85f);

    // ──────────────────────────── Public Accessors ────────────────────────────

    /// <summary>World-space position the player will be moved to.</summary>
    public Vector3   SpawnPosition => transform.position;

    /// <summary>World-space rotation the player will be set to (faces the object's forward).</summary>
    public Quaternion SpawnRotation => transform.rotation;

    // ─────────────────────────────── Gizmos ──────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        // Draw a sphere at the spawn location
        Gizmos.DrawSphere(transform.position, 0.3f);

        // Draw a line showing the facing direction
        Gizmos.DrawLine(transform.position,
                        transform.position + transform.forward * 1.2f);

        // Arrow tip
        Vector3 tip    = transform.position + transform.forward * 1.2f;
        Vector3 left   = transform.position + transform.forward * 0.8f + transform.right * -0.2f;
        Vector3 right2 = transform.position + transform.forward * 0.8f + transform.right *  0.2f;
        Gizmos.DrawLine(tip, left);
        Gizmos.DrawLine(tip, right2);

#if UNITY_EDITOR
        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                                  $"Respawn: {gameObject.name}");
#endif
    }
}
