using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public struct TileWithThreshold
{
    public string name;
    public Tile tile;
    [Range(0f, 1f)]
    public float threshold;
}

[System.Serializable]
public struct ForestSettings
{
    public List<GameObject> treePrefabs;
    [Range(0f, 1f)]
    public float density;
    public List<Tile> allowedTerrainTypes;
    public Tilemap forestAllowedTilemap;
    public Tile allowedTileMark;
    public float forestsSize;
    public int minNumTreesPerCell;
    public int maxNumTreesPerCell;
    public float treesSpreadValue;
    public GameObject treesContainer;
}

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        TerrainGenerator generator = (TerrainGenerator)target;

        if (GUILayout.Button("Generate Terrain Data"))
        {
            // Generate new terrain
            Debug.Log("Generate Terrain Data");
            generator.GenerateTerrainData();
        }

        if (GUILayout.Button("Generate Terrain Mesh"))
        {
            // Generate new terrain
            Debug.Log("Generate Terrain Mesh");
            generator.GenerateTerrainMesh(generator.terrainMesh);
        }

        if (generator.elevationTextureMap!=null) { 
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField("Perlin Noise Map");
            Texture2D texture = generator.elevationTextureMap;
            Rect rect = GUILayoutUtility.GetAspectRect((float)texture.width / texture.height);
            EditorGUI.DrawPreviewTexture(rect, texture);
            EditorGUI.EndDisabledGroup();
        }

    }
}


public class TerrainGenerator : MonoBehaviour
{
    public int width;
    public int height;
    public float scale;
    public int detailsLevel; // TODO: Add range >0
    public Tilemap terrainTypeTilemap;
    public float flatTerrainMin;
    public float flatTerrainMax;
    public float flatTerrainThreshold;
    public List<TileWithThreshold> tilesWithThresholds;
    public int seed;
    public ForestSettings forestSettings;
    public GameObject terrainMesh;
    public int terrainHeight;
    public Material terrainMaterial;

    public Texture2D elevationTextureMap;

     // Helper function to remove all tiles from the tilemap
    private void ClearTerrain()
    {
        // Destroy all trees in the scene
        foreach (Transform child in forestSettings.treesContainer.transform)
        {
            Destroy(child.gameObject);
        }

        // Clear the terrain tilemaps
        terrainTypeTilemap.ClearAllTiles();
        forestSettings.forestAllowedTilemap.ClearAllTiles();
    }

    float SmoothstepBlending(float value, float min,  float max, float threshold)
    {
        if (value < min || value > max)
        {
            return value; // Clamp values outside the range to the target value
        }
        else if (value < min+threshold)
        {
            float t = Mathf.InverseLerp(min, min + threshold, value);
            return Mathf.SmoothStep(value, min + threshold, t);
        }
        else if (value > max-threshold)
        {
            float t = Mathf.InverseLerp(max - threshold, max, value);
            return Mathf.SmoothStep(value, max - threshold, t);
        }
        else
        {
            return min + ((max-min)/2);
        }
    }

