using System.Collections.Generic;
using UnityEngine;
using System;

public class InventorySystem : MonoBehaviour
{
    [Header("Settings")]
    public int inventorySlots = 36;  // Total inventory slots (including hotbar)
    public int hotbarSlots = 9;      // First slots are considered hotbar
    
    [Header("References")]
    public GameObject blockItemPrefab;
    
    private List<BlockItem> inventory = new List<BlockItem>();
    private int selectedSlotIndex = 0;
    
    // Events
    public event Action OnInventoryChanged;
    
    void Start()
    {
        // Initialize inventory with empty slots
        InitializeInventory();
        
        // Add some starter items
        AddItem(new BlockItem { blockType = BlockType.Dirt, quantity = 64 });
        AddItem(new BlockItem { blockType = BlockType.Stone, quantity = 64 });
        AddItem(new BlockItem { blockType = BlockType.Cobblestone, quantity = 64 });
        AddItem(new BlockItem { blockType = BlockType.Wood, quantity = 32 });
        AddItem(new BlockItem { blockType = BlockType.LeavesGreen, quantity = 32 });
    }
    
    private void InitializeInventory()
    {
        inventory.Clear();
        
        // Create empty slots
        for (int i = 0; i < inventorySlots; i++)
        {
            inventory.Add(null);
        }
    }
    
    public BlockItem GetItemInSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < inventory.Count)
        {
            return inventory[slotIndex];
        }
        return null;
    }
    
    public void SetSelectedSlot(int index)
    {
        if (index >= 0 && index < hotbarSlots)
        {
            selectedSlotIndex = index;
        }
    }
    
    public int GetSelectedSlotIndex()
    {
        return selectedSlotIndex;
    }
    
    public BlockType GetSelectedBlockType()
    {
        BlockItem item = GetItemInSlot(selectedSlotIndex);
        if (item != null && item.quantity > 0)
        {
            return item.blockType;
        }
        return BlockType.Air;
    }
    
    public bool ConsumeItem(int slotIndex)
    {
        BlockItem item = GetItemInSlot(slotIndex);
        if (item != null && item.quantity > 0)
        {
            item.quantity--;
            
            // Remove item if quantity is 0
            if (item.quantity <= 0)
            {
                inventory[slotIndex] = null;
            }
            
            // Notify listeners
            OnInventoryChanged?.Invoke();
            return true;
        }
        return false;
    }
    
    public void AddItem(BlockItem newItem)
    {
        if (newItem == null || newItem.quantity <= 0)
            return;
            
        // First try to stack with existing items
        for (int i = 0; i < inventory.Count; i++)
        {
            BlockItem existingItem = inventory[i];
            if (existingItem != null && existingItem.blockType == newItem.blockType)
            {
                // Stack items
                existingItem.quantity += newItem.quantity;
                newItem.quantity = 0;
                
                // Break if we've added all items
                if (newItem.quantity <= 0)
                {
                    // Notify listeners
                    OnInventoryChanged?.Invoke();
                    return;
                }
            }
        }
        
        // If we still have items to add, find an empty slot
        if (newItem.quantity > 0)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] == null)
                {
                    // Create a copy of the item for the inventory
                    BlockItem itemCopy = new BlockItem
                    {
                        blockType = newItem.blockType,
                        quantity = newItem.quantity,
                        itemName = newItem.itemName,
                        icon = newItem.icon
                    };
                    
                    inventory[i] = itemCopy;
                    newItem.quantity = 0;
                    break;
                }
            }
        }
        
        // Notify listeners
        OnInventoryChanged?.Invoke();
    }
    
    public void CollectBlock(BlockType blockType)
    {
        // Create a new block item
        BlockItem newItem = BlockItem.CreateFromBlockType(blockType);
        if (newItem != null)
        {
            AddItem(newItem);
        }
    }
}
