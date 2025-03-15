using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;

public class ChunkGenerator : MonoBehaviour
{
    public GameObject chunkPrefab;
    public Material blockAtlasMaterial; // Material with texture atlas instead of separate materials
    public int atlasSize = 16; // Atlas is a 16x16 grid of textures
    public float textureSize = 16f; // Each texture is 16x16 pixels
    
    public int seed = 0;
    public int renderDistance = 8;
    
    private const int CHUNK_SIZE_X = 16;
    private const int CHUNK_SIZE_Y = 384;
    private const int CHUNK_SIZE_Z = 16;
    
    private const float TERRAIN_SCALE = 0.05f;
    private const float TERRAIN_HEIGHT_MULTIPLIER = 80f;
    private const float BASE_TERRAIN_HEIGHT = 128f;

    private const float BIOME_SCALE = 0.1f;
    
    private const float CAVES_SCALE = 0.1f;
    private const float CAVES_THRESHOLD = 0.4f;
    
    private const float ORE_SCALE = 0.2f;
    private const float COAL_THRESHOLD = 0.35f;
    private const float IRON_THRESHOLD = 0.4f;
    private const float GOLD_THRESHOLD = 0.45f;
    
    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    public PlayerController playerController; 
    private Vector2Int currentChunk = new Vector2Int(0, 0);
    
    // Threading related variables
    private Thread[] chunkGeneratorThreads;
    private bool isRunning = true;
    private ConcurrentQueue<Vector2Int> chunkRequestQueue = new ConcurrentQueue<Vector2Int>();
    private ConcurrentDictionary<Vector2Int, BlockType[,,]> generatedChunksData = new ConcurrentDictionary<Vector2Int, BlockType[,,]>();
    private List<Vector2Int> chunksToCreate = new List<Vector2Int>();
    private readonly object chunksToCreateLock = new object();
    private int maxThreads = 4; // Adjust based on system capabilities
    
    private void Start()
    {
        // Initialiser le générateur de nombres aléatoires avec la seed
        Random.InitState(seed);
        
        // Get initial player position and calculate current chunk
        Vector3 playerPos = playerController.GetPosition();
        currentChunk = GetChunkPosition(playerPos);
        
        Debug.Log($"Starting in chunk {currentChunk}");
        
        // Start chunk generator threads
        StartChunkGeneratorThreads();
        
        // Request initial chunks
        RequestChunksAround(currentChunk);
    }
    
    private void OnDestroy()
    {
        // Signal threads to stop and wait for them to finish
        isRunning = false;
        if (chunkGeneratorThreads != null)
        {
            foreach (Thread thread in chunkGeneratorThreads)
            {
                if (thread != null && thread.IsAlive)
                {
                    thread.Join(100); // Wait 100ms for thread to finish
                }
            }
        }
    }
    
    private void StartChunkGeneratorThreads()
    {
        isRunning = true;
        chunkGeneratorThreads = new Thread[maxThreads];
        
        for (int i = 0; i < maxThreads; i++)
        {
            chunkGeneratorThreads[i] = new Thread(ChunkGeneratorThreadWork);
            chunkGeneratorThreads[i].IsBackground = true; // Set as background thread
            chunkGeneratorThreads[i].Start();
        }
    }
    
    private void ChunkGeneratorThreadWork()
    {
        while (isRunning)
        {
            if (chunkRequestQueue.TryDequeue(out Vector2Int chunkPos))
            {
                // Generate blocks data in thread
                BlockType[,,] blocks = GenerateBlocksThreadSafe(chunkPos);
                
                // Store the generated data
                generatedChunksData[chunkPos] = blocks;
                
                // Signal main thread that chunk is ready to be created
                lock (chunksToCreateLock)
                {
                    chunksToCreate.Add(chunkPos);
                }
            }
            else
            {
                // If no work, sleep briefly to avoid high CPU usage
                Thread.Sleep(10);
            }
        }
    }
    
    private void FixedUpdate()
    {
        // Obtenir la position du joueur
        Vector3 playerPos = playerController.GetPosition();
        
        // Calculer dans quel chunk se trouve le joueur
        Vector2Int playerChunk = GetChunkPosition(playerPos);

        // Si le joueur a changé de chunk
        if (playerChunk != currentChunk)
        {
            currentChunk = playerChunk;
            RequestChunksAround(currentChunk);
        }
        
        // Process any chunks that are ready to be created
        ProcessReadyChunks();
    }
    
