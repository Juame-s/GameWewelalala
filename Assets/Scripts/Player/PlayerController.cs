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

    [Header("Interaction Settings")]
    [SerializeField] private float interactRadius = 2f;

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

    [Header("Mana / Stamina System")]
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float manaChargeRate = 50f;
    [SerializeField] private float manaRunDrainRate = 10f; // Slower drain while running
    [SerializeField] private float manaClimbDrainRate = 15f; // Normal drain while climbing
    [SerializeField] private float climbSpeed = 3f; // Speed while climbing walls
    [SerializeField] private float manaIdleDrainRate = 30f; // Fast drain when not doing anything
    [SerializeField] private float manaRunSpeedMultiplier = 1.5f;
    [SerializeField] private float manaChargeWalkSpeedMultiplier = 0.5f;

    [Header("Mana UI Customization")]
    [SerializeField] private Vector2 manaBarSize = new Vector2(0.4f, 1.8f);
    [SerializeField] private Vector3 manaBarOffset = new Vector3(1f, 1.5f, 0f);
    [SerializeField] private Vector2 manaFillOffsetMin = new Vector2(0.04f, 0.08f); // Left, Bottom padding
    [SerializeField] private Vector2 manaFillOffsetMax = new Vector2(-0.04f, -0.08f); // Right, Top padding
    [SerializeField] private Sprite manaBorderSprite;
    [SerializeField] private Sprite manaBackgroundSprite;
    [SerializeField] private Sprite manaFillSprite;
    [SerializeField] private Color manaBorderColor = new Color(0.6f, 0.4f, 0.2f, 1f);
    [SerializeField] private Color manaBackgroundColor = new Color(0.2f, 0.15f, 0.2f, 1f);
    [SerializeField] private Color manaFillColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    private float currentMana = 0f;
    private bool isChargingMana = false;
    private bool canChargeMana = true;
    private bool chargeOverridesRun = false;
    private float targetManaAlpha = 0f;
    private float lastSpacePressTime = -999f;
    private bool isNearWall = false;
    private bool isAttachedToWall = false;
    private Vector3 currentWallNormal;
    private Vector3 climbMoveDirection;

    private CanvasGroup manaCanvasGroup;
    private UnityEngine.UI.Image manaFill;
    private TrailRenderer runTrail;

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

        CreateManaUI();
        CreateTrailRenderer();
    }

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
        moveDirection = Vector3.zero;
        isRunning = false;
        UpdateAnimations();
    }

    void Update()
    {
        HandleRunInput();
        HandleManaInput();

        if (!isChargingMana && currentMana > 0f)
        {
            if (isAttachedToWall)
            {
                if (!isNearWall)
                {
                    isAttachedToWall = false;
                    rb.useGravity = true;
                }
                else
                {
                    currentMana -= manaClimbDrainRate * Time.deltaTime;
                    if (currentMana <= 0) 
                    {
                        currentMana = 0f;
                        isAttachedToWall = false;
                        rb.useGravity = true;
                    }
                }
            }
            else if (isRunning && moveDirection.magnitude > 0.1f)
            {
                currentMana -= manaRunDrainRate * Time.deltaTime;
                if (currentMana <= 0) currentMana = 0f;
            }
            else
            {
                // Idle / doing nothing
                currentMana -= manaIdleDrainRate * Time.deltaTime;
                if (currentMana <= 0) currentMana = 0f;
            }
        }

        if (runTrail != null)
        {
            runTrail.emitting = (isRunning && currentMana > 0f);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }

        // Get input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        if (isAttachedToWall)
        {
            if (currentWallNormal != Vector3.zero)
            {
                // Move based on wall normal tangent
                Vector3 wallRight = Vector3.Cross(currentWallNormal, Vector3.up).normalized;
                climbMoveDirection = (Vector3.up * vertical + wallRight * horizontal).normalized;

                // Rotate to face the wall
                Quaternion targetRotation = Quaternion.LookRotation(-currentWallNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
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
        }

        // Jump and Wall climb input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isAttachedToWall)
            {
                // Detach and jump
                isAttachedToWall = false;
                rb.useGravity = true;
                jumpRequested = true;
                lastSpacePressTime = Time.time;
            }
            else
            {
                bool doubleTapped = (Time.time - lastSpacePressTime <= doubleTapWindow);
                
                if (isNearWall && currentMana > 0f && doubleTapped && !isGrounded && !isChargingMana)
                {
                    // Attach to wall
                    isAttachedToWall = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    lastSpacePressTime = -999f;
                }
                else
                {
                    if (isGrounded && Time.time >= lastJumpTime + jumpCooldown)
                    {
                        jumpRequested = true;
                        lastJumpTime = Time.time;
                    }
                    lastSpacePressTime = Time.time;
                }
            }
        }

        UpdateAnimations();
        UpdateManaUI();
    }

    private void TryInteract()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactRadius);
        foreach (Collider col in colliders)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact();
                break; // Only interact with one object
            }
        }
    }

    private void HandleRunInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            chargeOverridesRun = false;
        }

        // Double-tap W detection (W is always forward, so no extra check needed)
        if (Input.GetKeyDown(KeyCode.W))
        {
            if (Time.time - lastWPressTime <= doubleTapWindow)
            {
                isRunning = true;
                chargeOverridesRun = false;
                lastWPressTime = -999f;
            }
            else
            {
                lastWPressTime = Time.time;
            }
        }

        if (chargeOverridesRun)
        {
            isRunning = false;
            return;
        }

        // Shift held → run (only if moving forward)
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            float v = Input.GetAxisRaw("Vertical");
            if (v > 0f) isRunning = true;
            return;
        }

        // Stop running if Shift released and not double-tap running
        // (double-tap keeps isRunning until player stops, so we only clear it on stop-move above)

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
        isNearWall = false;

        // Ground check
        float castDistance = 0.1f;
        if (capsuleCollider != null)
            castDistance = (capsuleCollider.height / 2f) - groundCheckRadius + groundCheckDistance;

        isGrounded = Physics.SphereCast(transform.position, groundCheckRadius, Vector3.down, out RaycastHit hit, castDistance, groundLayer);

        if (isAttachedToWall)
        {
            rb.linearVelocity = climbMoveDirection * climbSpeed - currentWallNormal * 1f;
            return;
        }

        // Move player (use run speed if running)
        float currentSpeed = moveSpeed;
        if (isChargingMana)
        {
            currentSpeed = moveSpeed * manaChargeWalkSpeedMultiplier;
        }
        else if (isRunning)
        {
            currentSpeed = runSpeed;
            if (currentMana > 0f)
            {
                currentSpeed = runSpeed * manaRunSpeedMultiplier;
            }
        }
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
                isNearWall = true;
                currentWallNormal = contact.normal;

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

    private void HandleManaInput()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            chargeOverridesRun = true;
        }

        if (Input.GetKeyUp(KeyCode.G))
        {
            chargeOverridesRun = false;
            canChargeMana = true;
        }

        if (Input.GetKey(KeyCode.G) && canChargeMana && !isRunning)
        {
            isChargingMana = true;

            currentMana += manaChargeRate * Time.deltaTime;
            if (currentMana >= maxMana)
            {
                currentMana = maxMana;
                isChargingMana = false;
                canChargeMana = false; // Must release G to charge again
            }
        }
        else
        {
            isChargingMana = false;
        }

        if (isChargingMana || currentMana > 0f)
        {
            targetManaAlpha = 1f;
        }
        else
        {
            targetManaAlpha = 0f;
        }
    }

    private void UpdateManaUI()
    {
        if (manaCanvasGroup != null)
        {
            manaCanvasGroup.alpha = Mathf.MoveTowards(manaCanvasGroup.alpha, targetManaAlpha, Time.deltaTime * 5f);
            
            if (manaFill != null)
                manaFill.fillAmount = currentMana / maxMana;

            if (Camera.main != null)
            {
                manaCanvasGroup.transform.rotation = Quaternion.LookRotation(manaCanvasGroup.transform.position - Camera.main.transform.position);
            }
        }
    }

    private void CreateManaUI()
    {
        if (manaCanvasGroup != null) return;

        Texture2D whiteTex = Texture2D.whiteTexture;
        Sprite defaultSprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), Vector2.zero);

        GameObject canvasObj = new GameObject("ManaCanvas");
        canvasObj.transform.SetParent(this.transform);
        canvasObj.transform.localPosition = manaBarOffset; 
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = manaBarSize;
        rt.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        manaCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        manaCanvasGroup.alpha = 0f;

        GameObject bgObj = new GameObject("InnerBg");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = manaBackgroundColor;
        if (manaBackgroundSprite != null)
        {
            bgImage.sprite = manaBackgroundSprite;
            if (manaBackgroundSprite.border != Vector4.zero) bgImage.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else
        {
            bgImage.sprite = defaultSprite;
        }
        
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = manaFillOffsetMin;
        bgRect.offsetMax = manaFillOffsetMax;

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(canvasObj.transform, false);
        manaFill = fillObj.AddComponent<UnityEngine.UI.Image>();
        manaFill.color = manaFillColor;
        
        if (manaFillSprite != null) 
            manaFill.sprite = manaFillSprite;
        else 
            manaFill.sprite = defaultSprite;
        
        manaFill.type = UnityEngine.UI.Image.Type.Filled;
        manaFill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical; 
        manaFill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
        manaFill.fillAmount = 0f;
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = manaFillOffsetMin;
        fillRect.offsetMax = manaFillOffsetMax;

        GameObject borderObj = new GameObject("BorderBg");
        borderObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image borderImage = borderObj.AddComponent<UnityEngine.UI.Image>();
        borderImage.color = manaBorderColor;
        if (manaBorderSprite != null)
        {
            borderImage.sprite = manaBorderSprite;
            if (manaBorderSprite.border != Vector4.zero) borderImage.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else
        {
            borderImage.sprite = defaultSprite;
        }
        
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;
    }

    private void CreateTrailRenderer()
    {
        if (runTrail != null) return;

        GameObject trailObj = new GameObject("RunTrail");
        trailObj.transform.SetParent(this.transform);
        
        Vector3 trailPos = new Vector3(0, 1f, 0);
        if (capsuleCollider != null) trailPos = capsuleCollider.center;
        trailObj.transform.localPosition = trailPos; 
        
        runTrail = trailObj.AddComponent<TrailRenderer>();
        runTrail.time = 0.4f;
        runTrail.startWidth = 0.6f;
        runTrail.endWidth = 0f;
        runTrail.material = new Material(Shader.Find("Sprites/Default"));
        runTrail.startColor = new Color(0f, 0.8f, 1f, 0.5f);
        runTrail.endColor = new Color(0f, 0.8f, 1f, 0f);
        runTrail.emitting = false;
        runTrail.minVertexDistance = 0.1f;
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
