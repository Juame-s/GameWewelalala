using UnityEngine;
using System;
using System.Collections.Generic;

// You can add more currencies here in the future
public enum CurrencyType
{
    Coins,
    Insight
}

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    // Dictionary to store the total amount for each currency type
    private Dictionary<CurrencyType, int> currencyTotals = new Dictionary<CurrencyType, int>();

    // Event triggered when currency is added. Passes (CurrencyType, TotalAmount, AddedAmount)
    public event Action<CurrencyType, int, int> OnCurrencyAdded;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddCurrency(CurrencyType type, int amount)
    {
        // If this currency type isn't in the dictionary yet, initialize it
        if (!currencyTotals.ContainsKey(type))
        {
            currencyTotals[type] = 0;
        }

        currencyTotals[type] += amount;
        
        // Broadcast the change to any UI listening
        OnCurrencyAdded?.Invoke(type, currencyTotals[type], amount);
    }

    // Helpful method to get current total for a specific currency
    public int GetCurrency(CurrencyType type)
    {
        if (currencyTotals.ContainsKey(type))
        {
            return currencyTotals[type];
        }
        return 0;
    }
}
