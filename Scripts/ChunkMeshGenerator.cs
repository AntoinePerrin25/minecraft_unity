using UnityEngine;
using System.Collections.Generic;

public class ChunkMeshGenerator : MonoBehaviour
{
    private const float TEXTURE_PADDING = 0.001f; // Padding to prevent texture bleeding

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private BlockType[,,] blockData;
    private bool hasBlockData = false;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }

    public void GenerateMesh(BlockType[,,] blocks, Material atlasMaterial, int atlasSize, float textureSize)
    {
        // Store the block data
        blockData = blocks;
        hasBlockData = true;

        int chunkSizeX = blocks.GetLength(0);
        int chunkSizeY = blocks.GetLength(1); 
        int chunkSizeZ = blocks.GetLength(2);
        
        // Préallouer des listes de taille suffisante (évite les redimensionnements)
        int estimatedBlockCount = chunkSizeX * chunkSizeY * chunkSizeZ / 2; // ~50% de blocs visibles
        int estimatedVertices = estimatedBlockCount * 4 * 6; // 4 vertices par face, ~6 faces visibles par bloc
        
        List<Vector3> vertices = new List<Vector3>(estimatedVertices);
        List<int> triangles = new List<int>(estimatedVertices / 4 * 6); // 6 indices par face
        List<Vector2> uvs = new List<Vector2>(estimatedVertices);

        // Parcourir tous les blocs
        for (int x = 0; x < chunkSizeX; x++)
        {
            for (int y = 0; y < chunkSizeY; y++)
            {
                for (int z = 0; z < chunkSizeZ; z++)
                {
                    if (blocks[x, y, z] == BlockType.Air)
                        continue;

                    AddBlockToMesh(blocks, x, y, z, vertices, triangles, uvs, chunkSizeX, chunkSizeY, chunkSizeZ, atlasSize);
                }
            }
        }

        // Créer le mesh
        
        /*
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        */

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support pour plus de vertices
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);

        mesh.Optimize();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Assigner le mesh
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;
        meshRenderer.material = atlasMaterial;
    }

    public bool TryGetBlockData(out BlockType[,,] blocks)
    {
        if (hasBlockData)
        {
            blocks = blockData;
            return true;
        }
        
        blocks = null;
        return false;
    }

    private void AddBlockToMesh(BlockType[,,] blocks, int x, int y, int z, 
                               List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
                               int sizeX, int sizeY, int sizeZ, int atlasSize)
    {
        BlockType blockType = blocks[x, y, z];
        BlockData.FaceTextures textures = BlockData.GetTextureData(blockType);
        
        // Face inférieure
        if (y - 1 < 0 || BlockData.IsTransparent(blocks[x, y - 1, z]))
        {
            AddFace(new Vector3(x, y, z+1),             // Point de départ
                    new Vector3(1, 0, 0), // Direction horizontale
                    new Vector3(0, 0, -1),            // Direction verticale
                    vertices, triangles, uvs,
                    GetTextureUVs(textures.down, atlasSize));
        }

        // Face supérieure
        if (y + 1 >= sizeY || BlockData.IsTransparent(blocks[x, y + 1, z]))
        {
            AddFace(new Vector3(x, y + 1, z),         // Point de départ
                    new Vector3(1, 0, 0),             // Direction horizontale
                    new Vector3(0, 0, 1),             // Direction verticale
                    vertices, triangles, uvs, 
                    GetTextureUVs(textures.up, atlasSize));
        }

        // Face avant (Z+)
        if (z + 1 >= sizeZ || BlockData.IsTransparent(blocks[x, y, z + 1]))
        {
            AddFace(new Vector3(x+1, y, z + 1),         // Point de départ
                    new Vector3(-1, 0, 0),             // Direction horizontale
                    new Vector3(0, 1, 0),             // Direction verticale
                    vertices, triangles, uvs,
                    GetTextureUVs(textures.front, atlasSize));
        }

        // Face arrière (Z-)
        if (z - 1 < 0 || BlockData.IsTransparent(blocks[x, y, z - 1]))
        {
            AddFace(new Vector3(x, y, z),         // Point de départ
                    new Vector3(1, 0, 0),            // Direction horizontale
                    new Vector3(0, 1, 0),             // Direction verticale
                    vertices, triangles, uvs,
                    GetTextureUVs(textures.back, atlasSize));
        }

        // Face gauche (X-)
        if (x - 1 < 0 || BlockData.IsTransparent(blocks[x - 1, y, z]))
        {
            // Correction: inverser l'ordre des sommets pour avoir la face vers l'extérieur
            AddFace(new Vector3(x, y, z + 1),         // Point de départ modifié
                    new Vector3(0, 0, -1),            // Direction horizontale modifiée
                    new Vector3(0, 1, 0),             // Direction verticale
                    vertices, triangles, uvs,
                    GetTextureUVs(textures.left, atlasSize));
        }

        // Face droite (X+)
        if (x + 1 >= sizeX || BlockData.IsTransparent(blocks[x + 1, y, z]))
        {
            // Correction: inverser l'ordre des sommets pour avoir la face vers l'extérieur
            AddFace(new Vector3(x + 1, y, z),         // Point de départ modifié
                    new Vector3(0, 0, 1),             // Direction horizontale modifiée
                    new Vector3(0, 1, 0),             // Direction verticale
                    vertices, triangles, uvs,
                    GetTextureUVs(textures.right, atlasSize));
        }
    }

    private void AddFace(Vector3 origin, Vector3 right, Vector3 up,
                        List<Vector3> vertices, List<int> triangles, List<Vector2> uvs,
                        Vector2[] textureCoords)
    {
        int vertCount = vertices.Count;

        // Ajouter les 4 sommets de la face
        vertices.Add(origin);                   // 0
        vertices.Add(origin + right);           // 1
        vertices.Add(origin + right + up);      // 2
        vertices.Add(origin + up);              // 3

        // Ajouter les UV pour la texture
        uvs.Add(textureCoords[0]); // Bas gauche
        uvs.Add(textureCoords[1]); // Bas droite
        uvs.Add(textureCoords[2]); // Haut droite
        uvs.Add(textureCoords[3]); // Haut gauche

        // Ajouter les indices pour les deux triangles de la face
        triangles.Add(vertCount);
        triangles.Add(vertCount + 3);
        triangles.Add(vertCount + 1);

        triangles.Add(vertCount + 1);
        triangles.Add(vertCount + 3);
        triangles.Add(vertCount + 2);
    }

    private Vector2[] GetTextureUVs(int textureIndex, int atlasSize)
    {
        // Calculer les coordonnées x et y dans l'atlas
        int x = textureIndex % atlasSize;
        int y = atlasSize - 1 - (textureIndex / atlasSize); // Inversion de l'axe Y pour compatibilité UV

        // Calculer les coordonnées UV avec padding pour éviter le bleeding
        float uvUnit = 1f / atlasSize;
        float uvPadding = TEXTURE_PADDING / atlasSize;

        // Ordre: Bas gauche, Bas droite, Haut droite, Haut gauche
        return new Vector2[]
        {
            new Vector2(x * uvUnit + uvPadding, y * uvUnit + uvPadding),
            new Vector2((x + 1) * uvUnit - uvPadding, y * uvUnit + uvPadding),
            new Vector2((x + 1) * uvUnit - uvPadding, (y + 1) * uvUnit - uvPadding),
            new Vector2(x * uvUnit + uvPadding, (y + 1) * uvUnit - uvPadding)
        };
    }
}