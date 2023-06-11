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

        if (GUILayout.Button("Generate Terrain"))
        {
            // Generate new terrain
            generator.GenerateTerrain();
        }
    }
}

public class TerrainGenerator : MonoBehaviour
{
    public int width;
    public int height;
    public float scale;
    public Tilemap terrainTypeTilemap;
    public List<TileWithThreshold> tilesWithThresholds;
    public int seed;
    public ForestSettings forestSettings;

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

    // Helper function to generate terrain for a single tile
    private void GenerateTileTerrain(int x, int y)
    {
        // Add some randomness to the Perlin noise by using a random offset based on the seed
        float randomOffset = seed;

        float sampleX = (float)x / width * scale + randomOffset;
        float sampleY = (float)y / height * scale + randomOffset;

        float elevation = Mathf.PerlinNoise(sampleX, sampleY);
        

        Tile tile = null;
        foreach (var item in tilesWithThresholds)
        {
            if (elevation >= item.threshold)
                tile = item.tile;
        }

        if (tile != null)
        {
            terrainTypeTilemap.SetTile(new Vector3Int(x, y, 0), tile);
        }
    }

    private void GenerateTerrainType()
    {
        // Loop over all tiles in the grid and generate terrain for each tile
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GenerateTileTerrain(x, y);
            }
        }
    }

    // Main function to generate terrain for the entire grid
    public void GenerateTerrain()
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