    public void GenerateTerrainMesh(GameObject meshGameObject)
    {
        // Create the mesh data
        Mesh terrainMesh = new Mesh();
        Vector3[] vertices = new Vector3[width * height];
        int[] triangles = new int[(width - 1) * (height - 1) * 6];
        Vector2[] uv = new Vector2[width * height];
        int triangleIndex = 0;

        // Loop over all tiles in the grid and generate mesh data for each tile
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Get the elevation value from the elevation texture map
                Color color = elevationTextureMap.GetPixel(x, y);
                float elevation = color.r * terrainHeight;

                // Set the vertex position
                vertices[x + y * width] = new Vector3(x, elevation, y);

                // Set the UV coordinates
                uv[x + y * width] = new Vector2((float)x / width, (float)y / height);

                // Generate the triangles
                if (x < width - 1 && y < height - 1)
                {
                    int topLeft = x + y * width;
                    int topRight = (x + 1) + y * width;
                    int bottomLeft = x + (y + 1) * width;
                    int bottomRight = (x + 1) + (y + 1) * width;

                    triangles[triangleIndex] = topLeft;
                    triangles[triangleIndex + 1] = bottomLeft;
                    triangles[triangleIndex + 2] = topRight;

                    triangles[triangleIndex + 3] = topRight;
                    triangles[triangleIndex + 4] = bottomLeft;
                    triangles[triangleIndex + 5] = bottomRight;

                    triangleIndex += 6;
                }
            }
        }

        // Assign the mesh data
        terrainMesh.vertices = vertices;
        terrainMesh.triangles = triangles;
        terrainMesh.uv = uv;

        // Calculate normals and tangents for proper shading
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateTangents();

        // Assign the mesh to the MeshFilter component on the provided GameObject
        MeshFilter meshFilter = meshGameObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = terrainMesh;

            // Assign the material to the MeshRenderer component on the provided GameObject
            MeshRenderer meshRenderer = meshGameObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = meshGameObject.AddComponent<MeshRenderer>();
            }
            meshRenderer.sharedMaterial = terrainMaterial;
        }
    }


    // Helper function to generate terrain for a single tile
    private void GenerateTileTerrain(int x, int y)
    {
        // Add some randomness to the Perlin noise by using a random offset based on the seed
        float randomOffset = seed;
        float elevation = 0;


        float sampleX = (float)x / width * scale + randomOffset;
        float sampleY = (float)y / height * scale + randomOffset;

        elevation = Mathf.PerlinNoise(sampleX, sampleY);

        if (detailsLevel > 1)
        {

            float factor = detailsLevel + 3;

            float sampleX_details = (float)x / width * scale * factor/2 + randomOffset;
            float sampleY_details = (float)y / height * scale * factor/2 + randomOffset;

            float elevationDetails = Mathf.PerlinNoise(sampleX_details, sampleY_details);
            elevationDetails = (elevationDetails * 2) - 1; // scaling to [-1,1]

            elevation += elevationDetails / (factor+1);
        }

        float t = flatTerrainThreshold;
        float min = flatTerrainMin;
        float max = flatTerrainMax;

        // Flattens Plains terrain
        elevation = SmoothstepBlending(elevation, min, max, t);

        Color color = new Color(elevation, elevation, elevation);
        elevationTextureMap.SetPixel(x, y, color);

        Tile tile = null;
        foreach (var item in tilesWithThresholds)
        {
            if (elevation >= item.threshold)
                tile = item.tile;
            else if(elevation < 0)
            {
                tile = tilesWithThresholds[0].tile;
            }
        }

        if (tile != null)
        {
            terrainTypeTilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private void GenerateTerrainType()
    {
        elevationTextureMap = new Texture2D(width, height);

        // Loop over all tiles in the grid and generate terrain for each tile
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GenerateTileTerrain(x, y);
            }
        }

        elevationTextureMap.Apply();
    }

    // Main function to generate terrain for the entire grid
    public void GenerateTerrainData()
    {
        // Clean the terrain before generating
        ClearTerrain();

        // Initialize the random number generator with the seed
        Random.InitState(seed);

        GenerateTerrainType();

        GenerateForests();

    }

    private void GenerateForests()
    {
        GenerateAllowedForestArea();

        GenerateTrees();


    }

    private void GenerateAllowedForestArea()
    {
        // Loop over all tiles in the terrain type tilemap
        foreach (Vector3Int pos in terrainTypeTilemap.cellBounds.allPositionsWithin)
        {
            // Get the Perlin noise value for the current tile

            float randomOffset = seed*2;

            float sampleX = (float)pos.x / width * forestSettings.forestsSize + randomOffset;
            float sampleY = (float)pos.y / height * forestSettings.forestsSize + randomOffset;

            float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);

            // If the noise value is below the specified threshold, discard the tile
            if (noiseValue > forestSettings.density)
            {
                forestSettings.forestAllowedTilemap.SetTile(pos, null);
                continue;
            }

            TileBase terrainTypeTile = terrainTypeTilemap.GetTile(pos);

            // If the current tile is not one of the allowed terrain types, skip it
            if (!forestSettings.allowedTerrainTypes.Contains(terrainTypeTile)) continue;

            // If the current tile is one of the allowed terrain types and the noise value is above the threshold, color the corresponding tile in the forest allowed tilemap
            forestSettings.forestAllowedTilemap.SetTile(pos, forestSettings.allowedTileMark);
        }
    }

    private void GenerateTrees()
    {
        // Create a game object to hold all the trees
        forestSettings.treesContainer.transform.SetParent(transform);

        // Generate Perlin noise to determine where trees should be placed
        float randomOffset = seed * 3;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float sampleX = (float)x / width * forestSettings.treesSpreadValue + randomOffset;
                float sampleY = (float)y / height * forestSettings.treesSpreadValue + randomOffset;

                float noiseValue = Mathf.PerlinNoise(sampleX, sampleY);

                Vector3Int cellPos = new Vector3Int(x, y, 0);
                TileBase cellTile = forestSettings.forestAllowedTilemap.GetTile(cellPos);

                // If the cell is not marked as allowed for forests, skip it
                if (cellTile != forestSettings.allowedTileMark) continue;

                // Get a random number of trees to place in the current cell
                int numTrees = Mathf.RoundToInt(forestSettings.minNumTreesPerCell + (forestSettings.maxNumTreesPerCell - forestSettings.minNumTreesPerCell) * noiseValue);

                // Get a list of random positions inside the current cell to place the trees
                List<Vector3> treePositions = GetRandomPositionsInCell(cellPos, numTrees);

                // Instantiate the trees at the random positions and set their parent to the Trees game object
                foreach (Vector3 pos in treePositions)
                {
                    GameObject treePrefab = forestSettings.treePrefabs[Random.Range(0, forestSettings.treePrefabs.Count)];
                    GameObject treeInstance = Instantiate(treePrefab, pos, Quaternion.identity, forestSettings.treesContainer.transform);
                }
            }
        }
    }

    // Helper function to get a list of random positions inside a given cell
    private List<Vector3> GetRandomPositionsInCell(Vector3Int cellPos, int numPositions)
    {
        List<Vector3> positions = new List<Vector3>();

        // Get the bounds of the cell in world space
        BoundsInt cellBounds = new BoundsInt(cellPos, new Vector3Int(1, 1, 1));

        // Generate the specified number of random positions inside the cell bounds
        for (int i = 0; i < numPositions; i++)
        {

            int c = 10000;

            Vector3 randomPos = new Vector3(
                Mathf.Round(Random.Range(cellBounds.min.x * c, cellBounds.max.x * c)) / c,
                0,
                Mathf.Round(Random.Range(cellBounds.min.y * c, cellBounds.max.y * c)) / c
            );

            if (i == 0)
            {
                Debug.Log(randomPos.ToString());
            }

            positions.Add(randomPos);
        }

        return positions;
    }

}
