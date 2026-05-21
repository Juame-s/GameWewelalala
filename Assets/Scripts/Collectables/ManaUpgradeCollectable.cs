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

        if (destroyOnCollect)
            Destroy(gameObject);
    }
}
