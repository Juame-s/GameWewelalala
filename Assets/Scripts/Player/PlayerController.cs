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

    // Dialogue state — set by DialogueManager, blocks input but keeps physics alive
    public bool IsInDialogue { get; set; }

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Mana / Stamina System")]
    [Tooltip("Set true in the Inspector to start with mana already unlocked (useful for testing).")]
    [SerializeField] private bool manaUnlocked = false;
    [SerializeField] private float maxMana = 100f;
    [SerializeField] private float manaChargeRate = 50f;
    [SerializeField] private float manaRunDrainRate = 10f; // Slower drain while running
    [SerializeField] private float manaClimbDrainRate = 15f; // Normal drain while climbing
    [SerializeField] private float climbSpeed = 3f; // Speed while climbing walls
    [SerializeField] private float manaIdleDrainRate = 30f; // Fast drain when not doing anything
    [SerializeField] private float manaRunSpeedMultiplier = 1.5f;
    [SerializeField] private float manaChargeWalkSpeedMultiplier = 0.5f;

    [Tooltip("Assign a ParticleSystem to emit while the player runs with mana.")]
    [SerializeField] private ParticleSystem manaRunParticles;

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

    [Header("Fall System")]
    [SerializeField] private float heavyFallThreshold = 2f;       // Seconds of freefall before locking wall climb
    [SerializeField] private float recoveryDuration = 0.6f;       // Seconds player is stunned after hard landing
    [Tooltip("Minimum fall time (seconds) needed to trigger the wall-grab drag.")]
    [SerializeField] private float wallGrabDragFallThreshold = 0.4f;
    [Tooltip("How long the player is dragged down after grabbing a wall mid-fall.")]
    [SerializeField] private float wallGrabDragDuration = 0.5f;
    [Tooltip("Downward speed during the wall-grab drag.")]
    [SerializeField] private float wallGrabDragSpeed = 3f;
    [Tooltip("Looping ParticleSystem that plays while the player is in heavy freefall.")]
    [SerializeField] private ParticleSystem heavyFallParticles;
    [Tooltip("One-shot ParticleSystem that fires when the player slams into the ground.")]
    [SerializeField] private ParticleSystem heavyLandParticles;
    [Tooltip("Looping ParticleSystem that plays while the player is being dragged down the wall.")]
    [SerializeField] private ParticleSystem wallGrabDragParticles;

    private float fallTimer = 0f;             // How long the player has been falling
    private bool isHeavyFalling = false;      // True once fallTimer > heavyFallThreshold
    private bool isRecovering = false;        // True right after a hard landing
    private float recoveryTimer = 0f;
    private bool isWallDragging = false;      // True while the wall-grab drag is active
    private float wallDragTimer = 0f;

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

    /// <summary>Called by DialogueManager to freeze input while keeping physics running.</summary>
    public void SetDialogueMode(bool inDialogue)
    {
        IsInDialogue = inDialogue;
        if (inDialogue)
        {
            // Stop horizontal movement immediately but keep gravity
            if (rb != null)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            moveDirection = Vector3.zero;
            isRunning = false;
            UpdateAnimations();
        }
    }

    void Update()
    {
        // Mana drain always runs, even during dialogue
        if (manaUnlocked && !isChargingMana && currentMana > 0f)
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

        UpdateManaUI(); // Always update the bar so it reflects drain during dialogue too

        // ── Fall tracking ──────────────────────────────────────────────────────
        bool isFalling = !isGrounded && !isAttachedToWall && rb.linearVelocity.y < -0.1f;

        if (isFalling)
        {
            fallTimer += Time.deltaTime;
            if (fallTimer >= heavyFallThreshold)
                isHeavyFalling = true;
        }
        else if (isGrounded)
        {
            if (isHeavyFalling)
            {
                // Hard landing — enter recovery
                isRecovering = true;
                recoveryTimer = recoveryDuration;
                OnHeavyLanding();
            }
            fallTimer = 0f;
            isHeavyFalling = false;
        }

        // Recovery countdown
        if (isRecovering)
        {
            recoveryTimer -= Time.deltaTime;
            if (recoveryTimer <= 0f)
                isRecovering = false;
        }

        // ── Fall particles ─────────────────────────────────────────────────────
        SetParticleEmitting(heavyFallParticles, isHeavyFalling);

        if (IsInDialogue || isRecovering) return; // Inputs blocked during dialogue or recovery

        HandleRunInput();
        if (manaUnlocked) HandleManaInput();

        // ── Mana Particles ─────────────────────────────────────────────────────
        bool wantRunParticles = manaUnlocked && isRunning && currentMana > 0f;

        SetParticleEmitting(manaRunParticles, wantRunParticles);

        // Legacy trail (only active if no inspector particle assigned)
        if (runTrail != null)
        {
            runTrail.emitting = wantRunParticles && manaRunParticles == null;
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
                
                if (manaUnlocked && isNearWall && currentMana > 0f && doubleTapped && !isGrounded && !isChargingMana && !isHeavyFalling && !isRecovering)
                {
                    // Attach to wall
                    isAttachedToWall = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    lastSpacePressTime = -999f;

                    // If falling long enough, trigger the drag-down penalty
                    if (fallTimer >= wallGrabDragFallThreshold)
                    {
                        isWallDragging = true;
                        wallDragTimer = wallGrabDragDuration;
                    }
                    fallTimer = 0f;
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

    /// <summary>
    /// Called by ManaUpgradeCollectable to permanently boost mana-related rates.
    /// Pass 0 for any value you don't want to change.
    /// </summary>
    /// <param name="chargeRateBonus">Added to manaChargeRate.</param>
    /// <param name="runDrainReduction">Subtracted from manaRunDrainRate (clamped to 0).</param>
    /// <param name="climbDrainReduction">Subtracted from manaClimbDrainRate (clamped to 0).</param>
    public void ApplyManaUpgrade(float chargeRateBonus, float runDrainReduction, float climbDrainReduction)
    {
        manaChargeRate   = Mathf.Max(0f, manaChargeRate   + chargeRateBonus);
        manaRunDrainRate  = Mathf.Max(0f, manaRunDrainRate  - runDrainReduction);
        manaClimbDrainRate = Mathf.Max(0f, manaClimbDrainRate - climbDrainReduction);
    }

    /// <summary>
    /// Called by ManaUnlockCollectable. Enables the entire mana system for this player.
    /// </summary>
    public void UnlockMana()
    {
        if (manaUnlocked) return;
        manaUnlocked = true;
        Debug.Log("[Mana] Mana system unlocked!");
    }

    /// <summary>Returns whether the mana system has been unlocked.</summary>
    public bool IsManaUnlocked => manaUnlocked;

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
            if (isWallDragging)
            {
                // Drag the player downward — they can't freely climb yet
                wallDragTimer -= Time.fixedDeltaTime;
                if (wallDragTimer <= 0f)
                    isWallDragging = false;

                // Slide down while pressing into wall
                rb.linearVelocity = new Vector3(0f, -wallGrabDragSpeed, 0f) - currentWallNormal * 1f;
            }
            else
            {
                rb.linearVelocity = climbMoveDirection * climbSpeed - currentWallNormal * 1f;
            }

            SetParticleEmitting(wallGrabDragParticles, isWallDragging);
            return;
        }

        // Freeze movement completely during landing recovery
        if (isRecovering)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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
            if (manaUnlocked && currentMana > 0f)
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

    /// <summary>Starts or stops a ParticleSystem without calling Play/Stop every frame.</summary>
    private void SetParticleEmitting(ParticleSystem ps, bool shouldEmit)
    {
        if (ps == null) return;
        if (shouldEmit && !ps.isEmitting) ps.Play();
        else if (!shouldEmit && ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
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

        if (Input.GetKey(KeyCode.G) && canChargeMana && !isRunning && !isAttachedToWall)
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


    private void OnHeavyLanding()
    {
        // Fire the one-shot landing burst
        if (heavyLandParticles != null) heavyLandParticles.Play();

        // TODO: animator.SetTrigger("HeavyLand");
        // TODO: CameraShake.Instance.Shake(0.3f, 0.15f);
        Debug.Log("[PlayerController] Heavy landing detected – recovery started.");
    }
}
