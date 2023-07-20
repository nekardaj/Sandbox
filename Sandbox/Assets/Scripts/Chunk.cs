using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Direction
{
    North,
    East,
    South,
    West
}

/// <summary>
/// Chunk is a game object so the whole chunk can be disabled when player is far away
/// </summary>
public class Chunk : MonoBehaviour
{
    public static readonly int ChunkSize = 16;
    public static readonly int ChunkHeight = 256;

    private static readonly int MaxAmplitude = 16;
    private static readonly float StartFrequency = 0.0625f;
    private static readonly int Octaves = 3;
    public static int PerlinNoise(int x, int z)
    {
        int amplitude = MaxAmplitude;
        float frequency = StartFrequency;
        int value = 0;
        for (int i = 0; i < Octaves; i++)
        {
            value += (int)(amplitude * Mathf.PerlinNoise(x * frequency, z * frequency));
            amplitude /= 2;
            frequency *= 2;
        }

        return value;
    }
    private static readonly int MinimalSnowHeight = 20;
    private static readonly int MinimalGrassHeight = 8;
    private static readonly int NormalGrassLayer = 4;
    /// <summary>
    /// Using only height to determine block type would look bad
    /// So I am going use fixed grass and snow layer width and add some noise to it 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    public static BlockType GetBlockType(int x, int y, int z)
    {
        //int height = PerlinNoise(x, z);
        if (y > 19)
        {
            return BlockType.Snow;
        }
        else if (y > 10)
        {
            return BlockType.Grass;
        }
        else
        {
            return BlockType.Rock;
        }
    }

    // another optimization might be to disable chunks that are not visible to the player
    // eg using heuristic using lowest and highest block in the chunk
    public Chunk[] neighbors = new Chunk[4]; // 4 neighbors

    public void RegisterNeighbor(Chunk chunk, Direction direction)
    {
        neighbors[(int)direction] = chunk;
    }

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
                columns[i,j] = new UnmodifiedColumn(x * ChunkSize + i, z * ChunkSize + j);
            }
        }
    }

    public void BlockDestroyed(Vector3Int position)
    {
        // calculate relative position in chunk
        int x = position.x - this.x * ChunkSize;
        int z = position.z - this.z * ChunkSize;
        // notify the column
        Column column = columns[x, z];
        if (column.GetType() == typeof(UnmodifiedColumn))
        {
            //columns[x,z] = column.Deconstruct()
        }
        column.BlockDestroyed(position.x, position.y, position.z, this);
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

    /// <summary>
    /// When a block is destroyed usually new one becomes visible and must be instantiated
    /// We need to set the parent to chunk so disabling works, so we need to pass the reference
    /// </summary>
    /// <param name="chunk">Chunk owning this column</param>
    public abstract void BlockDestroyed(int x, int y, int z, Chunk chunk);
    public abstract void BlockPlaced(int x, int y, int z, Chunk chunk);
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
    public override void BlockDestroyed(int x, int y, int z, Chunk chunk)
    {
        GameObject gridElement = GameObject.Instantiate(GridControl.GridElementPrefab[(int) Chunk.GetBlockType(x,y - 1, z)], transform);
        gridElement.name = "GridElement_" + x + "," + z;
        gridElement.transform.position = GridControl.grid.GetCellCenterWorld(new Vector3Int(x , y - 1, z ));
        blocks.Remove(y - 1);
        blocks.Add(y - 1, gridElement);

        gridElement.transform.parent = chunk.transform; // all chunks have 0,0,0 as their position
    }

    /// <summary>
    /// Convert data to modified column and return it
    /// </summary>
    /// <returns>This column data represented by ModifiedColumn class</returns>
    public ModifiedColumn Construct()
    {
        //ModifiedColumn column = new ModifiedColumn(x, z);
        List<Tuple<int, BlockType>> blocks = new List<Tuple<int, BlockType>>();
        //indexof key
        // .keys property and use binary search to find upper bound
        //return column;
        // keep it simple, this does not happen often
        return null;
    }
    public override void BlockPlaced(int x, int z, int y, Chunk chunk)
    {
        
    }

    public UnmodifiedColumn(int x, int z)
    {
        this.x = x;
        this.z = z;
        // top block of new chunk can always be seen
        int height = Chunk.PerlinNoise(x, z);
        GameObject gridElement = GameObject.Instantiate(GridControl.GridElementPrefab[(int)Chunk.GetBlockType(x, height, z)], transform);
        gridElement.name = "GridElement_" + x + "," + z;
        gridElement.transform.position = GridControl.grid.GetCellCenterWorld(new Vector3Int(x, height, z));
        blocks.Add(height, gridElement);
    }

    public int x;
    public int z;
    public int height;
    // having entry for every spawned cube is not ideal however spawning the Game Object is bigger compared to one entry
    private SortedDictionary<int, GameObject> blocks = new SortedDictionary<int, GameObject>();
    public List<int> layerHeight = new List<int>((int)BlockType.Count);


}

public class ModifiedColumn : Column
{
    private SortedDictionary<int, BlockType> blocks;
    public override void BlockDestroyed(int x, int y, int z, Chunk chunk)
    {
        throw new System.NotImplementedException();
    }
    // add list of tuples height and type of block to save memory
    // worst case it is twice as much as if saving 256 enum entries but in most cases it will be much less

    private SortedDictionary<int, GameObject> cubes = new SortedDictionary<int, GameObject>();
    public override void BlockPlaced(int x, int y, int z, Chunk chunk)
    {
        throw new System.NotImplementedException();
    }
}
