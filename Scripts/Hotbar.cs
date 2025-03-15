using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Hotbar : MonoBehaviour
{
    [Header("References")]
    public InventorySystem inventorySystem;
    public Transform slotParent;
    public GameObject slotPrefab;
    public Image selectionIndicator;
    public TMPro.TextMeshProUGUI itemNameText;
    
    [Header("Settings")]
    public int hotbarSlots = 9;
    public KeyCode[] hotbarKeys = new KeyCode[] {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
    };
    
    private List<HotbarSlot> slots = new List<HotbarSlot>();
    private int selectedSlotIndex = 0;

    void Start()
    {
        // Create the hotbar slots
        CreateHotbarSlots();
        
        // Set initial selection
        SelectSlot(0);
        
        // Register with inventory system
        if (inventorySystem != null)
        {
            inventorySystem.OnInventoryChanged += UpdateHotbar;
        }
    }

    void Update()
    {
        // Handle keyboard selection
        for (int i = 0; i < hotbarKeys.Length && i < hotbarSlots; i++)
        {
            if (Input.GetKeyDown(hotbarKeys[i]))
            {
                SelectSlot(i);
                break;
            }
        }
        
        // Handle scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            int direction = scroll > 0 ? -1 : 1;
            int newIndex = selectedSlotIndex + direction;
            
            // Wrap around
            if (newIndex < 0) newIndex = hotbarSlots - 1;
            if (newIndex >= hotbarSlots) newIndex = 0;
            
            SelectSlot(newIndex);
        }
    }
    
    private void CreateHotbarSlots()
    {
        // Clear existing slots
        foreach (Transform child in slotParent)
        {
            Destroy(child.gameObject);
        }
        slots.Clear();
        
        // Create new slots
        for (int i = 0; i < hotbarSlots; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            HotbarSlot slot = slotObj.GetComponent<HotbarSlot>();
            if (slot == null)
            {
                slot = slotObj.AddComponent<HotbarSlot>();
            }
            
            slot.Initialize(i);
            slots.Add(slot);
        }
    }
    
    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return;
            
        selectedSlotIndex = index;
        
        // Update selection indicator position
        RectTransform slotRect = slots[index].GetComponent<RectTransform>();
        if (selectionIndicator != null && slotRect != null)
        {
            selectionIndicator.rectTransform.position = slotRect.position;
        }
        
        // Update item name text
        UpdateItemNameText();
        
        // Notify any listeners about the selection change
        if (inventorySystem != null)
        {
            inventorySystem.SetSelectedSlot(index);
        }
    }
    
    private void UpdateItemNameText()
    {
        if (itemNameText != null && inventorySystem != null)
        {
            BlockItem item = inventorySystem.GetItemInSlot(selectedSlotIndex);
            if (item != null && item.quantity > 0)
            {
                itemNameText.text = item.itemName;
            }
            else
            {
                itemNameText.text = string.Empty;
            }
        }
    }
    
    public void UpdateHotbar()
    {
        if (inventorySystem == null)
            return;
            
        // Update all slots with current inventory
        for (int i = 0; i < slots.Count; i++)
        {
            BlockItem item = inventorySystem.GetItemInSlot(i);
            slots[i].UpdateSlot(item);
        }
        
        // Also update the item name text
        UpdateItemNameText();
    }
    
    public BlockType GetSelectedBlockType()
    {
        if (inventorySystem != null)
        {
            BlockItem item = inventorySystem.GetItemInSlot(selectedSlotIndex);
            if (item != null && item.quantity > 0)
            {
                return item.blockType;
            }
        }
        
        return BlockType.Air; // Nothing selected
    }
    
    public bool ConsumeSelectedBlock()
    {
        if (inventorySystem != null)
        {
            return inventorySystem.ConsumeItem(selectedSlotIndex);
        }
        
        return false;
    }
}

// Class for individual hotbar slots
public class HotbarSlot : MonoBehaviour
{
    public Image itemIcon;
    public TMPro.TextMeshProUGUI quantityText;
    
    private int slotIndex;
    
    public void Initialize(int index)
    {
        slotIndex = index;
        
        // Find components if not set
        if (itemIcon == null)
        {
            itemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();
        }
        
        if (quantityText == null)
        {
            quantityText = transform.Find("QuantityText")?.GetComponent<TMPro.TextMeshProUGUI>();
        }
    }
    
    public void UpdateSlot(BlockItem item)
    {
        if (item != null && item.quantity > 0)
        {
            // Update icon
            if (itemIcon != null)
            {
                itemIcon.sprite = item.icon;
                itemIcon.enabled = true;
            }
            
            // Update quantity
            if (quantityText != null)
            {
                quantityText.text = item.quantity.ToString();
                quantityText.enabled = true;
            }
        }
        else
        {
            // Empty slot
            if (itemIcon != null)
            {
                itemIcon.enabled = false;
            }
            
            if (quantityText != null)
            {
                quantityText.enabled = false;
            }
        }
    }
}