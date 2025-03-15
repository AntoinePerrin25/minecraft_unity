using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapSystem : MonoBehaviour
{
    public PlayerController player;
    public ChunkGenerator chunkGenerator;
    public RawImage minimapImage;
    public RectTransform playerIndicator;
    public RectTransform viewCone;
    
    [Header("Minimap Settings")]
    public int minimapSize = 512;
    public float minimapScale = 2f;  // How many world units per pixel
    public Color playerColor = Color.red;
    public Color viewConeColor = new Color(1f, 0.5f, 0f, 0.5f); // Semi-transparent orange
    public bool showDetailedChunks = true;  // Whether to show detailed block colors or just chunk colors
    public bool showGrid = true;  // Whether to show chunk grid lines
    public Color gridColor = new Color(0.3f, 0.3f, 0.3f);
    public Color unexploredColor = new Color(0.2f, 0.2f, 0.2f);
    
    private RenderTexture minimapTexture;
    private Texture2D heightmapTexture;
    private Dictionary<Vector2Int, Texture2D> chunkDetailTextures = new Dictionary<Vector2Int, Texture2D>();
    private Dictionary<Vector2Int, Color> chunkTopColors = new Dictionary<Vector2Int, Color>();
    
    private readonly Dictionary<BlockType, Color> blockColors = new Dictionary<BlockType, Color>()
    {
        { BlockType.Air,                    new Color(0.8f, 0.8f, 1.0f)     },      // Light blue for air
        { BlockType.Grass,                  new Color(0.3f, 0.8f, 0.3f)     },      // Green for grass
        { BlockType.Dirt,                   new Color(0.6f, 0.4f, 0.2f)     },      // Brown for dirt
        { BlockType.Stone,                  new Color(0.5f, 0.5f, 0.5f)     },      // Grey for stone
        { BlockType.Water,                  new Color(0.2f, 0.2f, 0.8f)     },      // Blue for water
        { BlockType.Coal,                   new Color(0.2f, 0.2f, 0.2f)     },      // Dark grey for coal
        { BlockType.Iron,                   new Color(0.8f, 0.7f, 0.6f)     },      // Light brown for iron
        { BlockType.Gold,                   new Color(1.0f, 0.8f, 0.0f)     },      // Yellow for gold
        { BlockType.Wood,                   new Color(0.6f, 0.3f, 0.0f)     },      // Dark brown for wood
        { BlockType.LeavesGreen,            new Color(0.0f, 0.5f, 0.0f)     },      // Dark green
        { BlockType.LeavesBrown,            new Color(0.45f, 0.32f, 0.18f)  },      // Brown
        { BlockType.LeavesRed,              new Color(0.6f, 0.1f, 0.1f)     },      // Red
        { BlockType.TransparentLeavesGreen, new Color(0.2f, 0.7f, 0.2f)     },      // Lighter green
        { BlockType.TransparentLeavesBrown, new Color(0.55f, 0.42f, 0.28f)  },      // Lighter brown
        { BlockType.TransparentLeavesRed,   new Color(0.8f, 0.2f, 0.2f)     }       // Lighter red
    };
    
    void Start()
    {
        // Create render texture for the minimap
        minimapTexture = new RenderTexture(minimapSize, minimapSize, 0);
        heightmapTexture = new Texture2D(minimapSize, minimapSize, TextureFormat.RGBA32, false);
        
        // Set up the UI raw image
        if (minimapImage != null)
        {
            minimapImage.texture = minimapTexture;
        }
        
        // Set up player indicator and view cone
        if (playerIndicator != null)
        {
            playerIndicator.GetComponent<Image>().color = playerColor;
        }
        
        if (viewCone != null)
        {
            viewCone.GetComponent<Image>().color = viewConeColor;
        }
    }
    
    void Update()
    {
        UpdateMinimap();
        UpdatePlayerIndicator();
    }
    
    void UpdateMinimap()
    {
        if (chunkGenerator == null || heightmapTexture == null)
            return;
            
        // Get player position
        Vector3 playerPos = player.GetPosition();
        Vector2Int currentChunk = chunkGenerator.GetChunkPosition(playerPos);
        
        // Calculate area to show on minimap
        int range = Mathf.CeilToInt(minimapSize / (2 * chunkGenerator.renderDistance * minimapScale));
        
        // Clear the texture
        Color[] clearColors = new Color[minimapSize * minimapSize];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = new Color(0.1f, 0.1f, 0.1f, 1f); // Dark background
            
        heightmapTexture.SetPixels(clearColors);
        
        // Draw chunks
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                Vector2Int chunkPos = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                DrawChunkOnMinimap(chunkPos);
            }
        }
        
        // Apply changes
        heightmapTexture.Apply();
        Graphics.Blit(heightmapTexture, minimapTexture);
    }
    
    void DrawChunkOnMinimap(Vector2Int chunkPos)
    {
        // Calculate pixel position on minimap
        Vector3 playerPos = player.GetPosition();
        Vector2Int playerChunk = chunkGenerator.GetChunkPosition(playerPos);
        
        int chunkSize = 16; // Same as CHUNK_SIZE_X/Z in ChunkGenerator
        float pixelsPerChunk = chunkSize / minimapScale;
        
        // Calculate chunk position relative to player for minimap
        int relX = chunkPos.x - playerChunk.x;
        int relZ = chunkPos.y - playerChunk.y;
        
        // Center of minimap
        int centerX = minimapSize / 2;
        int centerZ = minimapSize / 2;
        
        // Draw chunk on minimap
        int startX = Mathf.RoundToInt(centerX + relX * pixelsPerChunk);
        int startZ = Mathf.RoundToInt(centerZ + relZ * pixelsPerChunk);
        int size = Mathf.CeilToInt(pixelsPerChunk);
        
        // Make sure we're in bounds
        if (startX + size < 0 || startX >= minimapSize || startZ + size < 0 || startZ >= minimapSize)
            return;

        // Draw the chunk content
        if (chunkGenerator.IsChunkGenerated(chunkPos))
        {
            if (showDetailedChunks)
            {
                // Get or create detailed chunk texture
                Texture2D chunkTexture = GetDetailedChunkTexture(chunkPos);
                if (chunkTexture != null)
                {
                    // Draw the detailed chunk texture
                    DrawDetailedChunkTexture(chunkTexture, startX, startZ, size);
                }
                else
                {
                    // Fall back to simple color if detailed texture isn't available
                    DrawSimpleChunkColor(chunkPos, startX, startZ, size);
                }
            }
            else
            {
                // Draw simple chunk color
                DrawSimpleChunkColor(chunkPos, startX, startZ, size);
            }
        }
        else
        {
            // Draw unexplored chunk
            DrawUnexploredChunk(startX, startZ, size);
        }
        
        // Draw chunk grid lines
        if (showGrid)
        {
            DrawChunkGridLines(startX, startZ, size);
        }
    }
    
    private void DrawDetailedChunkTexture(Texture2D chunkTexture, int startX, int startZ, int size)
    {
        // Calculate the scaling factor between chunk texture and minimap
        float scaleX = (float)chunkTexture.width / size;
        float scaleZ = (float)chunkTexture.height / size;
        
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                int pixelX = startX + x;
                int pixelZ = startZ + z;
                
                if (pixelX >= 0 && pixelX < minimapSize && pixelZ >= 0 && pixelZ < minimapSize)
                {
                    // Sample the detailed texture - using point sampling for simplicity
                    int sampleX = Mathf.FloorToInt(x * scaleX);
                    int sampleZ = Mathf.FloorToInt(z * scaleZ);
                    
                    // Clamp to valid texture coordinates
                    sampleX = Mathf.Clamp(sampleX, 0, chunkTexture.width - 1);
                    sampleZ = Mathf.Clamp(sampleZ, 0, chunkTexture.height - 1);
                    
                    Color blockColor = chunkTexture.GetPixel(sampleX, sampleZ);
                    heightmapTexture.SetPixel(pixelX, pixelZ, blockColor);
                }
            }
        }
    }
    
    private void DrawSimpleChunkColor(Vector2Int chunkPos, int startX, int startZ, int size)
    {
        // Get chunk color (cached or calculate new)
        if (!chunkTopColors.TryGetValue(chunkPos, out Color chunkColor))
        {
            chunkColor = CalculateChunkColor(chunkPos);
            chunkTopColors[chunkPos] = chunkColor;
        }
        
        // Draw chunk as a colored square
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++) // Fixed loop variable from 'x' to 'z'
            {
                int pixelX = startX + x;
                int pixelZ = startZ + z;
                
                if (pixelX >= 0 && pixelX < minimapSize && pixelZ >= 0 && pixelZ < minimapSize)
                {
                    heightmapTexture.SetPixel(pixelX, pixelZ, chunkColor);
                }
            }
        }
    }
    
    private void DrawUnexploredChunk(int startX, int startZ, int size)
    {
        for (int x = 0; x < size; x++)
        {
            for (int z = 0; z < size; z++)
            {
                int pixelX = startX + x;
                int pixelZ = startZ + z;
                
                if (pixelX >= 0 && pixelX < minimapSize && pixelZ >= 0 && pixelZ < minimapSize)
                {
                    heightmapTexture.SetPixel(pixelX, pixelZ, unexploredColor);
                }
            }
        }
    }
    
    private void DrawChunkGridLines(int startX, int startZ, int size)
    {
        for (int i = 0; i <= size; i++)
        {
            int pixelX = startX + i;
            int pixelZ = startZ + i;
            
            if (pixelX >= 0 && pixelX < minimapSize)
            {
                if (startZ >= 0 && startZ < minimapSize)
                    heightmapTexture.SetPixel(pixelX, startZ, gridColor);
                if (startZ + size >= 0 && startZ + size < minimapSize)
                    heightmapTexture.SetPixel(pixelX, startZ + size, gridColor);
            }
            
            if (pixelZ >= 0 && pixelZ < minimapSize)
            {
                if (startX >= 0 && startX < minimapSize)
                    heightmapTexture.SetPixel(startX, pixelZ, gridColor);
                if (startX + size >= 0 && startX + size < minimapSize)
                    heightmapTexture.SetPixel(startX + size, pixelZ, gridColor);
            }
        }
    }
    
    private Texture2D GetDetailedChunkTexture(Vector2Int chunkPos)
    {
        // Try to get from cache first
        if (chunkDetailTextures.TryGetValue(chunkPos, out Texture2D texture))
        {
            return texture;
        }
        
        // Generate new detailed texture for this chunk
        texture = GenerateChunkDetailTexture(chunkPos);
        if (texture != null)
        {
            chunkDetailTextures[chunkPos] = texture;
        }
        
        return texture;
    }
    
    private Texture2D GenerateChunkDetailTexture(Vector2Int chunkPos)
    {
        // Request the top blocks data from chunk generator
        BlockType[,] topBlocks = chunkGenerator.GetTopBlocksInChunk(chunkPos);
        if (topBlocks == null)
            return null;
            
        int chunkSize = 16; // CHUNK_SIZE_X/Z
        Texture2D texture = new Texture2D(chunkSize, chunkSize, TextureFormat.RGBA32, false);
        
        // Fill with block colors
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                BlockType blockType = topBlocks[x, z];
                Color blockColor = GetBlockColor(blockType);
                texture.SetPixel(x, z, blockColor);
            }
        }
        
        texture.Apply();
        return texture;
    }
    
    private Color GetBlockColor(BlockType blockType)
    {
        if (blockColors.TryGetValue(blockType, out Color color))
            return color;
            
        // Default color if not found
        return new Color(0.5f, 0.5f, 0.5f);
    }
    
    // Method to invalidate cached chunk data when a chunk changes
    public void InvalidateChunkCache(Vector2Int chunkPos)
    {
        chunkTopColors.Remove(chunkPos);
        chunkDetailTextures.Remove(chunkPos);
    }
    
    // Call this when player modifies blocks to refresh affected chunks
    public void RefreshMinimap()
    {
        // Clear all cached data to force re-render
        chunkTopColors.Clear();
        chunkDetailTextures.Clear();
        
        // Update the minimap immediately
        UpdateMinimap();
    }
    
    Color CalculateChunkColor(Vector2Int chunkPos)
    {
        // Check if chunk exists or is generated
        if (chunkGenerator.IsChunkGenerated(chunkPos))
        {
            BlockType topBlock = chunkGenerator.GetTopBlockTypeAt(chunkPos);
            if (blockColors.TryGetValue(topBlock, out Color color))
                return color;
        }
        
        // Default color for chunks that don't exist yet
        return new Color(0.2f, 0.2f, 0.2f);
    }
    
    void UpdatePlayerIndicator()
    {
        if (playerIndicator == null || viewCone == null || player == null)
            return;
        
        // Center the player indicator
        playerIndicator.anchoredPosition = Vector2.zero;
        
        // Update view cone rotation to match player's look direction
        float yRotation = player.GetLookRotation();
        viewCone.rotation = Quaternion.Euler(0, 0, -yRotation);
    }
}
