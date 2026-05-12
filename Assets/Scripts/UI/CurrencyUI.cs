using UnityEngine;
using TMPro;

public class CurrencyUI : MonoBehaviour
{
    [Header("UI Settings")]
    [Tooltip("Select which currency type this UI will display.")]
    [SerializeField] private CurrencyType currencyTypeToDisplay = CurrencyType.Coins;

    [Header("UI References")]
    [Tooltip("The main text showing the total currency.")]
    [SerializeField] private TextMeshProUGUI totalCurrencyText;
    
    [Tooltip("The prefab that contains the sliding/fading text script (+X).")]
    [SerializeField] private GameObject popupTextPrefab;
    
    [Tooltip("The transform where the popup text will spawn (usually on top of the UI text).")]
    [SerializeField] private RectTransform popupSpawnPoint;

    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyAdded += HandleCurrencyAdded;
            
            // Set the initial text
            UpdateCurrencyText(CurrencyManager.Instance.GetCurrency(currencyTypeToDisplay));
        }
    }

    private void OnDestroy()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyAdded -= HandleCurrencyAdded;
        }
    }

    private void HandleCurrencyAdded(CurrencyType type, int total, int addedAmount)
    {
        // Only trigger the UI update and popup if it matches our specific currency
        if (type == currencyTypeToDisplay)
        {
            UpdateCurrencyText(total);
            SpawnPopup(addedAmount);
        }
    }

    private void UpdateCurrencyText(int total)
    {
        if (totalCurrencyText != null)
        {
            totalCurrencyText.text = total.ToString();
        }
    }

    private void SpawnPopup(int addedAmount)
    {
        if (popupTextPrefab != null && popupSpawnPoint != null)
        {
            GameObject popupObj = Instantiate(popupTextPrefab, popupSpawnPoint.position, Quaternion.identity, popupSpawnPoint);
            CurrencyPopup popup = popupObj.GetComponent<CurrencyPopup>();
            
            if (popup != null)
            {
                popup.Setup(addedAmount);
            }
        }
    }
}
