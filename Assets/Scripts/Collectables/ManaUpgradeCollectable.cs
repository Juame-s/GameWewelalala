using UnityEngine;

/// <summary>
/// A collectible that permanently upgrades the player's mana-related rates.
/// Attach to a GameObject with a Trigger Collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ManaUpgradeCollectable : MonoBehaviour, IInteractable
{
    [Header("Mana Upgrade Settings")]
    [Tooltip("How much to add to manaChargeRate (units/s). Use 0 to skip.")]
    [SerializeField] private float chargeRateBonus = 10f;

    [Tooltip("How much to SUBTRACT from manaRunDrainRate (units/s). Use 0 to skip.")]
    [SerializeField] private float runDrainReduction = 2f;

    [Tooltip("How much to SUBTRACT from manaClimbDrainRate (units/s). Use 0 to skip.")]
    [SerializeField] private float climbDrainReduction = 3f;

    [Tooltip("How much to ADD to the snow melt radius when charging mana. Use 0 to skip.")]
    [SerializeField] private float meltRadiusBonus = 1f;


    [Header("Pickup Settings")]
    [SerializeField] private bool destroyOnCollect = true;

    [Header("Optional Feedback")]
    [SerializeField] private GameObject collectEffect; // Particle / VFX prefab to spawn on pickup

    [Header("Collect Flash Effect")]
    [Tooltip("Icon shown on screen when this collectible is picked up.")]
    [SerializeField] private Sprite collectIcon;
    [Tooltip("Text shown on screen when this collectible is picked up.")]
    [SerializeField] private string collectLabel = "Mana Upgraded!";

    [Header("Ghost Objects")]
    [Tooltip("GhostObject components that will become visible (ghostly) when this collectible is picked up.")]
    [SerializeField] private GhostObject[] ghostsToReveal;

    [Header("Camera Pan (Cinematic)")]
    [Tooltip("If assigned, the camera will temporarily pan to this point when collected.")]
    [SerializeField] private Transform cameraPanTarget;
    [Tooltip("How long the camera stays focused on the pan target before returning to the player.")]
    [SerializeField] private float cameraPanDuration = 2.0f;
    [Tooltip("How fast the camera pans. Lower is slower/smoother.")]
    [SerializeField] private float cameraPanSpeed = 2.0f;

    private bool isCollected = false;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("ManaUpgradeCollectable: Collider should be set to Trigger for automatic pickup.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    public void Interact()
    {
        // Support manual E-key interaction via the IInteractable interface
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            Collect(player.gameObject);
    }

    private void Collect(GameObject playerObject)
    {
        if (isCollected) return;
        isCollected = true;

        PlayerController player = playerObject.GetComponent<PlayerController>();
        if (player == null)
            player = playerObject.GetComponentInChildren<PlayerController>();

        if (player != null)
        {
            player.ApplyManaUpgrade(chargeRateBonus, runDrainReduction, climbDrainReduction, meltRadiusBonus);
            Debug.Log($"[ManaUpgrade] Applied → ChargeRate+{chargeRateBonus}  RunDrain-{runDrainReduction}  ClimbDrain-{climbDrainReduction}  MeltRadius+{meltRadiusBonus}");
        }
        else
        {
            Debug.LogError("ManaUpgradeCollectable: Could not find PlayerController on the player object.");
        }

        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        if (CollectFlashEffect.Instance != null)
            CollectFlashEffect.Instance.Play(collectIcon, collectLabel);

        if (ghostsToReveal != null)
            foreach (var ghost in ghostsToReveal)
                if (ghost != null) ghost.SetGhost(true);

        if (cameraPanTarget != null)
        {
            CameraFollow cam = Camera.main.GetComponent<CameraFollow>();
            if (cam == null) cam = FindFirstObjectByType<CameraFollow>();
            
            if (cam != null)
                cam.PanToTemporaryTarget(cameraPanTarget, cameraPanDuration, 0.1f, cameraPanSpeed);
        }

        if (destroyOnCollect)
            Destroy(gameObject);
    }
}
