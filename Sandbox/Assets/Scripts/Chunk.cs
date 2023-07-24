using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

public enum Direction
{
    North,
    East,
    West,
    South
}

/// <summary>
/// Chunk is a game object so the whole chunk can be disabled when player is far away
/// </summary>
public class Chunk : MonoBehaviour
{
    public static readonly int ChunkSize = 16;
    public static readonly int ChunkHeight = 256;

    private static readonly int MaxAmplitude = 32;
    private static readonly float StartFrequency = 0.0625f;
    private static readonly int Octaves = 3;

    private static int seedX;
    private static int seedZ;

    public static void InitializeSeed()
    {
        seedX = UnityEngine.Random.Range(0, 1 << 20);
        seedZ = UnityEngine.Random.Range(0, 1 << 20);
        Debug.Log("seed:" + seedX + " " + seedZ);
    }

    public static int PerlinNoise(int x, int z)
    {
        int amplitude = MaxAmplitude;
        float frequency = StartFrequency;
        int value = 0;
        for (int i = 0; i < Octaves; i++)
        {
            //value += (int)(amplitude * Mathf.PerlinNoise(x * frequency + seedX, z * frequency + seedZ));
            value += (int)(amplitude * Mathf.PerlinNoise((x + seedX) * frequency, (z + seedZ) * frequency));
            //value += (int)(amplitude * Mathf.PerlinNoise(x * frequency, z * frequency));
            amplitude /= 2;
            frequency *= 2;
        }

        return value;
    }

    public static List<int> ConstructLayers(int x, int z)
    {
        int height = PerlinNoise(x, z);
        List<int> layers = new List<int>();
        if (height > MinimalGrassHeight)
        {
            int grassStart = 0;

        }

        return null;
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
        modified = false;
        columns = new Column[ChunkSize,ChunkSize];
        for (int i = 0; i < ChunkSize; i++)
        {
            for (int j = 0; j < ChunkSize; j++)
            {
                columns[i,j] = new Column(x * ChunkSize + i, z * ChunkSize + j, this);
            }
        }
    }

    public BlockType GetTypeAt(Vector3Int position)
    {
        int x = position.x - this.x * ChunkSize;
        int z = position.z - this.z * ChunkSize;
        // notify the column
        Column column = columns[x, z];
        return column.GetTypeAt(position);
    }

    public BlockType BlockDestroyed(Vector3Int position)
    {
        // calculate relative position in chunk
        modified = true;
        int x = position.x - this.x * ChunkSize;
        int z = position.z - this.z * ChunkSize;
        // notify the column
        Column column = columns[x, z];
        for (int i = 0; i < 4; i++)
        {
            Tuple<int, int> offset = GridControl.Directions[i];
            var neighbor = new Tuple<int,int>(x + offset.Item1, z + offset.Item2);
            if (neighbor.Item1 >= 0 && neighbor.Item1 < ChunkSize && neighbor.Item2 >= 0 && neighbor.Item2 < ChunkSize)
            {
                columns[neighbor.Item1, neighbor.Item2].NeighborDestroyed(position.y, this);
            }
            else
            {
                // find the column in neighboring chunk
                //neighbors[i].NeighborDestroyed(position.x, position.y, position.z, this);
                // only one coordinate is out of bounds
                if (neighbor.Item1 < 0 || neighbor.Item1 >= ChunkSize)
                {
                    //
                    neighbors[i].columns[neighbor.Item1 >= ChunkSize ? 0 : (ChunkSize - 1), z].NeighborDestroyed(position.y, neighbors[i]);
                }
                else
                {
                    //
                    neighbors[i].columns[x, neighbor.Item2 >= ChunkSize ? 0 : (ChunkSize - 1)].NeighborDestroyed(position.y, neighbors[i]);
                }
            }
        }
        return column.BlockReplaced(position.x, position.y, position.z, this, BlockType.Count);
    }
    public void BlockPlaced(Vector3Int position, BlockType blockType)
    {
        modified = true;
        int x = position.x - this.x * ChunkSize;
        int z = position.z - this.z * ChunkSize;
        // notify the column
        Column column = columns[x, z];
        _ = column.BlockReplaced(position.x, position.y, position.z, this, blockType);
    }

    private int x;
    private int z;

    private Column[,] columns = new Column[ChunkSize, ChunkSize];

    public bool modified
    {
        get;
        private set;
    }

}

