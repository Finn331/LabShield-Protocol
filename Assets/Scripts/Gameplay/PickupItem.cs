using UnityEngine;

public class PickupItem : Interactable
{
    [Header("Item Settings")]
    public string itemName;

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
