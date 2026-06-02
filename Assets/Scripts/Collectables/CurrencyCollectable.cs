using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CurrencyCollectable : MonoBehaviour, IInteractable
{
    [Header("Currency Settings")]
    [Tooltip("Select what type of currency this object gives.")]
    [SerializeField] private CurrencyType currencyType = CurrencyType.Coins;
    
    [SerializeField] private int currencyValue = 1;
    [SerializeField] private bool destroyOnCollect = true;

    [Header("Collect Flash Effect")]
    [Tooltip("Icon shown on screen when this collectible is picked up. Leave empty to skip.")]
    [SerializeField] private Sprite collectIcon;
    [Tooltip("Text shown on screen when this collectible is picked up. Leave empty to skip.")]
    [SerializeField] private string collectLabel = "+1 Coin";

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
            Debug.LogWarning("CurrencyCollectable: Collider is not set to Trigger. It should be a trigger for 'contact' pickup.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Collect();
        }
    }

    public void Interact()
    {
        Collect();
    }

    private void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        if (CurrencyManager.Instance != null)
        {
            // Pass the specific currency type
            CurrencyManager.Instance.AddCurrency(currencyType, currencyValue);
        }
        else
        {
            Debug.LogError("CurrencyManager instance not found in the scene! Make sure you have a CurrencyManager.");
        }

        if (!string.IsNullOrEmpty(collectLabel) && CollectFlashEffect.Instance != null)
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
        {
            Destroy(gameObject);
        }
    }
}
