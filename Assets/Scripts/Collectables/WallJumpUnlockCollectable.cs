using UnityEngine;

/// <summary>
/// A one-time collectible that unlocks the player's wall jump skill.
/// Attach to a GameObject with a Trigger Collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WallJumpUnlockCollectable : MonoBehaviour, IInteractable
{
    [Header("Pickup Settings")]
    [SerializeField] private bool destroyOnCollect = true;

    [Header("Optional Feedback")]
    [Tooltip("Particle / VFX prefab spawned at the collectible's position on pickup.")]
    [SerializeField] private GameObject collectEffect;

    private bool isCollected = false;

    private void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("WallJumpUnlockCollectable: Collider should be set to Trigger for automatic pickup.");
        }
    }

    // Auto-pickup on contact
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Collect(other.gameObject);
        }
    }

    // Manual E-key pickup via IInteractable
    public void Interact()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            Collect(player.gameObject);
    }

    private void Collect(GameObject playerObject)
    {
        if (isCollected) return;

        PlayerController player = playerObject.GetComponent<PlayerController>();
        if (player == null)
            player = playerObject.GetComponentInChildren<PlayerController>();

        if (player == null)
        {
            Debug.LogError("WallJumpUnlockCollectable: Could not find PlayerController on the player object.");
            return;
        }

        if (player.IsWallJumpUnlocked)
        {
            if (destroyOnCollect) Destroy(gameObject);
            return;
        }

        isCollected = true;
        player.UnlockWallJump();
        Debug.Log("[WallJumpUnlock] Wall jump skill unlocked via collectible!");

        if (collectEffect != null)
            Instantiate(collectEffect, transform.position, Quaternion.identity);

        if (destroyOnCollect)
            Destroy(gameObject);
    }
}
