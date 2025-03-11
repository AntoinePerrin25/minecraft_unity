using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ChunkGenerator : MonoBehaviour
{
    public GameObject chunkPrefab;
    public Material blockAtlasMaterial; // Material with texture atlas instead of separate materials
    public int atlasSize = 16; // Atlas is a 16x16 grid of textures
    public float textureSize = 16f; // Each texture is 16x16 pixels
    
    public int seed = 0;
    public int renderDistance = 3;
    
    private const int CHUNK_SIZE_X = 16;
    private const int CHUNK_SIZE_Y = 384;
    private const int CHUNK_SIZE_Z = 16;
    
    private const float TERRAIN_SCALE = 0.05f;
    private const float TERRAIN_HEIGHT_MULTIPLIER = 80f;
    private const float BASE_TERRAIN_HEIGHT = 128f;
    
    private const float CAVES_SCALE = 0.1f;
    private const float CAVES_THRESHOLD = 0.4f;
    
    private const float ORE_SCALE = 0.2f;
    private const float COAL_THRESHOLD = 0.7f;
    private const float IRON_THRESHOLD = 0.8f;
    private const float GOLD_THRESHOLD = 0.9f;
    
    private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int currentChunk = new Vector2Int(0, 0);
    
    private void Start()
    {
        // Initialiser le générateur de nombres aléatoires avec la seed
        Random.InitState(seed);
        
        // Générer les chunks autour du joueur
        UpdateChunks();
    }
    
    private void Update()
    {
        // Obtenir la position du joueur
        Vector3 playerPos = transform.position;
        
        // Calculer dans quel chunk se trouve le joueur
        Vector2Int playerChunk = new Vector2Int(
            Mathf.FloorToInt(playerPos.x / CHUNK_SIZE_X),
            Mathf.FloorToInt(playerPos.z / CHUNK_SIZE_Z)
        );
        
        // Si le joueur a changé de chunk
        if (playerChunk != currentChunk)
        {
            currentChunk = playerChunk;
            UpdateChunks();
        }
    }
    
    private void UpdateChunks()
    {
        // Liste des chunks à conserver
        HashSet<Vector2Int> chunksToKeep = new HashSet<Vector2Int>();
        
        // Générer les chunks autour du joueur
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2Int chunkPos = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                chunksToKeep.Add(chunkPos);
                
                if (!chunks.ContainsKey(chunkPos))
                {
                    // Générer un nouveau chunk
                    GenerateChunk(chunkPos);
                }
            }
        }
        
        // Supprimer les chunks trop éloignés
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
    
    private void GenerateChunk(Vector2Int chunkPos)
    {
        // Créer un nouvel objet chunk
        GameObject chunk = Instantiate(chunkPrefab, new Vector3(chunkPos.x * CHUNK_SIZE_X, 0, chunkPos.y * CHUNK_SIZE_Z), Quaternion.identity);
        chunk.name = "Chunk_" + chunkPos.x + "_" + chunkPos.y;
        chunks.Add(chunkPos, chunk);
        
        // Générer le mesh du chunk
        ChunkMeshGenerator meshGenerator = chunk.GetComponent<ChunkMeshGenerator>();
        if (meshGenerator != null)
        {
            // Récupérer les blocs pour ce chunk
            BlockType[,,] blocks = GenerateBlocks(chunkPos);
            
            // Générer le mesh avec le matériau de l'atlas
            meshGenerator.GenerateMesh(blocks, blockAtlasMaterial, atlasSize, textureSize);
        }
    }
    
    private BlockType[,,] GenerateBlocks(Vector2Int chunkPos)
    {
        BlockType[,,] blocks = new BlockType[CHUNK_SIZE_X, CHUNK_SIZE_Y, CHUNK_SIZE_Z];
        
        // Générer le terrain
        for (int x = 0; x < CHUNK_SIZE_X; x++)
        {
            for (int z = 0; z < CHUNK_SIZE_Z; z++)
            {
                // Calculer les coordonnées absolues dans le monde
                float worldX = chunkPos.x * CHUNK_SIZE_X + x;
                float worldZ = chunkPos.y * CHUNK_SIZE_Z + z;
                
                // Générer la hauteur du terrain avec le bruit de Perlin
                float terrainHeight = GenerateTerrainHeight(worldX, worldZ);
                int heightInt = Mathf.FloorToInt(terrainHeight);
                
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
                        if (ShouldBeACave(worldX, y, worldZ))
                        {
                            blocks[x, y, z] = BlockType.Air;
                        }
                        // Générer les minerais
                        else
                        {
                            blocks[x, y, z] = GenerateOre(worldX, y, worldZ, blocks[x, y, z]);
                        }
                    }
                }
                
                // Générer les arbres (1% de chance sur chaque bloc d'herbe)
                if (Random.value < 0.01f && blocks[x, heightInt, z] == BlockType.Grass)
                {
                    GenerateTree(blocks, x, heightInt + 1, z);
                }
            }
        }
        
        return blocks;
    }
    
    private float GenerateTerrainHeight(float x, float z)
    {
        // Utiliser plusieurs octaves de bruit de Perlin pour la génération de terrain
        float height = 0;
        
        // Première couche: collines de base
        height += Mathf.PerlinNoise(x * TERRAIN_SCALE + seed, z * TERRAIN_SCALE + seed) * TERRAIN_HEIGHT_MULTIPLIER;
        
        // Deuxième couche: petites variations
        height += Mathf.PerlinNoise(x * TERRAIN_SCALE * 2 + seed + 100, z * TERRAIN_SCALE * 2 + seed + 100) * TERRAIN_HEIGHT_MULTIPLIER * 0.5f;
        
        // Troisième couche: grandes formations
        height += Mathf.PerlinNoise(x * TERRAIN_SCALE * 0.5f + seed + 200, z * TERRAIN_SCALE * 0.5f + seed + 200) * TERRAIN_HEIGHT_MULTIPLIER * 1.5f;
        
        // Normaliser la hauteur
        height = height / 3.0f + BASE_TERRAIN_HEIGHT;
        
        return height;
    }
    
    private bool ShouldBeACave(float x, float y, float z)
    {
        // Implémentation de bruit 3D compatible avec Unity
        float cavesNoise = Perlin3D(
            x * CAVES_SCALE + seed + 300,
            y * CAVES_SCALE + seed + 300,
            z * CAVES_SCALE + seed + 300
        );
        
        return cavesNoise > CAVES_THRESHOLD;
    }
    
    private BlockType GenerateOre(float x, float y, float z, BlockType currentBlock)
    {
        // Ne générer des minerais que dans la pierre
        if (currentBlock != BlockType.Stone)
            return currentBlock;
        
        // Implémentation de bruit 3D compatible avec Unity
        float oreNoise = Perlin3D(
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
    
    private void GenerateTree(BlockType[,,] blocks, int x, int y, int z)
    {
        // Vérifier si l'arbre peut être placé (espace suffisant)
        if (y + 4 >= CHUNK_SIZE_Y || x <= 1 || x >= CHUNK_SIZE_X - 2 || z <= 1 || z >= CHUNK_SIZE_Z - 2)
            return;
        
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
                    blocks[x + leafX, y + 3, z + leafZ] = BlockType.Leaves;
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
                    blocks[x + leafX, y + 4, z + leafZ] = BlockType.Leaves;
                }
            }
        }
        
        // Feuille supérieure
        if (y + 5 < CHUNK_SIZE_Y)
        {
            blocks[x, y + 5, z] = BlockType.Leaves;
        }
    }
    
    // Implémentation personnalisée de bruit 3D basée sur le bruit de Perlin 2D
    private float Perlin3D(float x, float y, float z)
    {
        // Utiliser plusieurs couches de Perlin 2D pour simuler du 3D
        float xy = Mathf.PerlinNoise(x, y);
        float yz = Mathf.PerlinNoise(y, z);
        float xz = Mathf.PerlinNoise(x, z);
        
        float yx = Mathf.PerlinNoise(y, x);
        float zy = Mathf.PerlinNoise(z, y);
        float zx = Mathf.PerlinNoise(z, x);
        
        // Combiner les résultats
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
    Leaves
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
            case BlockType.Leaves:
                return new FaceTextures(18);
            default:
                return new FaceTextures(0);
        }
    }

    // Indique si le bloc est transparent
    public static bool IsTransparent(BlockType blockType)
    {
        return blockType == BlockType.Air || blockType == BlockType.Water || blockType == BlockType.Leaves;
    }
}