    private void ProcessReadyChunks()
    {
        List<Vector2Int> chunksToProcess = null;
        
        // Get chunks ready for creation
        lock (chunksToCreateLock)
        {
            if (chunksToCreate.Count > 0)
            {
                chunksToProcess = new List<Vector2Int>(chunksToCreate);
                chunksToCreate.Clear();
            }
        }
        
        if (chunksToProcess != null)
        {
            foreach (Vector2Int chunkPos in chunksToProcess)
            {
                if (generatedChunksData.TryRemove(chunkPos, out BlockType[,,] blocks))
                {
                    CreateChunkGameObject(chunkPos, blocks);
                }
            }
        }
    }
    
    private void RequestChunksAround(Vector2Int centerChunk)
    {
        // Track which chunks to keep
        HashSet<Vector2Int> chunksToKeep = new HashSet<Vector2Int>();
        
        // Request chunks around player
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2Int chunkPos = new Vector2Int(centerChunk.x + x, centerChunk.y + z);
                chunksToKeep.Add(chunkPos);
                
                // Only request if not already loaded or being generated
                if (!chunks.ContainsKey(chunkPos) && 
                    !generatedChunksData.ContainsKey(chunkPos))
                {
                    bool contains = false;
                    foreach (Vector2Int chunk in chunkRequestQueue)
                    {
                        if (chunk == chunkPos)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        chunkRequestQueue.Enqueue(chunkPos);
                    }
                }
            }
        }
        
        // Remove chunks that are too far away
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        foreach (Vector2Int chunkPos in chunks.Keys)
        {
            if (!chunksToKeep.Contains(chunkPos))
            {
                chunksToRemove.Add(chunkPos);
            }
        }
        
        foreach (Vector2Int chunkPos in chunksToRemove)
        {
            Destroy(chunks[chunkPos]);
            chunks.Remove(chunkPos);
        }
    }
    
    private void CreateChunkGameObject(Vector2Int chunkPos, BlockType[,,] blocks)
    {
        // Skip if chunk was already created while we were generating
        if (chunks.ContainsKey(chunkPos))
            return;
            
        // Create the chunk object
        GameObject chunk = Instantiate(chunkPrefab, new Vector3(chunkPos.x * CHUNK_SIZE_X, 0, chunkPos.y * CHUNK_SIZE_Z), Quaternion.identity);
        chunk.name = "Chunk_" + chunkPos.x + "_" + chunkPos.y;
        chunks.Add(chunkPos, chunk);
        
        // Generate mesh for the chunk
        ChunkMeshGenerator meshGenerator = chunk.GetComponent<ChunkMeshGenerator>();
        if (meshGenerator != null)
        {
            meshGenerator.GenerateMesh(blocks, blockAtlasMaterial, atlasSize, textureSize);
        }
    }
    
    // Thread-safe version of GenerateBlocks that doesn't use Unity API
    private BlockType[,,] GenerateBlocksThreadSafe(Vector2Int chunkPos)
    {
        BlockType[,,] blocks = new BlockType[CHUNK_SIZE_X, CHUNK_SIZE_Y, CHUNK_SIZE_Z];
        System.Random random = new System.Random(seed + chunkPos.x * 10000 + chunkPos.y);
        
        // Générer le terrain
        for (int x = 0; x < CHUNK_SIZE_X; x++)
        {
            for (int z = 0; z < CHUNK_SIZE_Z; z++)
            {
                // Calculer les coordonnées absolues dans le monde
                float worldX = chunkPos.x * CHUNK_SIZE_X + x;
                float worldZ = chunkPos.y * CHUNK_SIZE_Z + z;
                
                // Générer la hauteur du terrain avec le bruit de Perlin
                float terrainHeight = GenerateTerrainHeightThreadSafe(worldX, worldZ);
                int heightInt = (int)System.Math.Floor(terrainHeight);
                
                // Remplir les blocs du terrain
                for (int y = 0; y < CHUNK_SIZE_Y; y++)
                {
                    // Si on est au-dessus de la hauteur du terrain, c'est de l'air
                    if (y > heightInt)
                    {
                        // En dessous d'un certain niveau, c'est de l'eau
                        if (y < BASE_TERRAIN_HEIGHT * 0.4f)
                        {
                            blocks[x, y, z] = BlockType.Water;
                        }
                        else
                        {
                            blocks[x, y, z] = BlockType.Air;
                        }
                    }
                    // Si on est à la surface, c'est de l'herbe
                    else if (y == heightInt && y > BASE_TERRAIN_HEIGHT * 0.4f)
                    {
                        blocks[x, y, z] = BlockType.Grass;
                    }
                    // Juste en dessous de la surface, c'est de la terre
                    else if (y > heightInt - 4 && y > BASE_TERRAIN_HEIGHT * 0.4f)
                    {
                        blocks[x, y, z] = BlockType.Dirt;
                    }
                    // Plus profond, c'est de la pierre
                    else
                    {
                        blocks[x, y, z] = BlockType.Stone;
                        
                        // Générer les grottes
                        if (ShouldBeACaveThreadSafe(worldX, y, worldZ))
                        {
                            blocks[x, y, z] = BlockType.Air;
                        }
                        // Générer les minerais
                        else
                        {
                            blocks[x, y, z] = GenerateOreThreadSafe(worldX, y, worldZ, blocks[x, y, z]);
                        }
                    }
                }
                
                // Générer les arbres (1% de chance sur chaque bloc d'herbe)
                if (random.NextDouble() < 0.01 && blocks[x, heightInt, z] == BlockType.Grass)
                {
                    GenerateTreeThreadSafe(blocks, x, heightInt + 1, z, chunkPos);
                }
            }
        }
        
        return blocks;
    }
    
    // Thread-safe versions of noise generation methods that don't use Unity's Random or Mathf classes
    
    private float GenerateTerrainHeightThreadSafe(float x, float z)
    {
        // Use thread-safe perlin noise
        float height = 0;
        
        height += ThreadSafePerlinNoise(x * TERRAIN_SCALE + seed, z * TERRAIN_SCALE + seed) * TERRAIN_HEIGHT_MULTIPLIER;
        height += ThreadSafePerlinNoise(x * TERRAIN_SCALE * 2 + seed + 100, z * TERRAIN_SCALE * 2 + seed + 100) * TERRAIN_HEIGHT_MULTIPLIER * 0.5f;
        height += ThreadSafePerlinNoise(x * TERRAIN_SCALE * 0.5f + seed + 200, z * TERRAIN_SCALE * 0.5f + seed + 200) * TERRAIN_HEIGHT_MULTIPLIER * 1.5f;
        
        height = height / 3.0f + BASE_TERRAIN_HEIGHT;
        
        return height;
    }
    
    private bool ShouldBeACaveThreadSafe(float x, float y, float z)
    {
        float cavesNoise = Perlin3DThreadSafe(
            x * CAVES_SCALE + seed + 300,
            y * CAVES_SCALE + seed + 300,
            z * CAVES_SCALE + seed + 300
        );
        
        return cavesNoise > CAVES_THRESHOLD;
    }
    
    private BlockType GenerateOreThreadSafe(float x, float y, float z, BlockType currentBlock)
    {
        // Ne générer des minerais que dans la pierre
        if (currentBlock != BlockType.Stone)
            return currentBlock;
        
        // Thread-safe 3D noise
        float oreNoise = Perlin3DThreadSafe(
            x * ORE_SCALE + seed + 400,
            y * ORE_SCALE + seed + 400,
            z * ORE_SCALE + seed + 400
        );
        
        // Déterminer le type de minerai en fonction de la profondeur et du bruit
        if (oreNoise > GOLD_THRESHOLD && y < BASE_TERRAIN_HEIGHT * 0.2f)
        {
            return BlockType.Gold;
        }
        else if (oreNoise > IRON_THRESHOLD && y < BASE_TERRAIN_HEIGHT * 0.4f)
        {
            return BlockType.Iron;
        }
        else if (oreNoise > COAL_THRESHOLD && y < BASE_TERRAIN_HEIGHT * 0.6f)
        {
            return BlockType.Coal;
        }
        
        return currentBlock;
    }
    
    public Vector2Int GetChunkPosition(Vector3 position)
    {
        // Convert world position to chunk coordinates
        int chunkX = Mathf.FloorToInt(position.x / CHUNK_SIZE_X);
        int chunkZ = Mathf.FloorToInt(position.z / CHUNK_SIZE_Z);
        return new Vector2Int(chunkX, chunkZ);
    }
    
    // Add this method for biome calculation
    private BiomeType CalculateBiomeType(Vector2Int chunkPos)
    {
        // Use noise to create smooth biome transitions
        float biomeNoise = ThreadSafePerlinNoise(
            chunkPos.x * BIOME_SCALE + seed * 0.3f,
            chunkPos.y * BIOME_SCALE + seed * 0.7f
        );
        
        // Use the noise value to determine biome type
        if (biomeNoise < 0.3f)
            return BiomeType.Forest; // Green leaves
        else if (biomeNoise < 0.6f)
            return BiomeType.Autumn; // Red leaves
        else
            return BiomeType.Savanna; // Brown leaves
    }
    
    // Add this method to get the appropriate leaf type based on biome and transparency
    private BlockType GetLeafTypeForBiome(BiomeType biome, bool transparent)
    {
        switch (biome)
        {
            case BiomeType.Forest:
                return transparent ? BlockType.TransparentLeavesGreen : BlockType.LeavesGreen;
                
            case BiomeType.Autumn:
                return transparent ? BlockType.TransparentLeavesRed : BlockType.LeavesRed;
                
            case BiomeType.Savanna:
                return transparent ? BlockType.TransparentLeavesBrown : BlockType.LeavesBrown;
                
            default:
                return transparent ? BlockType.TransparentLeavesGreen : BlockType.LeavesGreen;
        }
    }
    
    // Modify the tree generation methods to use biome-specific leaves
    private void GenerateTreeThreadSafe(BlockType[,,] blocks, int x, int y, int z, Vector2Int chunkPos)
    {
        // Vérifier si l'arbre peut être placé (espace suffisant)
        if (y + 4 >= CHUNK_SIZE_Y || x <= 1 || x >= CHUNK_SIZE_X - 2 || z <= 1 || z >= CHUNK_SIZE_Z - 2)
            return;
        
        // Determine biome for this chunk
        BiomeType biome = CalculateBiomeType(chunkPos);
        
        // Get leaf type for this biome (with 30% chance of transparency)
        System.Random random = new System.Random(seed + chunkPos.x * 73856 + chunkPos.y * 19349 + x * 384 + z);
        bool transparent = random.NextDouble() < 0.3f;
        BlockType leafType = GetLeafTypeForBiome(biome, transparent);
        
        // Thread-safe tree generation (no Unity API calls)
        // Tronc
        for (int treeY = 0; treeY < 5; treeY++)
        {
            blocks[x, y + treeY, z] = BlockType.Wood;
        }
        
        // Feuilles (couche inférieure)
        for (int leafX = -2; leafX <= 2; leafX++)
        {
            for (int leafZ = -2; leafZ <= 2; leafZ++)
            {
                if (x + leafX >= 0 && x + leafX < CHUNK_SIZE_X && 
                    z + leafZ >= 0 && z + leafZ < CHUNK_SIZE_Z &&
                    y + 3 < CHUNK_SIZE_Y)
                {
                    blocks[x + leafX, y + 3, z + leafZ] = leafType;
                }
            }
        }
        
        // Feuilles (couche du milieu)
        for (int leafX = -1; leafX <= 1; leafX++)
        {
            for (int leafZ = -1; leafZ <= 1; leafZ++)
            {
                if (x + leafX >= 0 && x + leafX < CHUNK_SIZE_X && 
                    z + leafZ >= 0 && z + leafZ < CHUNK_SIZE_Z &&
                    y + 4 < CHUNK_SIZE_Y)
                {
                    blocks[x + leafX, y + 4, z + leafZ] = leafType;
                }
            }
        }
        
        // Feuille supérieure
        if (y + 5 < CHUNK_SIZE_Y)
        {
            blocks[x, y + 5, z] = GetLeafTypeForBiome(biome, false);
        }
    }
    
    // Thread-safe Perlin noise implementations
    private float ThreadSafePerlinNoise(float x, float y)
    {
        // Simplex noise could be used here for better performance
        // This is a simple implementation of perlin-like noise
        
        // Convert to grid coordinates
        int X = (int)System.Math.Floor(x);
        int Y = (int)System.Math.Floor(y);
        
        // Get fractional parts
        x -= X;
        y -= Y;
        
        // Wrap to ensure we stay in the positive range for random generation
        X = X & 255;
        Y = Y & 255;
        
        // Calculate dot products from pseudorandom gradients
        float n00 = DotGridGradient(X, Y, x, y);
        float n01 = DotGridGradient(X, Y + 1, x, y - 1);
        float n10 = DotGridGradient(X + 1, Y, x - 1, y);
        float n11 = DotGridGradient(X + 1, Y + 1, x - 1, y - 1);
        
        // Smooth interpolation
        float u = Fade(x);
        float v = Fade(y);
        
        // Interpolate
        float x0 = Lerp(n00, n10, u);
        float x1 = Lerp(n01, n11, u);
        float result = Lerp(x0, x1, v);
        
        // Perlin noise typically returns values in [-1,1], but we normalize to [0,1]
        return (result + 1) / 2;
    }
    
    private float DotGridGradient(int ix, int iy, float x, float y)
    {
        // Use a hashing function to get a reproducible pseudorandom gradient
        int hash = GetHashValue(ix, iy, seed);
        
        // Convert hash to a gradient direction
        float angle = hash * (3.14159f / 128.0f);
        float gradX = (float)System.Math.Cos(angle);
        float gradY = (float)System.Math.Sin(angle);
        
        // Compute dot product
        return x * gradX + y * gradY;
    }
    
    private int GetHashValue(int x, int y, int seed)
    {
        // Simple hash function for reproducible pseudorandom values
        int hash = seed;
        hash ^= x * 73856093;
        hash ^= y * 19349663;
        hash = hash % 256;
        return hash;
    }
    
    private float Fade(float t)
    {
        // Smoothstep function: 6t^5 - 15t^4 + 10t^3
        return t * t * t * (t * (t * 6 - 15) + 10);
    }
    
    private float Lerp(float a, float b, float t)
    {
        return a + t * (b - a);
    }
    
    private float Perlin3DThreadSafe(float x, float y, float z)
    {
        // Thread-safe 3D Perlin noise approximation using 2D noise slices
        float xy = ThreadSafePerlinNoise(x, y);
        float yz = ThreadSafePerlinNoise(y, z);
        float xz = ThreadSafePerlinNoise(x, z);
        
        float yx = ThreadSafePerlinNoise(y, x);
        float zy = ThreadSafePerlinNoise(z, y);
        float zx = ThreadSafePerlinNoise(z, x);
        
        return (xy + yz + xz + yx + zy + zx) / 6.0f;
    }

    public IEnumerator BreakBlockAt(Vector3Int blockPos)
    {
        // Calculate chunk position
        Vector2Int chunkPos = new Vector2Int(
            Mathf.FloorToInt(blockPos.x / CHUNK_SIZE_X),
            Mathf.FloorToInt(blockPos.z / CHUNK_SIZE_Z)
        );
        
        // Calculate local block position within the chunk
        int localX = blockPos.x - chunkPos.x * CHUNK_SIZE_X;
        int localY = blockPos.y;
        int localZ = blockPos.z - chunkPos.y * CHUNK_SIZE_Z;
        
        // If localX or localZ are negative, adjust them and the chunk position
        if (localX < 0)
        {
            localX += CHUNK_SIZE_X;
            chunkPos.x--;
        }
        if (localZ < 0)
        {
            localZ += CHUNK_SIZE_Z;
            chunkPos.y--;
        }
        
        // Skip if the block is outside the world bounds
        if (localY < 0 || localY >= CHUNK_SIZE_Y)
            yield break;
        
        // Find the chunk gameobject
        if (chunks.TryGetValue(chunkPos, out GameObject chunkObject))
        {
            ChunkMeshGenerator meshGenerator = chunkObject.GetComponent<ChunkMeshGenerator>();
            if (meshGenerator != null)
            {
                // Get blocks data from the chunk
                BlockType[,,] blocks = new BlockType[CHUNK_SIZE_X, CHUNK_SIZE_Y, CHUNK_SIZE_Z];
                
                // Request block data from the mesh generator
                if (meshGenerator.TryGetBlockData(out blocks))
                {
                    // Set the block to air
                    blocks[localX, localY, localZ] = BlockType.Air;
                    
                    // Regenerate the mesh with updated block data
                    meshGenerator.GenerateMesh(blocks, blockAtlasMaterial, atlasSize, textureSize);
                    
                    // Also update adjacent chunks if the block is at the edge
                    if (localX == 0)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x - 1, chunkPos.y));
                    else if (localX == CHUNK_SIZE_X - 1)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x + 1, chunkPos.y));
                    
                    if (localZ == 0)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x, chunkPos.y - 1));
                    else if (localZ == CHUNK_SIZE_Z - 1)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x, chunkPos.y + 1));
                }
            }
        }
        
        // Notify minimap
        NotifyMinimapOfChunkChange(chunkPos);
        
        yield return null;
    }

    public IEnumerator PlaceBlockAt(Vector3Int blockPos, BlockType blockType)
    {
        // Calculate chunk position
        Vector2Int chunkPos = new Vector2Int(
            Mathf.FloorToInt(blockPos.x / CHUNK_SIZE_X),
            Mathf.FloorToInt(blockPos.z / CHUNK_SIZE_Z)
        );
        
        // Calculate local block position within the chunk
        int localX = blockPos.x - chunkPos.x * CHUNK_SIZE_X;
        int localY = blockPos.y;
        int localZ = blockPos.z - chunkPos.y * CHUNK_SIZE_Z;
        
        // If localX or localZ are negative, adjust them and the chunk position
        if (localX < 0)
        {
            localX += CHUNK_SIZE_X;
            chunkPos.x--;
        }
        if (localZ < 0)
        {
            localZ += CHUNK_SIZE_Z;
            chunkPos.y--;
        }
        
        // Skip if the block is outside the world bounds
        if (localY < 0 || localY >= CHUNK_SIZE_Y)
            yield break;
        
        // Find the chunk gameobject
        if (chunks.TryGetValue(chunkPos, out GameObject chunkObject))
        {
            ChunkMeshGenerator meshGenerator = chunkObject.GetComponent<ChunkMeshGenerator>();
            if (meshGenerator != null)
            {
                // Get blocks data from the chunk
                BlockType[,,] blocks = new BlockType[CHUNK_SIZE_X, CHUNK_SIZE_Y, CHUNK_SIZE_Z];
                
                // Request block data from the mesh generator
                if (meshGenerator.TryGetBlockData(out blocks))
                {
                    // Set the block to the specified type
                    blocks[localX, localY, localZ] = blockType;
                    
                    // Regenerate the mesh with updated block data
                    meshGenerator.GenerateMesh(blocks, blockAtlasMaterial, atlasSize, textureSize);
                    
                    // Also update adjacent chunks if the block is at the edge
                    if (localX == 0)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x - 1, chunkPos.y));
                    else if (localX == CHUNK_SIZE_X - 1)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x + 1, chunkPos.y));
                    
                    if (localZ == 0)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x, chunkPos.y - 1));
                    else if (localZ == CHUNK_SIZE_Z - 1)
                        RegenerateChunkAt(new Vector2Int(chunkPos.x, chunkPos.y + 1));
                }
            }
        }
        
        // Notify minimap
        NotifyMinimapOfChunkChange(chunkPos);
        
        yield return null;
    }

    private void RegenerateChunkAt(Vector2Int chunkPos)
    {
        if (chunks.TryGetValue(chunkPos, out GameObject chunkObject))
        {
            ChunkMeshGenerator meshGenerator = chunkObject.GetComponent<ChunkMeshGenerator>();
            if (meshGenerator != null)
            {
                BlockType[,,] blocks = new BlockType[CHUNK_SIZE_X, CHUNK_SIZE_Y, CHUNK_SIZE_Z];
                
                if (meshGenerator.TryGetBlockData(out blocks))
                {
                    meshGenerator.GenerateMesh(blocks, blockAtlasMaterial, atlasSize, textureSize);
                }
            }
        }
    }

    // Old method for compatibility with existing calls from the main thread
    private BlockType[,,] GenerateBlocks(Vector2Int chunkPos)
    {
        return GenerateBlocksThreadSafe(chunkPos);
    }

    // Add these methods to support the minimap
    public bool IsChunkGenerated(Vector2Int chunkPos)
    {
        return chunks.ContainsKey(chunkPos);
    }
    
    public BlockType GetTopBlockTypeAt(Vector2Int chunkPos)
    {
        if (!chunks.TryGetValue(chunkPos, out GameObject chunkObject))
            return BlockType.Air;
            
        ChunkMeshGenerator meshGenerator = chunkObject.GetComponent<ChunkMeshGenerator>();
        if (meshGenerator == null || !meshGenerator.TryGetBlockData(out BlockType[,,] blocks))
            return BlockType.Air;
            
        // Get the dominant block type in the chunk (simplified approach)
        Dictionary<BlockType, int> blockCounts = new Dictionary<BlockType, int>();
        
        for (int x = 0; x < CHUNK_SIZE_X; x++)
        {
            for (int z = 0; z < CHUNK_SIZE_Z; z++)
            {
                // Find the highest non-air block at this x,z position
                for (int y = CHUNK_SIZE_Y - 1; y >= 0; y--)
                {
                    if (blocks[x, y, z] != BlockType.Air)
                    {
                        BlockType blockType = blocks[x, y, z];
                        
                        // Count this block type
                        if (!blockCounts.ContainsKey(blockType))
                            blockCounts[blockType] = 0;
                            
                        blockCounts[blockType]++;
                        
                        // Just look at the top block in each column
                        break;
                    }
                }
            }
        }
        
        // Find the most common non-Air block type
        BlockType mostCommon = BlockType.Air;
        int maxCount = 0;
        
        foreach (var pair in blockCounts)
        {
            if (pair.Key != BlockType.Air && pair.Value > maxCount)
            {
                maxCount = pair.Value;
                mostCommon = pair.Key;
            }
        }
        
        return mostCommon;
    }

    // Add this method to get top blocks for the minimap
    public BlockType[,] GetTopBlocksInChunk(Vector2Int chunkPos)
    {
        if (!chunks.TryGetValue(chunkPos, out GameObject chunkObject))
            return null;
            
        ChunkMeshGenerator meshGenerator = chunkObject.GetComponent<ChunkMeshGenerator>();
        if (meshGenerator == null || !meshGenerator.TryGetBlockData(out BlockType[,,] blocks))
            return null;
            
        BlockType[,] topBlocks = new BlockType[CHUNK_SIZE_X, CHUNK_SIZE_Z];
        
        // Find the top non-air block for each x,z position
        for (int x = 0; x < CHUNK_SIZE_X; x++)
        {
            for (int z = 0; z < CHUNK_SIZE_Z; z++)
            {
                topBlocks[x, z] = BlockType.Air;  // Default to air
                
                // Find the highest non-air block
                for (int y = CHUNK_SIZE_Y - 1; y >= 0; y--)
                {
                    if (blocks[x, y, z] != BlockType.Air)
                    {
                        topBlocks[x, z] = blocks[x, y, z];
                        break;
                    }
                }
            }
        }
        
        return topBlocks;
    }

    // Notify minimap when a block is broken or placed
    private void NotifyMinimapOfChunkChange(Vector2Int chunkPos)
    {
        MinimapSystem minimap = FindAnyObjectByType<MinimapSystem>();
        if (minimap != null)
        {
            minimap.InvalidateChunkCache(chunkPos);
        }
    }

    // Add method to get block type at specific position
    public BlockType GetBlockTypeAt(Vector3Int blockPos)
    {
        // Calculate chunk position
        Vector2Int chunkPos = new Vector2Int(
            Mathf.FloorToInt(blockPos.x / CHUNK_SIZE_X),
            Mathf.FloorToInt(blockPos.z / CHUNK_SIZE_Z)
        );
        
        // Calculate local block position within the chunk
        int localX = blockPos.x - chunkPos.x * CHUNK_SIZE_X;
        int localY = blockPos.y;
        int localZ = blockPos.z - chunkPos.y * CHUNK_SIZE_Z;
        
        // If localX or localZ are negative, adjust them and the chunk position
        if (localX < 0)
        {
            localX += CHUNK_SIZE_X;
            chunkPos.x--;
        }
        if (localZ < 0)
        {
            localZ += CHUNK_SIZE_Z;
            chunkPos.y--;
        }
        
        // Skip if the block is outside the world bounds
        if (localY < 0 || localY >= CHUNK_SIZE_Y)
            return BlockType.Air;
        
        // Find the chunk gameobject
        if (chunks.TryGetValue(chunkPos, out GameObject chunkObject))
        {
            ChunkMeshGenerator meshGenerator = chunkObject.GetComponent<ChunkMeshGenerator>();
            if (meshGenerator != null)
            {
                // Get blocks data from the chunk
                if (meshGenerator.TryGetBlockData(out BlockType[,,] blocks))
                {
                    return blocks[localX, localY, localZ];
                }
            }
        }
        
        return BlockType.Air;
    }
}

