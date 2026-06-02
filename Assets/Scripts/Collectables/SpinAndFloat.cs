using UnityEngine;

/// <summary>
/// A simple script to make an object slowly spin and float up and down.
/// Great for collectables.
/// </summary>
public class SpinAndFloat : MonoBehaviour
{
    [Header("Spin Settings")]
    [Tooltip("Degrees per second to rotate around the Y axis.")]
    public float spinSpeed = 90f;

    [Header("Float Settings")]
    [Tooltip("How fast the object bobs up and down.")]
    public float floatSpeed = 2f;
    [Tooltip("How high/low the object moves from its starting position.")]
    public float floatAmplitude = 0.25f;

    private Vector3 startPos;
    private float timeOffset;

    void Start()
    {
        startPos = transform.position;
        // Add a random time offset so multiple items don't bob exactly in sync
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        // Handle Spinning
        transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime, Space.World);

        // Handle Floating
        float newY = startPos.y + Mathf.Sin((Time.time * floatSpeed) + timeOffset) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
}