// One of the invariants that should be preserved is this: all the chunks around the player are spawned
// this ensures that whenever a block is destroyed or placed we only need to check the targeted chunk and pass information
// to the column and its neighbors
// if chunk was not modified by the player we can optimize by spawning only top layer
// and any blocks that are not covered by other blocks from all directions


// Before the column is modified it should behave differently(and be memory efficient)
// But having two different classes brings new problems so instead I will put both List(unmodified state) and SortedDictionary(modified) in the same class
// Which only wastes one pointer
// If the column was not modified by player we only need to remember few values
// Almost everything can be calculated from scratch because the terrain generation is deterministic
// but caching some information helps and uses very little memory compared to modified columns


/// <summary>
/// Class holding the data about one column
/// Until it is modified only list of layer heights is used
/// After modification the list is converted to SortedDictionary to allow faster lookup and modifications
/// </summary>
public class Column
{
    // instantiate needs some transform as an argument
    private static Transform transform = new GameObject().transform;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="y">Y coordinate of destroyed block</param>
    private void UpdateLayers(int y)
    {

    }

    private void CreateBlock(int y, BlockType blockType, Chunk chunk)
    {
        GameObject gridElement = GameObject.Instantiate(GridControl.GridElementPrefab[(int)blockType], transform);
        gridElement.name = "GridElement_" + x + "," + z;
        gridElement.transform.position = GridControl.grid.GetCellCenterWorld(new Vector3Int(x, y, z));
        blocks.Add(y, gridElement);
        gridElement.transform.parent = chunk.transform;
    }

    public BlockType GetTypeAt(Vector3Int position)
    {
        if (layerHeights == null)
        {
            int i = 0;
            while (layerList[i] < position.y)
            {
                i++;
            }
            return (BlockType)i;
        }

        if (layerHeights != null)
        {
            foreach (var pair in layerHeights)
            {
                if (pair.Key >= position.y)
                {
                    return pair.Value;
                }
            }
        }
        throw new ArgumentException("The block does not exist anymore");
    }

