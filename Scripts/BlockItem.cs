using UnityEngine;

[System.Serializable]
public class BlockItem
{
    public BlockType blockType;
    public string itemName;
    public Sprite icon;
    public int quantity;
    
    // Static dictionary to map block types to their display names
    private static readonly System.Collections.Generic.Dictionary<BlockType, string> BlockTypeNames = 
        new System.Collections.Generic.Dictionary<BlockType, string>
        {
            { BlockType.Air, "Air" },
            { BlockType.Dirt, "Dirt" },
            { BlockType.Grass, "Grass Block" },
            { BlockType.Stone, "Stone" },
            { BlockType.Cobblestone, "Cobblestone" },
            { BlockType.Water, "Water" },
            { BlockType.Coal, "Coal Ore" },
            { BlockType.Iron, "Iron Ore" },
            { BlockType.Gold, "Gold Ore" },
            { BlockType.Wood, "Wood" },
            { BlockType.LeavesGreen, "Green Leaves" },
            { BlockType.LeavesBrown, "Brown Leaves" },
            { BlockType.LeavesRed, "Red Leaves" },
            { BlockType.TransparentLeavesGreen, "Transparent Green Leaves" },
            { BlockType.TransparentLeavesBrown, "Transparent Brown Leaves" },
            { BlockType.TransparentLeavesRed, "Transparent Red Leaves" }
            /*
            { BlockType.Sand, "Sand" },
            { BlockType.Glass, "Glass" },
            { BlockType.Brick, "Brick" },
            { BlockType.TNT, "TNT" },
            { BlockType.Diamond, "Diamond Ore" },
            { BlockType.Emerald, "Emerald Ore" },
            { BlockType.Redstone, "Redstone Ore" },
            { BlockType.Lava, "Lava" },
            { BlockType.Glowstone, "Glowstone" },
            { BlockType.Netherrack, "Netherrack" },
            { BlockType.NetherBrick, "Nether Brick" },
            { BlockType.NetherQuartz, "Nether Quartz" },
            { BlockType.EndStone, "End Stone" },
            { BlockType.Obsidian, "Obsidian" },
            { BlockType.Snow, "Snow" },
            { BlockType.Ice, "Ice" },
            { BlockType.Pumpkin, "Pumpkin" },
            { BlockType.Melon, "Melon" },
            { BlockType.Cactus, "Cactus" },
            { BlockType.SugarCane, "Sugar Cane" },
            { BlockType.TallGrass, "Tall Grass" },
            { BlockType.Flower, "Flower" },
            { BlockType.Rose, "Rose" },
            { BlockType.Tulip, "Tulip" },
            { BlockType.Dandelion, "Dandelion" },
            { BlockType.Poppy, "Poppy" },
            { BlockType.BlueOrchid, "Blue Orchid" },
            { BlockType.Allium, "Allium" },
            { BlockType.AzureBluet, "Azure Bluet" },
            { BlockType.RedTulip, "Red Tulip" },
            { BlockType.OrangeTulip, "Orange Tulip" },
            { BlockType.WhiteTulip, "White Tulip" },
            { BlockType.OxeyeDaisy, "Oxeye Daisy" },
            { BlockType.Sunflower, "Sunflower" },
            { BlockType.Lilac, "Lilac" },
            { BlockType.RoseBush, "Rose Bush" },
            { BlockType.Peony, "Peony" },
            { BlockType.BrownMush, "Brown Mushroom" },
            { BlockType.RedMush, "Red Mushroom" },
            { BlockType.MushroomStem, "Mushroom Stem" },
            { BlockType.Vine, "Vine" },
            { BlockType.LilyPad, "Lily Pad" },
            { BlockType.Cocoa, "Cocoa" },
            { BlockType.Carrot, "Carrot" },
            { BlockType.Potato, "Potato" },
            { BlockType.Beetroot, "Beetroot" },
            { BlockType.Wheat, "Wheat" },
            { BlockType.Hay, "Hay Bale" },
            { BlockType.PumpkinPie, "Pumpkin Pie" },
            { BlockType.Bread, "Bread" },
            { BlockType.Cake, "Cake" },
            { BlockType.Cookie, "Cookie" },
            { BlockType.MelonSlice, "Melon Slice" },
            { BlockType.GoldenApple, "Golden Apple" },
            { BlockType.EnchantedApple, "Enchanted Apple" },
            { BlockType.Bone, "Bone" },
            { BlockType.Feather, "Feather" },
            { BlockType.Leather, "Leather" },
            { BlockType.Egg, "Egg" },
            { BlockType.Ink, "Ink Sac" },
            { BlockType.Slime, "Slimeball" },
            { BlockType.String, "String" },
            { BlockType.Gunpowder, "Gunpowder" },
            { BlockType.GlowstoneDust, "Glowstone Dust" },
            { BlockType.RedstoneDust, "Redstone Dust" },
            { BlockType.NetherWart, "Nether Wart" },
            { BlockType.Sugar, "Sugar" },
            { BlockType.BlazeRod, "Blaze Rod" },
            { BlockType.GhastTear, "Ghast Tear" },
            { BlockType.MagmaCream, "Magma Cream" },
            { BlockType.GoldNugget, "Gold Nugget" },
            { BlockType.NetherStar, "Nether Star" },
            { BlockType.PrismarineShard, "Prismarine Shard" },
            { BlockType.PrismarineCrystal, "Prismarine Crystal" }
            */
        };
        
    // Create a BlockItem from a BlockType
    public static BlockItem CreateFromBlockType(BlockType type)
    {
        // Skip creating items for non-collectible blocks
        if (type == BlockType.Air || type == BlockType.Water)
            return null;
            
        BlockItem item = new BlockItem
        {
            blockType = type,
            itemName = GetBlockTypeName(type),
            icon = GetBlockIcon(type),
            quantity = 1
        };
        
        return item;
    }
    
    // Get the display name for a block type
    public static string GetBlockTypeName(BlockType type)
    {
        if (BlockTypeNames.TryGetValue(type, out string name))
            return name;
            
        return "Unknown Block";
    }
    
    // Get the icon for a block type
    private static Sprite GetBlockIcon(BlockType type)
    {
        // Try to load from Resources folder
        Sprite icon = Resources.Load<Sprite>($"BlockIcons/{type}");
        
        if (icon == null)
        {
            // Use a fallback icon if specific one not found
            icon = Resources.Load<Sprite>("BlockIcons/Default");
        }
        
        return icon;
    }
}
