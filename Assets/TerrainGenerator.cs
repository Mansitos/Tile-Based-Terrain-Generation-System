using Palmmedia.ReportGenerator.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

[System.Serializable]
public struct TileWithThreshold
{
    public string name;
    public Tile tile;
    [Range(0f, 1f)]
    public float threshold;
    public GameObject terrainMesh;
    public Material material;
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
            Debug.Log("Generate Terrain Data");
            generator.GenerateTerrainData();
        }

        if (GUILayout.Button("Generate Forests"))
        {
            Debug.Log("Generate Forests");
            generator.GenerateForests();
        }

        if (GUILayout.Button("Wipe Forests"))
        {
            Debug.Log("Wipe Forests");
            generator.WipeForests();
        }

        if (GUILayout.Button("Generate Terrain Mesh"))
        {
            Debug.Log("Generate Terrain Mesh");
            generator.GenerateTerrainMesh(generator.terrainMesh, 1/generator.getInternalResolutionFactor());
        }

        // Update terrain heigthmap visualisation on inspector
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
    public int width; // in meters
    public int height; // in meters

    private int widthInternal;  // in cells
    private int heightInternal;  // in cells

    public float scale;
    public int detailsLevel; // TODO: Add range >0
    [Range(1, 4)]
    public int resolutionLevel = 1;
    private float internalResolutionFactor;

    public Tilemap terrainTypeTilemap;
    public float flatTerrainMin;
    public float flatTerrainMax;
    public float flatTerrainThreshold;
    public List<TileWithThreshold> tilesWithThresholds;
    public int seed;
    public ForestSettings forestSettings;
    public GameObject terrainMesh;
    public int terrainHeight;
    public float mountainsHeightMultiplier;
    public Material terrainMaterial;
    public Texture2D elevationTextureMap;

    public Material material1;
    public Material material2;
    public Material material3;

    // Helper function to remove all tiles from the tilemap
    private void ClearTerrainData()
    {
        WipeForests();
        terrainTypeTilemap.ClearAllTiles();
        forestSettings.forestAllowedTilemap.ClearAllTiles();
    }

    public float getInternalResolutionFactor()
    {
        return internalResolutionFactor;
    }

    float PlainsBlending(float value, float min,  float max)
    {
        if (value <= min)
        {
            return value;

        }
        else if (value >= max)
        {
            return (value - ((max - min))) * mountainsHeightMultiplier;

        }
        else if (value < max && value > min)
        {
            return min;
        }
        else
        {
            return value;
        }
    }


    public void WipeForests()
    {
        // Destroy all trees in the scene
        foreach (Transform child in forestSettings.treesContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }



    private Material CalculateMaterialBasedOnHeight(float height)
    {
        // Customize this method to assign different materials based on height thresholds
        if (height > 0.3)
        {
            // Return Material 1
            return material1;
        }
        else if (height > 0.6)
        {
            // Return Material 2
            return material2;
        }
        else
        {
            // Return Material 3
            return material3;
        }
    }

    public void GenerateTerrainMesh(GameObject meshGameObject, float unitDistance)
    {
        // Create the mesh data
        Mesh terrainMesh = new Mesh();
        Vector3[] vertices = new Vector3[widthInternal * heightInternal];
        int[] triangles = new int[(widthInternal - 1) * (heightInternal - 1) * 6];
        Vector2[] uv = new Vector2[widthInternal * heightInternal];
        int triangleIndex = 0;

        // Loop over all tiles in the grid and generate mesh data for each tile
        for (int x = 0; x < widthInternal; x++)
        {
            for (int y = 0; y < heightInternal; y++)
            {
                // Get the elevation value from the elevation texture map
                Color color = elevationTextureMap.GetPixel(x, y);
                float elevation = color.r * terrainHeight;

                // Set the vertex position with the custom unit distance
                vertices[x + y * widthInternal] = new Vector3(x * unitDistance, elevation, y * unitDistance);

                // Set the UV coordinates
                uv[x + y * widthInternal] = new Vector2((float)x / widthInternal, (float)y / heightInternal);

                // Generate the triangles
                if (x < widthInternal - 1 && y < heightInternal - 1)
                {
                    int topLeft = x + y * widthInternal;
                    int topRight = (x + 1) + y * widthInternal;
                    int bottomLeft = x + (y + 1) * widthInternal;
                    int bottomRight = (x + 1) + (y + 1) * widthInternal;

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


        float sampleX = (float)x / widthInternal * scale + randomOffset;
        float sampleY = (float)y / heightInternal * scale + randomOffset;

        elevation = Mathf.PerlinNoise(sampleX, sampleY);

        if (detailsLevel > 1)
        {

            float factor = detailsLevel + 3;

            float sampleX_details = (float)x / widthInternal * scale * factor/2 + randomOffset;
            float sampleY_details = (float)y / heightInternal * scale * factor/2 + randomOffset;

            float elevationDetails = Mathf.PerlinNoise(sampleX_details, sampleY_details);
            elevationDetails = (elevationDetails * 2) - 1; // scaling to [-1,1]

            elevation += elevationDetails / (factor+1);
        }

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

        float t = flatTerrainThreshold;
        float min = flatTerrainMin;
        float max = flatTerrainMax;

        // Flattens Plains terrain
        elevation = PlainsBlending(elevation, min + t, max - t);

        Color color = new Color(elevation, elevation, elevation);
        elevationTextureMap.SetPixel(x, y, color);
    }

    private void GenerateTerrainType()
    {
        elevationTextureMap = new Texture2D(widthInternal, heightInternal);

        // Loop over all tiles in the grid and generate terrain for each tile
        for (int x = 0; x < widthInternal; x++)
        {
            for (int y = 0; y < heightInternal; y++)
            {
                GenerateTileTerrain(x, y);
            }
        }

        elevationTextureMap.Apply();
    }

    private void InitialiseGeneratorVariables()
    {
        internalResolutionFactor = (float)(Mathf.Pow(2,resolutionLevel-1));
        terrainTypeTilemap.transform.localScale = new Vector3(1 / internalResolutionFactor, 1 / internalResolutionFactor, 1 / internalResolutionFactor);
        forestSettings.forestAllowedTilemap.transform.localScale = new Vector3(1 / internalResolutionFactor, 1 / internalResolutionFactor, 1 / internalResolutionFactor);

        heightInternal = height * (int)internalResolutionFactor;
        widthInternal = width * (int)internalResolutionFactor;
    }

    public void GenerateTerrainData()
    {
        InitialiseGeneratorVariables();
        ClearTerrainData();
        Random.InitState(seed); // Initialize the random number generator with the seed
        GenerateTerrainType();
    }

    public void GenerateForests()
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

            float sampleX = (float)pos.x / widthInternal * forestSettings.forestsSize + randomOffset;
            float sampleY = (float)pos.y / heightInternal * forestSettings.forestsSize + randomOffset;

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
        int internalResFactor = (int)internalResolutionFactor;
        

        for (int x = 0; x < widthInternal; x+= internalResFactor)
        {
            for (int y = 0; y < heightInternal; y+= internalResFactor)
            {
                float sampleX = (float)x / widthInternal * forestSettings.treesSpreadValue + randomOffset;
                float sampleY = (float)y / heightInternal * forestSettings.treesSpreadValue + randomOffset;

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
        BoundsInt cellBounds = new BoundsInt(cellPos, new Vector3Int(1* (int)internalResolutionFactor, 1 * (int)internalResolutionFactor, 1* (int)internalResolutionFactor));

        // Generate the specified number of random positions inside the cell bounds
        for (int i = 0; i < numPositions; i++)
        {

            int c = 10000;

            Vector3 randomPos = new Vector3(
                Mathf.Round(Random.Range(cellBounds.min.x * c, cellBounds.max.x * c)) / c,
                0,
                Mathf.Round(Random.Range(cellBounds.min.y * c, cellBounds.max.y * c)) / c
            );

            randomPos = new Vector3(randomPos.x / internalResolutionFactor, 0, randomPos.z / internalResolutionFactor);

            positions.Add(randomPos);
        }

        return positions;
    }

}
