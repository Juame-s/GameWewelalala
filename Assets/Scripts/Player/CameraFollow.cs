using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;
    
    [Header("Camera Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -7f);
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float lookAtHeight = 1.5f;
    
    [Header("Mouse Control")]
    [SerializeField] private bool enableMouseRotation = true;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minVerticalAngle = -60f; // Looking down limit
    [SerializeField] private float maxVerticalAngle = 80f;  // Looking up limit (prevents flip at 90)
    
    [Header("Collision Detection")]
    [SerializeField] private bool enableCollisionDetection = true;
    [SerializeField] private LayerMask collisionLayers; // Layers the camera should avoid
    [SerializeField] private float collisionRadius = 0.3f; // Radius for sphere cast
    [SerializeField] private float minDistance = 1f; // Minimum distance from player
    [SerializeField] private float collisionSmoothSpeed = 15f; // How fast camera adjusts to collisions
    
    private float currentHorizontalAngle = 0f;
    private float currentVerticalAngle = 20f;
    private float currentDistance; // Current distance from player (adjusted for collisions)
    
    // Dialogue state — set by DialogueManager, blocks mouse rotation without disabling this component
    public bool IsInDialogue { get; set; }
    
    // Public property to get camera angle
    public float HorizontalAngle => currentHorizontalAngle;
    public float VerticalAngle => currentVerticalAngle;
    
    private Transform _cinematicTarget;
    private float _cinematicWeight = 0f;
    private Coroutine _panCoroutine;

    void Start()
    {
        // Load sensitivity from settings
        mouseSensitivity = PlayerPrefs.GetFloat("Sensitivity", 3f);

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
        
        // Initialize current distance to the offset magnitude
        currentDistance = offset.magnitude;
        
        // If no collision layers set, use default
        if (collisionLayers == 0)
        {
            collisionLayers = LayerMask.GetMask("Default");
        }
        
        // Lock cursor for better camera control
        if (enableMouseRotation)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
    
    void LateUpdate()
    {
        if (target == null) return;
        
        // Do not update camera logic or lock cursor if the game is paused
        if (Time.timeScale == 0f) return;
        
        // Handle mouse input for camera rotation
        if (enableMouseRotation && !IsInDialogue)
        {
            float currentSens = mouseSensitivity;

            currentHorizontalAngle += Input.GetAxis("Mouse X") * currentSens;
            currentVerticalAngle -= Input.GetAxis("Mouse Y") * currentSens;
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
            
            // Unlock cursor with Escape
            // Escape key handling is now managed by PauseMenuManager
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
        
        // Calculate rotation based on mouse input
        Quaternion rotation = Quaternion.Euler(currentVerticalAngle, currentHorizontalAngle, 0f);
        
        // Calculate desired direction and distance
        Vector3 direction = rotation * offset.normalized;
        float desiredDistance = offset.magnitude;
        
        // Check for collisions and adjust distance
        if (enableCollisionDetection)
        {
            desiredDistance = CheckCameraCollision(target.position, direction, desiredDistance);
        }
        
        // Smoothly adjust current distance
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, collisionSmoothSpeed * Time.deltaTime);
        
        // Calculate final position with adjusted distance
        Vector3 desiredPosition = target.position + direction * currentDistance;
        
        // Smoothly move camera
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        
        // Base Look at target
        Vector3 baseLookAtPosition = target.position + Vector3.up * lookAtHeight;
        transform.LookAt(baseLookAtPosition);
        
        // Apply cinematic rotation override if active
        if (_cinematicTarget != null && _cinematicWeight > 0f)
        {
            Vector3 dirToTarget = (_cinematicTarget.position - transform.position).normalized;
            if (dirToTarget != Vector3.zero)
            {
                Quaternion cinematicRotation = Quaternion.LookRotation(dirToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, cinematicRotation, _cinematicWeight);
            }
        }
    }
    
    /// <summary>
    /// Check for collisions between target and camera, return safe distance
    /// </summary>
    private float CheckCameraCollision(Vector3 targetPosition, Vector3 direction, float maxDistance)
    {
        RaycastHit hit;
        
        // Use SphereCast to detect obstacles between player and camera
        if (Physics.SphereCast(targetPosition, collisionRadius, direction, out hit, maxDistance, collisionLayers))
        {
            // Calculate safe distance (slightly before the hit point)
            float safeDistance = hit.distance - collisionRadius;
            
            // Clamp to minimum distance
            safeDistance = Mathf.Max(safeDistance, minDistance);
            
            return safeDistance;
        }
        
        // No collision, use max distance
        return maxDistance;
    }

    /// <summary>
    /// Temporarily changes the camera's target to pan towards a specific point, then returns to the player.
    /// </summary>
    public void PanToTemporaryTarget(Transform tempTarget, float duration, float delay = 0f, float panSpeed = 2f)
    {
        if (_panCoroutine != null) StopCoroutine(_panCoroutine);
        _panCoroutine = StartCoroutine(PanRoutine(tempTarget, duration, delay, panSpeed));
    }

    private System.Collections.IEnumerator PanRoutine(Transform tempTarget, float duration, float delay, float customPanSpeed)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        _cinematicTarget = tempTarget;
        
        // Disable mouse control while panning so the player can't mess up the view
        bool wasMouseEnabled = enableMouseRotation;
        enableMouseRotation = false;

        // Smoothly pan TO the target
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * customPanSpeed;
            _cinematicWeight = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }
        _cinematicWeight = 1f;

        // Hold
        yield return new WaitForSeconds(duration);

        // Smoothly return TO the player
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * customPanSpeed;
            _cinematicWeight = Mathf.SmoothStep(1f, 0f, t);
            yield return null;
        }
        _cinematicWeight = 0f;

        // Restore controls
        _cinematicTarget = null;
        enableMouseRotation = wasMouseEnabled;
        _panCoroutine = null;
    }

    /// <summary>
    /// Called by SettingsMenu when sensitivity changes to immediately update it.
    /// </summary>
    public void UpdateSensitivity()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("Sensitivity", 3f);
    }
}