// Types de blocs disponibles avec informations de texture
public enum BlockType
{
    Air,
    Dirt,
    Grass,
    Cobblestone,
    Stone,
    Water,
    Coal,
    Iron,
    Gold,
    Wood,
    LeavesGreen,
    LeavesBrown,
    LeavesRed,
    TransparentLeavesGreen = LeavesGreen + 16,
    TransparentLeavesBrown = LeavesBrown + 16,
    TransparentLeavesRed = LeavesRed + 16,
}

public enum BiomeType
{
    Forest,
    Autumn,
    Savanna,
}

// Informations de texture pour chaque bloc
public static class BlockData
{
    // Structure pour stocker les indices de texture pour chaque face
    public struct FaceTextures
    {
        public int up;
        public int down;
        public int front;
        public int back;
        public int left;
        public int right;

        public FaceTextures(int all)
        {
            up = down = front = back = left = right = all;
        }

        public FaceTextures(int upTex, int sideTex, int downTex)
        {
            up = upTex;
            down = downTex;
            front = back = left = right = sideTex;
        }
    }

    // Indices de texture pour chaque type de bloc
    public static FaceTextures GetTextureData(BlockType blockType)
    {
        switch (blockType)
        {
            case BlockType.Air:
                return new FaceTextures(0);
            case BlockType.Dirt:
                return new FaceTextures(1); // Dirt texture index in atlas
            case BlockType.Grass:
                return new FaceTextures(3, 2, 1); // Top, Side, Bottom
            case BlockType.Cobblestone:
                return new FaceTextures(4);
            case BlockType.Stone:
                return new FaceTextures(5);
            case BlockType.Water:
                return new FaceTextures(6);
            case BlockType.Coal:
                return new FaceTextures(7);
            case BlockType.Iron:
                return new FaceTextures(8);
            case BlockType.Gold:
                return new FaceTextures(9);
            case BlockType.Wood:
                return new FaceTextures(17, 16, 17); // Top, Side, Bottom
            case BlockType.LeavesGreen:
                return new FaceTextures(18);
            case BlockType.LeavesBrown:
                return new FaceTextures(19);
            case BlockType.LeavesRed:
                return new FaceTextures(20);
            case BlockType.TransparentLeavesGreen:
                return new FaceTextures(18+16);
            case BlockType.TransparentLeavesBrown:
                return new FaceTextures(19+16);
            case BlockType.TransparentLeavesRed:
                return new FaceTextures(20+16);
            default:
                return new FaceTextures(0);
        }
    }

    // Indique si le bloc est transparent
    public static bool IsTransparent(BlockType blockType)
    {
        return  blockType == BlockType.Air ||
                blockType == BlockType.Water || 
                blockType == BlockType.LeavesGreen ||
                blockType == BlockType.LeavesBrown ||
                blockType == BlockType.LeavesRed ||
                blockType == BlockType.TransparentLeavesGreen ||
                blockType == BlockType.TransparentLeavesBrown ||
                blockType == BlockType.TransparentLeavesRed;
    }
}