    /// <summary>
    /// When block is replaced in a column this method updates layers and spawns blocks if needed
    /// </summary>
    /// <param name="chunk">parent chunk</param>
    /// <param name="newBlock">Type of the block being put in, this allows using the method for placing blocks as well</param>
    /// <returns>Type of block that was destroyed, empty type if block was placed</returns>
    public BlockType BlockReplaced(int x, int y, int z, Chunk chunk, BlockType newBlock)
    {
        BlockType destroyedBlock = BlockType.Count;
        BlockType blockUnder = BlockType.Count;

        if (newBlock == BlockType.Count)
        {
            blocks.Remove(y);
        }


        if (layerHeights == null) // column was not modified before - change representation
        {
            Reconstruct();
            layerList = null;
        }

        // if we are placing a block the last layer can be much lower than y - 1 causing edge cases
        // adding new layer of air fixes this
        if (newBlock != BlockType.Count && layerHeights.Keys.Max() < y)
        {
            layerHeights.Add(y, BlockType.Count);
        }

        // determine parameters of the layers affected

        // there are two cases: both destroyed block and the one under it are in the same layer or not
        // if they are in the same layer we need to make the layer go one block lower add empty one and continue with the one of destroyed block if needed
        int layerUnder = 0;
        var enumerator = layerHeights.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current.Key >= y - 1) // layer below the destroyed block
            {
                layerUnder = enumerator.Current.Key;
                blockUnder = enumerator.Current.Value;
                break;
            }
        }

        int currentLayer;
        BlockType blockAbove = BlockType.Count;
        if (enumerator.Current.Key >= y) // layer below the destroyed block is same
        {
            currentLayer = enumerator.Current.Key;
            destroyedBlock = enumerator.Current.Value;
        }
        else
        {
            enumerator.MoveNext();
            currentLayer = enumerator.Current.Key;
            destroyedBlock = enumerator.Current.Value;
        }

        if (newBlock == BlockType.Count && enumerator.Current.Key > y) // if block is being destroyed we might need to spawn block above it
        {
            blockAbove = enumerator.Current.Value;
        }
        else
        {
            if (enumerator.MoveNext())
            {
                blockAbove = enumerator.Current.Value;
            }
            // no layer is above the destroyed block
        }
        
        enumerator.Dispose();

        // update the layers
        if (blockUnder == destroyedBlock)
        {
            //the layer ends with destroyed block
            if (currentLayer == y)
            {
                layerHeights[currentLayer] = newBlock; //old layer is replaced with new one
                // shrink the layer by one
                layerHeights.Add(layerUnder - 1, blockUnder);
            }
            else
            {
                // layer ends with block under destroyed block
                layerHeights[y] = newBlock;
                layerHeights.Add(y - 1, blockUnder);
            }
        }
        else
        {
            layerHeights[y] = newBlock;
        }
        
        // Spawn the new blocks if needed
        if (blockUnder != BlockType.Count && newBlock == BlockType.Count && !blocks.ContainsKey(y - 1) ) // block under this one was not spawned and were destroying
        {
            CreateBlock(y - 1, blockUnder, chunk);
        }
        if (newBlock != BlockType.Count) // placing block
        {
            CreateBlock(y, newBlock, chunk);
        }
        if (newBlock == BlockType.Count && blockAbove != BlockType.Count && !blocks.ContainsKey(y + 1) )
        {
            CreateBlock(y + 1, blockAbove, chunk);
        }

        // merge neighboring layers of same type
        List<int> keysToRemove = new List<int>();
        BlockType previousType = BlockType.Count;
        enumerator = layerHeights.GetEnumerator();
        enumerator.MoveNext();
        previousType = enumerator.Current.Value;
        int previousKey = enumerator.Current.Key;
        while (enumerator.MoveNext())
        {
            if (enumerator.Current.Value == previousType)
            {
                keysToRemove.Add(previousKey);
            }
            previousType = enumerator.Current.Value;
            previousKey = enumerator.Current.Key;
        }
        enumerator.Dispose();
        foreach (var key in keysToRemove)
        {
            layerHeights.Remove(key);
        }
        return destroyedBlock;
    }

    /// <summary>
    /// Convert data to modified column and return it
    /// </summary>
    public void Reconstruct()
    {
        //ModifiedColumn column = new ModifiedColumn(x, z);
        layerHeights = new SortedDictionary<int, BlockType>();
        for (int i = 0; i < layerList.Count; i++)
        {
            // Since there are as many layers as types of blocks we can cast index to BlockType to get the type
            layerHeights.Add(layerList[i], (BlockType)i);
        }
    }
    public void BlockPlaced(int x, int z, int y, Chunk chunk)
    {
        
    }

    public void NeighborDestroyed(int y, Chunk chunk)
    {
        if (layerHeights == null && Chunk.PerlinNoise(x,z) >= y && !blocks.ContainsKey(y))
        {
            CreateBlock(y, Chunk.GetBlockType(x, y, z), chunk);
        }

        if (layerHeights != null && !blocks.ContainsKey(y))
        {
            foreach (var pair in layerHeights)
            {
                if (pair.Key >= y)
                {
                    if (pair.Value == BlockType.Count)
                    {
                        break;
                    }
                    CreateBlock(y, pair.Value, chunk);
                    break;
                }
            }
        }
    }

    public Column(int x, int z, Chunk chunk)
    {
        this.x = x;
        this.z = z;
        int minNeigbor = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Tuple<int, int> offset = GridControl.Directions[i];
            minNeigbor = Math.Min(Chunk.PerlinNoise(x + offset.Item1, z + offset.Item2), minNeigbor);
        }
        // top block of new chunk can always be seen
        height = Chunk.PerlinNoise(x, z);
        if (height <= 10)
        {
            layerList = new List<int>() { height };
        }
        else
        {
            if (height <= 19)
            {
                layerList = new List<int>() { 10, height};
            }
            else
            {
                layerList = new List<int>() { 10, 19, height };
            }
        }
        
        
        CreateBlock(height, Chunk.GetBlockType(x,height,z), chunk);
        // top block must always be spawned, others depend on neighbors
        for (int i = height - 1; i > minNeigbor; i--)
        {
            CreateBlock(i, Chunk.GetBlockType(x, i, z), chunk);
        }
    }

    public int x;
    public int z;
    public int height;
    // having entry for every spawned cube is not ideal however spawning the Game Object is bigger compared to one entry
    private SortedDictionary<int, GameObject> blocks = new SortedDictionary<int, GameObject>();

    private SortedDictionary<int, BlockType> layerHeights;
    public List<int> layerList = new List<int>((int)BlockType.Count);


}
