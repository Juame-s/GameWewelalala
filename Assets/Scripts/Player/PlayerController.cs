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

    [Header("Mana Sounds")]
    [Tooltip("Plays at a regular interval while the player is actively charging mana.")]
    [SerializeField] private AudioClip manaChargeTickClip;
    [Tooltip("How often (in seconds) the tick sound fires while charging.")]
    [SerializeField] private float manaChargeTickInterval = 0.5f;
    [Tooltip("Plays once when the mana bar reaches 100%.")]
    [SerializeField] private AudioClip manaChargeFullClip;
    [SerializeField] [Range(0f, 1f)] private float manaSoundVolume = 0.7f;

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
    private float chargeTickTimer = 0f;      // Accumulates time while charging for tick sound
    private float lastSpacePressTime = -999f;
    private bool isNearWall = false;
    private bool isAttachedToWall = false;
    private Vector3 currentWallNormal;
    private Vector3 climbMoveDirection;

    [Header("Wall Jump Skill")]
    [SerializeField] private bool wallJumpUnlocked = false;
    [SerializeField] private float wallJumpForce = 15f;
    [SerializeField] private float wallJumpUpForce = 10f;
    [SerializeField] private float wallJumpControlLockTime = 0.3f;
    [Tooltip("How long (seconds) the player stays attached to a wall after losing contact, to bridge corner gaps.")]
    [SerializeField] private float wallCornerGraceDuration = 0.12f;
    [Tooltip("How long mana drain is paused after performing a wall jump.")]
    [SerializeField] private float wallJumpManaPauseDuration = 1f;
    private bool isWallJumping = false;
    private float wallJumpTimer = 0f;
    private float wallCornerGraceTimer = 0f;
    private float wallJumpManaPauseTimer = 0f;

    [Header("Snow Interaction")]
    [SerializeField] private float snowTrailRadius = 0.5f;
    [SerializeField] private float baseSnowMeltRadius = 3f;
    [SerializeField] private float snowInteractionAmount = 5f; // Intensity of melting
    private float bonusSnowMeltRadius = 0f;



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

    [Header("Rope Walking")]
    [SerializeField] private Vector2 ropeBarSize = new Vector2(4f, 0.4f);
    [SerializeField] private Vector3 ropeBarOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private float ropeMoveSpeed = 4f; 
    [SerializeField] private float ropeBalanceControlPower = 1500f; // Increased so the notch moves very fast when commanded
    [SerializeField] private float ropeMaxTiltAngle = 40f;
    [SerializeField] private float maxRopeEntryDistance = 3f;
    
    [Header("Ziplining")]
    [SerializeField] private float ziplineSpeed = 20f;
    [SerializeField] private float minZiplineSlope = 2f;
    [SerializeField] private float ziplineHangOffset = 1.2f;
    
    [Header("Rope UI Customization")]
    [SerializeField] private Sprite ropeBarBgSprite;
    [SerializeField] private Sprite ropeCenterSprite;
    [SerializeField] private Sprite ropeNotchSprite;
    [SerializeField] private Color ropeBarBgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Color ropeCenterColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color ropeNotchColorSafe = Color.white;
    [SerializeField] private Color ropeNotchColorDanger = Color.red;
    [SerializeField] private float ropeNotchWidth = 0.2f;
    [SerializeField] private float ropeCenterWidth = 0.4f;
    
    public bool IsRopeWalking { get; private set; }
    public bool IsZiplining { get; private set; }
    private RopeWalkable currentRope;
    private float ropeProgress = 0f;
    private Vector3 currentRopeDir;
    private float balanceValue = 0f;
    private float balanceVelocity = 0f;
    private CanvasGroup ropeCanvasGroup;
    private RectTransform ropeNotch;
    private float targetRopeUIAlpha = 0f;

    private CanvasGroup manaCanvasGroup;
    private UnityEngine.UI.Image manaFill;
    private TrailRenderer runTrail;
    private AudioSource audioSource;

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
        CreateRopeUI();
        CreateTrailRenderer();

        // Grab or create an AudioSource for mana sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound
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
        // Tick wall jump mana pause timer
        if (wallJumpManaPauseTimer > 0f)
            wallJumpManaPauseTimer -= Time.deltaTime;

        // Mana drain always runs, even during dialogue
        if (manaUnlocked && !isChargingMana && currentMana > 0f && wallJumpManaPauseTimer <= 0f)
        {
            if (isAttachedToWall)
            {
                if (!isNearWall && wallCornerGraceTimer <= 0f)
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
        UpdateRopeUI();

        if (IsRopeWalking)
        {
            HandleRopeWalking();
            UpdateAnimations();
            return;
        }
        
        if (IsZiplining)
        {
            HandleZiplining();
            UpdateAnimations();
            return;
        }

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
            isWallJumping = false;
            wallJumpTimer = 0f;
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

        if (isWallJumping && isNearWall && manaUnlocked && currentMana > 0f && !isGrounded && !isHeavyFalling && !isRecovering)
        {
            if (wallJumpTimer < wallJumpControlLockTime - 0.1f)
            {
                isAttachedToWall = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                isWallJumping = false;
                wallJumpTimer = 0f;

                if (fallTimer >= wallGrabDragFallThreshold)
                {
                    isWallDragging = true;
                    wallDragTimer = wallGrabDragDuration;
                }
                fallTimer = 0f;
            }
        }

        HandleRunInput();
        if (manaUnlocked) HandleManaInput();

        // ── Mana Particles ─────────────────────────────────────────────────────
        bool wantRunParticles = manaUnlocked && isRunning && currentMana > 0f;

        SetParticleEmitting(manaRunParticles, wantRunParticles);

        if (VolumetricSnowManager.Instance != null)
        {
            if (isGrounded && !isAttachedToWall)
            {
                // Leave a trail where walking
                VolumetricSnowManager.Instance.AddImpulse(transform.position, snowTrailRadius, snowInteractionAmount * Time.deltaTime);
            }
            if (isChargingMana)
            {
                // Melt area while charging
                float currentMeltRadius = baseSnowMeltRadius + bonusSnowMeltRadius;
                VolumetricSnowManager.Instance.AddImpulse(transform.position, currentMeltRadius, snowInteractionAmount * 2f * Time.deltaTime);
            }
        }



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

        // Wall Jump input
        if (isAttachedToWall && wallJumpUnlocked && Input.GetKeyDown(KeyCode.Q) && !isWallDragging && currentMana >= 10f)
        {
            currentMana -= 10f;
            isAttachedToWall = false;
            rb.useGravity = true;
            isWallJumping = true;
            wallJumpTimer = wallJumpControlLockTime;
            wallJumpManaPauseTimer = wallJumpManaPauseDuration; // Pause mana drain for a moment

            transform.rotation = Quaternion.LookRotation(currentWallNormal);
            rb.linearVelocity = currentWallNormal * wallJumpForce + Vector3.up * wallJumpUpForce;
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
                
                if (doubleTapped && !isGrounded && !isChargingMana && !isHeavyFalling && !isRecovering)
                {
                    if (TryStartZipline())
                    {
                        lastSpacePressTime = -999f;
                        return;
                    }
                }
                
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
    public void ApplyManaUpgrade(float chargeRateBonus, float runDrainReduction, float climbDrainReduction, float meltRadiusBonus = 0f)
    {
        manaChargeRate   = Mathf.Max(0f, manaChargeRate   + chargeRateBonus);
        manaRunDrainRate  = Mathf.Max(0f, manaRunDrainRate  - runDrainReduction);
        manaClimbDrainRate = Mathf.Max(0f, manaClimbDrainRate - climbDrainReduction);
        bonusSnowMeltRadius += meltRadiusBonus;
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

    public void UnlockWallJump()
    {
        wallJumpUnlocked = true;
    }

    public bool IsWallJumpUnlocked => wallJumpUnlocked;

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
        
        // Optional Zipline pose:
        // if (IsZiplining) animator.Play("ZiplinePose"); 
    }

    void FixedUpdate()
    {
        isNearWall = false;

        // Tick down the corner grace timer
        if (wallCornerGraceTimer > 0f)
            wallCornerGraceTimer -= Time.fixedDeltaTime;

        // While attached, probe ahead with a raycast to find the next wall face at corners
        if (isAttachedToWall && climbMoveDirection.magnitude > 0.1f)
        {
            float probeRadius = capsuleCollider != null ? capsuleCollider.radius + 0.1f : 0.4f;
            Vector3 probeDir = new Vector3(climbMoveDirection.x, 0f, climbMoveDirection.z).normalized;
            if (probeDir.magnitude > 0.05f &&
                Physics.SphereCast(transform.position, probeRadius, probeDir,
                    out RaycastHit cornerHit, 0.6f, groundLayer))
            {
                float hitNormalY = cornerHit.normal.y;
                if (Mathf.Abs(hitNormalY) < 0.5f)
                {
                    // Found an adjacent wall face — switch to it seamlessly
                    isNearWall = true;
                    currentWallNormal = cornerHit.normal;
                }
            }
        }

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

        if (wallJumpTimer > 0f)
        {
            wallJumpTimer -= Time.fixedDeltaTime;
        }
        else
        {
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
        }

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

    void OnCollisionEnter(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            float normalY = contact.normal.y;
            if (Mathf.Abs(normalY) < 0.5f)
            {
                // Touching a new wall face — refresh the corner grace window
                wallCornerGraceTimer = wallCornerGraceDuration;
                if (isAttachedToWall)
                {
                    // Seamlessly pivot to the new face
                    currentWallNormal = contact.normal;
                    isNearWall = true;
                }
                break;
            }
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
                wallCornerGraceTimer = wallCornerGraceDuration; // keep refreshing while in contact

                if (!isWallJumping)
                {
                    Vector3 pushDirection = contact.normal;
                    pushDirection.y = 0;
                    pushDirection.Normalize();

                    Vector3 velocity = rb.linearVelocity;
                    velocity.x = pushDirection.x * 0.5f;
                    velocity.z = pushDirection.z * 0.5f;
                    rb.linearVelocity = velocity;
                }

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

            // Tick sound every manaChargeTickInterval seconds
            chargeTickTimer += Time.deltaTime;
            if (chargeTickTimer >= manaChargeTickInterval)
            {
                chargeTickTimer -= manaChargeTickInterval;
                if (manaChargeTickClip != null && audioSource != null)
                    audioSource.PlayOneShot(manaChargeTickClip, manaSoundVolume);
            }

            if (currentMana >= maxMana)
            {
                currentMana = maxMana;
                isChargingMana = false;
                canChargeMana = false; // Must release G to charge again
                chargeTickTimer = 0f;

                if (manaChargeFullClip != null && audioSource != null)
                    audioSource.PlayOneShot(manaChargeFullClip, manaSoundVolume);
            }
        }
        else
        {
            isChargingMana = false;
            chargeTickTimer = 0f; // Reset tick so next charge starts fresh
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

    // ── Rope Walking System ──────────────────────────────────────────────────

    public void StartRopeWalking(RopeWalkable rope)
    {
        if (isAttachedToWall || IsInDialogue || isRecovering || IsRopeWalking || IsZiplining) return;
        
        // Only start rope walking if falling or walking horizontally onto it, not jumping up into it
        if (rb.linearVelocity.y > 0.1f) return;
        
        // Only allow starting the minigame if the player is near one of the anchor points
        float distToStart = Vector3.Distance(transform.position, rope.startPoint.position);
        float distToEnd = Vector3.Distance(transform.position, rope.endPoint.position);
        
        if (distToStart > maxRopeEntryDistance && distToEnd > maxRopeEntryDistance)
        {
            return; // Player is too far from the start or end points (e.g. jumped into the middle)
        }
        
        IsRopeWalking = true;
        currentRope = rope;
        
        Vector3 startPos = rope.startPoint.position;
        Vector3 endPos = rope.endPoint.position;
        Vector3 ropeDir = (endPos - startPos).normalized;
        
        // Determine the "forward" direction relative to the camera
        float dotCamera = 1f;
        if (Camera.main != null)
        {
            dotCamera = Vector3.Dot(Camera.main.transform.forward, ropeDir);
        }
        currentRopeDir = dotCamera > 0 ? ropeDir : -ropeDir;
        
        Vector3 playerToStart = transform.position - startPos;
        ropeProgress = Mathf.Clamp(Vector3.Dot(playerToStart, ropeDir), 0f, rope.RopeLength);
        
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        
        balanceValue = 0f;
        balanceVelocity = Random.Range(-10f, 10f); // Initial nudge
        
        targetRopeUIAlpha = 1f;
        
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
        }
    }

    private void HandleRopeWalking()
    {
        if (currentRope == null) 
        { 
            EndRopeWalking(); 
            return; 
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            EndRopeWalking();
            jumpRequested = true;
            return;
        }

        Vector3 startPos = currentRope.startPoint.position;
        Vector3 endPos = currentRope.endPoint.position;
        Vector3 ropeDir = (endPos - startPos).normalized;
        float ropeLen = currentRope.RopeLength;

        // 1. Move along rope
        float vertical = Input.GetAxisRaw("Vertical");
        float moveSign = Vector3.Dot(currentRopeDir, ropeDir) > 0 ? 1f : -1f;
        
        ropeProgress += vertical * moveSign * ropeMoveSpeed * Time.deltaTime;
        
        if (ropeProgress < 0f || ropeProgress > ropeLen)
        {
            EndRopeWalking();
            return;
        }

        Vector3 currentPos = currentRope.GetRopePoint(ropeProgress);
        transform.position = currentPos;
        
        // Face the movement direction along the curve tangent
        if (Mathf.Abs(vertical) > 0.1f)
        {
            float testProgress = ropeProgress + moveSign * 0.1f;
            // Clamp to ensure we don't go out of bounds for the tangent test
            testProgress = Mathf.Clamp(testProgress, 0f, ropeLen);
            
            Vector3 nextPos = currentRope.GetRopePoint(testProgress);
            Vector3 tangent = (nextPos - currentPos).normalized;
            
            // If the player is at the very edge, the tangent might be zero. Fallback to currentRopeDir.
            if (tangent == Vector3.zero) tangent = currentRopeDir;

            Vector3 lookDir = vertical > 0 ? tangent : -tangent;
            if (lookDir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(lookDir);
                // Tilt the character based on balance value (-100 to 100)
                float tiltAngle = (balanceValue / 100f) * -ropeMaxTiltAngle; 
                Quaternion tiltRot = Quaternion.AngleAxis(tiltAngle, Vector3.forward);
                
                Quaternion targetRot = lookRot * tiltRot;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        // 2. Balancing
        float instability = currentRope.baseInstability * (1f + ropeLen * 0.05f);
        
        // Gravity pulls the notch further away from center (Slightly reduced so you don't fight a wall)
        float gravityPull = balanceValue * instability * 0.2f;
        
        // Unpredictable wobble (Reduced so it doesn't pin you)
        float wobble = (Mathf.PerlinNoise(Time.time * 2f, 0f) * 2f - 1f); // Range -1 to 1
        gravityPull += wobble * instability * 2f;
        
        // Add minimum drift if perfectly centered
        if (Mathf.Abs(balanceValue) < 5f)
        {
            gravityPull += Mathf.Sign(balanceValue != 0 ? balanceValue : Random.Range(-1f, 1f)) * instability;
        }
        
        balanceVelocity += gravityPull * Time.deltaTime;

        // Player inputs A/D to counter (inverted from before)
        float horizontal = Input.GetAxisRaw("Horizontal");
        balanceVelocity += horizontal * ropeBalanceControlPower * Time.deltaTime;

        // Friction to prevent infinite velocity (Lowered so the notch can actually build speed)
        balanceVelocity = Mathf.Lerp(balanceVelocity, 0f, Time.deltaTime * 3f);
        
        balanceValue += balanceVelocity * Time.deltaTime;

        // 3. Fall condition
        if (Mathf.Abs(balanceValue) > 100f)
        {
            FallFromRope();
        }
    }

    private void EndRopeWalking()
    {
        IsRopeWalking = false;
        currentRope = null;
        rb.useGravity = true;
        targetRopeUIAlpha = 0f;
    }

    private void FallFromRope()
    {
        EndRopeWalking();
        // Give a slight push sideways depending on which way they fell
        Vector3 fallDir = transform.right * Mathf.Sign(balanceValue);
        rb.linearVelocity = fallDir * 2f;
    }
    
    // ── Zipline System ───────────────────────────────────────────────────────

    private bool TryStartZipline()
    {
        // Check for ropes above the player in a 4 unit radius
        Collider[] hits = Physics.OverlapSphere(transform.position + Vector3.up * 2f, 4f);
        RopeWalkable bestRope = null;
        float closestDist = float.MaxValue;
        
        foreach (var hit in hits)
        {
            RopeWalkable rw = hit.GetComponent<RopeWalkable>();
            if (rw != null)
            {
                // Must have a noticeable slope to zipline
                if (Mathf.Abs(rw.startPoint.position.y - rw.endPoint.position.y) < minZiplineSlope) continue;
                
                float dist = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestRope = rw;
                }
            }
        }
        
        if (bestRope != null)
        {
            StartZipline(bestRope);
            return true;
        }
        return false;
    }

    private void StartZipline(RopeWalkable rope)
    {
        IsZiplining = true;
        currentRope = rope;
        
        Vector3 startPos = rope.startPoint.position;
        Vector3 endPos = rope.endPoint.position;
        Vector3 ropeDir = (endPos - startPos).normalized;
        Vector3 playerToStart = transform.position - startPos;
        ropeProgress = Mathf.Clamp(Vector3.Dot(playerToStart, ropeDir), 0f, rope.RopeLength);
        
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        
        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);
        }
    }

    private void HandleZiplining()
    {
        if (currentRope == null) { EndZiplining(); return; }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            EndZiplining();
            jumpRequested = true;
            return;
        }
        
        Vector3 startPos = currentRope.startPoint.position;
        Vector3 endPos = currentRope.endPoint.position;
        float ropeLen = currentRope.RopeLength;
        
        // Always move from highest to lowest point
        float moveSign = startPos.y > endPos.y ? 1f : -1f;
        
        ropeProgress += moveSign * ziplineSpeed * Time.deltaTime;
        
        if (ropeProgress < 0f || ropeProgress > ropeLen)
        {
            EndZiplining();
            return;
        }
        
        // Suspend the player *below* the rope slightly
        Vector3 currentPos = currentRope.GetRopePoint(ropeProgress);
        transform.position = currentPos - Vector3.up * ziplineHangOffset;
        
        // Face the zipline direction
        Vector3 ropeDir = (endPos - startPos).normalized;
        Vector3 lookDir = moveSign > 0 ? ropeDir : -ropeDir;
        lookDir.y = 0; // Flatten look dir
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), rotationSpeed * Time.deltaTime);
        }
    }

    private void EndZiplining()
    {
        IsZiplining = false;
        currentRope = null;
        rb.useGravity = true;
        // Apply slight forward momentum when getting off zipline
        rb.linearVelocity = transform.forward * ziplineSpeed * 0.5f;
    }

    private void CreateRopeUI()
    {
        if (ropeCanvasGroup != null) return;

        Texture2D whiteTex = Texture2D.whiteTexture;
        Sprite defaultSprite = Sprite.Create(whiteTex, new Rect(0, 0, whiteTex.width, whiteTex.height), Vector2.zero);

        GameObject canvasObj = new GameObject("RopeCanvas");
        canvasObj.transform.SetParent(this.transform);
        canvasObj.transform.localPosition = ropeBarOffset; 
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = ropeBarSize;
        rt.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        ropeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        ropeCanvasGroup.alpha = 0f;

        // Bar background
        GameObject bgObj = new GameObject("RopeBarBg");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImage = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = ropeBarBgColor;
        if (ropeBarBgSprite != null)
        {
            bgImage.sprite = ropeBarBgSprite;
            if (ropeBarBgSprite.border != Vector4.zero) bgImage.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else
        {
            bgImage.sprite = defaultSprite;
        }
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

        // Center Indicator (Target zone)
        GameObject centerObj = new GameObject("RopeCenter");
        centerObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image centerImage = centerObj.AddComponent<UnityEngine.UI.Image>();
        centerImage.color = ropeCenterColor;
        if (ropeCenterSprite != null)
        {
            centerImage.sprite = ropeCenterSprite;
            if (ropeCenterSprite.border != Vector4.zero) centerImage.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else
        {
            centerImage.sprite = defaultSprite;
        }
        RectTransform centerRect = centerObj.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0f);
        centerRect.anchorMax = new Vector2(0.5f, 1f);
        centerRect.sizeDelta = new Vector2(ropeCenterWidth, 0f);
        centerRect.anchoredPosition = Vector2.zero;

        // Notch
        GameObject notchObj = new GameObject("RopeNotch");
        notchObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image notchImage = notchObj.AddComponent<UnityEngine.UI.Image>();
        notchImage.color = ropeNotchColorSafe;
        if (ropeNotchSprite != null)
        {
            notchImage.sprite = ropeNotchSprite;
            if (ropeNotchSprite.border != Vector4.zero) notchImage.type = UnityEngine.UI.Image.Type.Sliced;
        }
        else
        {
            notchImage.sprite = defaultSprite;
        }
        ropeNotch = notchObj.GetComponent<RectTransform>();
        ropeNotch.anchorMin = new Vector2(0.5f, 0f);
        ropeNotch.anchorMax = new Vector2(0.5f, 1f);
        ropeNotch.sizeDelta = new Vector2(ropeNotchWidth, 0f); 
        ropeNotch.anchoredPosition = Vector2.zero;
    }

    private void UpdateRopeUI()
    {
        if (ropeCanvasGroup != null)
        {
            ropeCanvasGroup.alpha = Mathf.MoveTowards(ropeCanvasGroup.alpha, targetRopeUIAlpha, Time.deltaTime * 5f);
            
            if (ropeCanvasGroup.alpha > 0f)
            {
                // Balance value is -100 to 100.
                float maxOffset = ropeBarSize.x / 2f;
                float currentOffset = (balanceValue / 100f) * maxOffset;
                
                if (ropeNotch != null)
                {
                    ropeNotch.anchoredPosition = new Vector2(currentOffset, 0f);
                    
                    // Color shifts to danger color as it gets closer to edges
                    UnityEngine.UI.Image notchImg = ropeNotch.GetComponent<UnityEngine.UI.Image>();
                    if (notchImg != null)
                    {
                        float danger = Mathf.Abs(balanceValue) / 100f;
                        notchImg.color = Color.Lerp(ropeNotchColorSafe, ropeNotchColorDanger, danger);
                    }
                }
                
                if (Camera.main != null)
                {
                    ropeCanvasGroup.transform.rotation = Quaternion.LookRotation(ropeCanvasGroup.transform.position - Camera.main.transform.position);
                }
            }
        }
    }
}
