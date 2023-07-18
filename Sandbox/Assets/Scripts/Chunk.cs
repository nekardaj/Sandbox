using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk is a game object so the whole chunk can be disabled when player is far away
/// </summary>
public class Chunk : MonoBehaviour
{
    public static readonly int ChunkSize = 16;
    public static readonly int ChunkHeight = 256;
    

    // another optimization might be to disable chunks that are not visible to the player
    // eg using heuristic using lowest and highest block in the chunk
    public Chunk[] neighbors = new Chunk[4]; // 4 neighbors
    
    // these values can be used for optimization
    public int minimalHeight;
    public int maximalHeight;

    public void Initialize(int x, int z)
    {
        this.x = x;
        this.z = z;
        modified = new bool[ChunkSize,ChunkSize]; // default value is false so we dont need to initialize it
        columns = new Column[ChunkSize,ChunkSize];
        for (int i = 0; i < ChunkSize; i++)
        {
            for (int j = 0; j < ChunkSize; j++)
            {
                columns[i,j] = new UnmodifiedColumn(x + i, z + j);
            }
        }
        for (int i = 0; i < ChunkSize; i++)
        {
            for (int j = 0; j < ChunkSize; j++)
            {
                GameObject gridElement = Instantiate(GridControl.GridElementPrefab[(int)BlockType.Grass], transform);
                gridElement.name = "GridElement_" + i + "," + j;
                int height = (int)(4 * Mathf.PerlinNoise(i / 4f, j / 4f));
                gridElement.transform.position = GridControl.grid.GetCellCenterWorld(new Vector3Int( x * ChunkSize + i, height, z * ChunkSize + j));
            }
        }
    }

    public void BlockDestroyed(Vector3Int position)
    {
        // calculate relative position in chunk
        int x = position.x - this.x * ChunkSize;
        int z = position.z - this.z * ChunkSize;
        // notify the column
        columns[x,z].BlockDestroyed(position.x, position.y, position.z);
    }

    private int x;
    private int z;

    private Column[,] columns = new Column[ChunkSize, ChunkSize];
    private bool[,] modified = new bool[ChunkSize, ChunkSize];

}

// One of the invariants that should be preserved is this: all the chunks around the player are spawned
// this ensures that whenever a block is destroyed or placed we only need to check the targeted chunk and pass information
// to the column and its neighbors
// if chunk was not modified by the player we can optimize by spawning only top layer
// and any blocks that are not covered by other blocks from all directions

public abstract class Column
{
    // y coord is enough?
    // block destruction forces an instantiation of a new block
    // so adding some information to it wont be a problem (eg a bitmask)
    public abstract void BlockDestroyed(int x, int y, int z);
    public abstract void BlockPlaced(int x, int y, int z);
    /*
    // because all neighbors are not instantiated during constructor of column we need to check which blocks to spawn after
    public abstract void Update()
    {

    }
    */
}

/// <summary>
/// If the column was not modified by player we only need to remember few values
/// Almost everything can be calculated from scratch because the terrain generation is deterministic
/// but caching some information helps and uses very little memory compared to modified columns
/// </summary>
public class UnmodifiedColumn : Column
{
    // instantiate needs some transform as an argument
    private static Transform transform = new GameObject().transform;
    public override void BlockDestroyed(int x, int y, int z)
    {
        GameObject gridElement = GameObject.Instantiate(GridControl.GridElementPrefab[(int)BlockType.Grass], transform);
        gridElement.name = "GridElement_" + x + "," + z;
        gridElement.transform.position = GridControl.grid.GetCellCenterWorld(new Vector3Int(x , y - 1, z ));
    }

    public override void BlockPlaced(int x, int z, int y)
    {
        
    }

    public UnmodifiedColumn(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public int x;
    public int z;
    public int height;
    // having entry for every spawned cube is not ideal however spawning the Game Object is bigger compared to one entry
    private SortedDictionary<int, GameObject> cubes = new SortedDictionary<int, GameObject>();
    public List<int> layerHeight = new List<int>((int)BlockType.Count);


}

public class ModifiedColumn : Column
{
    public override void BlockDestroyed(int x, int y, int z)
    {
        throw new System.NotImplementedException();
    }
    // add list of tuples height and type of block to save memory
    // worst case it is twice as much as if saving 256 enum entries but in most cases it will be much less
    public override void BlockPlaced(int x, int y, int z)
    {
        throw new System.NotImplementedException();
    }
}
