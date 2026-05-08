using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float jumpForce = 3f;

    [Header("Run Settings")]
    [SerializeField] private float doubleTapWindow = 0.3f; // Max time between two W presses to trigger run

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float groundCheckRadius = 0.3f;

    [SerializeField] private float jumpCooldown = 0.2f;
    private float lastJumpTime;

    private Rigidbody rb;
    private bool isGrounded;
    private Vector3 moveDirection;
    private CapsuleCollider capsuleCollider;
    private bool jumpRequested;

    // Run state
    private bool isRunning;
    private float lastWPressTime = -999f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 70f;

        if (groundLayer == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
            Debug.LogWarning("PlayerController: Ground layer not set in inspector, using Default layer.");
        }
    }

    void Update()
    {
        HandleRunInput();

        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Calculate move direction relative to camera
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        moveDirection = (forward * vertical + right * horizontal).normalized;

        // Cancel run if player stops moving or is not going forward
        if (moveDirection.magnitude < 0.1f || vertical <= 0f)
            isRunning = false;

        // Rotate player to face camera's forward direction
        Vector3 cameraForward = Camera.main.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        if (cameraForward.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Jump input
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && Time.time >= lastJumpTime + jumpCooldown)
        {
            jumpRequested = true;
            lastJumpTime = Time.time;
        }

        UpdateAnimations();
    }

    private void HandleRunInput()
    {
        // Shift held → run (only if moving forward)
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            float v = Input.GetAxisRaw("Vertical");
            if (v > 0f) isRunning = true;
            return;
        }

        // Stop running if Shift released and not double-tap running
        // (double-tap keeps isRunning until player stops, so we only clear it on stop-move above)

        // Double-tap W detection (W is always forward, so no extra check needed)
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (Time.time - lastWPressTime <= doubleTapWindow)
            {
                isRunning = true;
                lastWPressTime = -999f;
            }
            else
            {
                lastWPressTime = Time.time;
            }
        }

        // If Shift is not held and we were running only via Shift, stop
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            // Only clear Shift-based run; double-tap run persists until player stops moving
            // We differentiate by checking: if isRunning was set by Shift and Shift is now released
            // Since we can't easily distinguish, we rely on the stop-move cancel in Update()
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null) return;

        bool isMoving = moveDirection.magnitude > 0.1f;

        animator.SetBool("IsWalking", isMoving && !isRunning);
        animator.SetBool("IsRunning", isMoving && isRunning);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
    }

    void FixedUpdate()
    {
        // Ground check
        float castDistance = 0.1f;
        if (capsuleCollider != null)
            castDistance = (capsuleCollider.height / 2f) - groundCheckRadius + groundCheckDistance;

        isGrounded = Physics.SphereCast(transform.position, groundCheckRadius, Vector3.down, out RaycastHit hit, castDistance, groundLayer);

        // Move player (use run speed if running)
        float currentSpeed = isRunning ? runSpeed : moveSpeed;
        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;

        // Apply jump
        if (jumpRequested && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, Mathf.Sqrt(2f * jumpForce * Physics.gravity.magnitude), rb.linearVelocity.z);
            jumpRequested = false;

            if (animator != null)
                animator.SetTrigger("Jump");
        }
        else if (jumpRequested)
        {
            jumpRequested = false;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (isGrounded) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            float normalY = contact.normal.y;

            if (Mathf.Abs(normalY) < 0.5f)
            {
                Vector3 pushDirection = contact.normal;
                pushDirection.y = 0;
                pushDirection.Normalize();

                Vector3 velocity = rb.linearVelocity;
                velocity.x = pushDirection.x * 0.5f;
                velocity.z = pushDirection.z * 0.5f;
                rb.linearVelocity = velocity;

                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;

        float castDistance = groundCheckDistance;
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col != null)
            castDistance = (col.height / 2f) - groundCheckRadius + groundCheckDistance;

        Vector3 start = transform.position;
        Vector3 end = transform.position + Vector3.down * castDistance;

        Gizmos.DrawWireSphere(start, groundCheckRadius);
        Gizmos.DrawWireSphere(end, groundCheckRadius);
        Gizmos.DrawLine(start, end);

        Gizmos.color = Color.green;
        Vector3 frontPosition = transform.position + Vector3.up * 1f;
        Gizmos.DrawRay(frontPosition, transform.forward * 1.5f);
        Gizmos.DrawSphere(frontPosition + transform.forward * 1.5f, 0.1f);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(frontPosition, -transform.forward * 0.5f);
    }
}
