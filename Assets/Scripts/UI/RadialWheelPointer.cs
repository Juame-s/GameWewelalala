using UnityEngine;

public class RadialWheelPointer : MonoBehaviour
{
    [Tooltip("The RectTransform of the center wheel graphic")]
    public RectTransform wheelCenter;
    
    [Tooltip("Offset rotation if the arrow sprite doesn't point naturally right/up")]
    public float rotationOffset = 0f;

    private void Update()
    {
        if (wheelCenter == null) return;

        // Get the mouse position in screen space
        Vector2 mousePosition = Input.mousePosition;

        // Convert the wheel's center world position to screen space
        Vector2 wheelScreenPosition = RectTransformUtility.WorldToScreenPoint(null, wheelCenter.position);
        
        // If a canvas with a camera is used, you'd pass the camera above instead of null.
        // Assuming Screen Space - Overlay, null works perfectly.

        // Calculate the direction from the wheel to the mouse
        Vector2 direction = mousePosition - wheelScreenPosition;

        // Calculate the angle using Atan2
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Apply rotation with offset
        transform.rotation = Quaternion.Euler(0, 0, angle + rotationOffset);
    }
}
