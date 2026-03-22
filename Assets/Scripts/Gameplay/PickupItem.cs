using UnityEngine;

public class PickupItem : Interactable
{
    private static System.Collections.Generic.List<PickupItem> allItems = new System.Collections.Generic.List<PickupItem>();

    [Header("Item Settings")]
    public string itemName;

    private void OnEnable()
    {
        if (!allItems.Contains(this)) allItems.Add(this);
    }

    private void OnDisable()
    {
        if (allItems.Contains(this)) allItems.Remove(this);
    }

    public static void DisableAllPickups()
    {
        foreach (var item in allItems)
        {
            if (item != null)
            {
                item.isInteractable = false;
            }
        }
        // Clear list since we won't need to track them for disabling anymore
        allItems.Clear();
    }

    protected override void ExecuteInteraction()
    {
        Debug.Log($"Picking up {itemName}");
        
        // Add to Inventory
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(itemName);
        }
        else
        {
            Debug.LogWarning("InventoryManager missing!");
        }

        // Disable object in scene (Simulate picking up)
        gameObject.SetActive(false);
    }
}
