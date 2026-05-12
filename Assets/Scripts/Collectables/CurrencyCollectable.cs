using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CurrencyCollectable : MonoBehaviour, IInteractable
{
    [Header("Currency Settings")]
    [Tooltip("Select what type of currency this object gives.")]
    [SerializeField] private CurrencyType currencyType = CurrencyType.Coins;
    
    [SerializeField] private int currencyValue = 1;
    [SerializeField] private bool destroyOnCollect = true;

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

        if (destroyOnCollect)
        {
            Destroy(gameObject);
        }
    }
}
