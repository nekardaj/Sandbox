using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using NoiseLibrary;
using TerrainGenerationPCG;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public enum Direction
{
    North,
    East,
    West,
    South
}

/// <summary>
/// The input for the job is just chunk coordinates
/// </summary>
public struct PrecomputeData
{
    public int x;
    public int z;
}

public struct PrecomputeJob : IJobParallelFor
{
    // for every chunk we have ChunkSize^2 entries so we need to ignore index restrictions
    // every batch of ChunkSize^2 entries is for one chunk so there is no need for synchronization
    [NativeDisableParallelForRestriction]
    public NativeArray<int> heightsData;
    [NativeDisableParallelForRestriction]
    public NativeArray<BiomeType> biomesData;
    
    public NativeArray<Vector2Int> chunks;

    public void Execute(int index)
    {
        Vector2Int position = chunks[index];
        for (int i = 0; i < Chunk.ChunkSize; i++)
        {
            for (int j = 0; j < Chunk.ChunkSize; j++)
            {
                var result = Chunk.MapGenerator.GetFilteredBiomeAndHeight(position.x * Chunk.ChunkSize + i, position.y * Chunk.ChunkSize + j);
                // for every chunk we have ChunkSize^2 entries and inside the chunk we use flat indexing
                int flatIndex = i + Chunk.ChunkSize * j + index * Chunk.ChunkSize * Chunk.ChunkSize;
                biomesData[flatIndex] = result.Item1;
                heightsData[flatIndex] = result.Item2;
            }
        }
    }

    
}

/// <summary>
/// Chunk is a game object so the whole chunk can be disabled when player is far away
/// </summary>
public class Chunk : MonoBehaviour
{
    #region static variables
    public static readonly int ChunkSize = 16;
    public static readonly int ChunkHeight = 256;
    private static readonly int BaseHeight = 96;

    private static int SeedX;
    private static int SeedZ;

    private static readonly float LayerHeightDisplacementAmplitude = 2.5f;
    private static readonly int BaseLayerThickness = 5;

    public static MapGenerator MapGenerator = new MapGenerator();
    #endregion

    public int x
    {
        get;
        private set;
    }
    public int z
    {
        get;
        private set;
    }

    private Column[,] columns = new Column[ChunkSize, ChunkSize];

    public int[,] heights; // caching the heights of the columns
    public BiomeType[,] biomes; // caching the biomes of the columns
    // another optimization might be to disable chunks that are not visible to the player
    // eg using heuristic using lowest and highest block in the chunk
    public Chunk[] neighbors = new Chunk[4]; // 4 neighbors

    private static FastNoiseLite noise = new FastNoiseLite();

    static Chunk()
    {
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetSeed(System.DateTime.Now.Millisecond ^ 0b101001);
        noise.SetFrequency(0.05f);
    }

    public bool modified
    {
        get;
        private set;
    }

    // these values can be used for optimization
    public int minimalHeight;
    public int maximalHeight;

    public static void InitializeSeed()
    {
        SeedX = UnityEngine.Random.Range(0, 1 << 20);
        SeedZ = UnityEngine.Random.Range(0, 1 << 20);
        Debug.Log("seed:" + SeedX + " " + SeedZ);
    }

    public struct PrecomputeData
    {
        private const int Size = 16;
        public NativeArray<int> heightsData;
        public NativeArray<BiomeType> biomesData;
        public int x;
        public int z;

        public PrecomputeData(int x, int z)
        {
            this.x = x;
            this.z = z;
            heightsData = new NativeArray<int>(ChunkSize * ChunkSize, Allocator.TempJob);
            biomesData = new NativeArray<BiomeType>(ChunkSize * ChunkSize, Allocator.TempJob);
        }
    }
    /// <summary>
    /// When caching cannot be used this method is used to compute the height of the column
    /// Does not use caching, the column is assumed to be in a different chunk which might not exist yet
    /// </summary>
    /// <param name="x">X coordinate of a column(not chunk)</param>
    /// <param name="z">Z coordinate of a column(not chunk)</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Tuple<BiomeType, int> ComputeHeight(int x, int z)
    {
        return MapGenerator.GetFilteredBiomeAndHeight(x, z);
    }

    public SortedDictionary<int,BlockType> ConstructLayers(int x, int z)
    {
        int localX = (x - ChunkSize * this.x);
        int localZ = (z - ChunkSize * this.z);
        BiomeType biome = biomes[localX, localZ];
        int height = heights[localX, localZ];
        SortedDictionary<int, BlockType> layerHeights = new SortedDictionary<int, BlockType>();
        // add the top layer using the biome for the type of the block
        layerHeights.Add(height, (BlockType)biome); // cursed cast but we made sure that the values match, I dont want to add myriad of block types anyway
        // add the rest of the layers
        int layerThicknessDisplacement = (int) ((1 + noise.GetNoise(x,z)) / 2 * LayerHeightDisplacementAmplitude);
        layerHeights.Add(height - BaseLayerThickness + layerThicknessDisplacement, BlockType.Rock);

        return layerHeights;
    }
    public void RegisterNeighbor(Chunk chunk, Direction direction)
    {
        neighbors[(int)direction] = chunk;
    }

    public void PrecomputeHeights(int x, int z)
    {
        heights = new int[ChunkSize, ChunkSize];
        biomes = new BiomeType[ChunkSize, ChunkSize];
        for (int i = 0; i < ChunkSize; i++)
        {
            for (int j = 0; j < ChunkSize; j++)
            {
                MapGenerator.GetFilteredBiomeAndHeight(x * ChunkSize + i, z * ChunkSize + j).Deconstruct(out biomes[i, j], out heights[i, j]);
            }
        }
    }
    public void Initialize(int x, int z, int[,] precomputedHeights, BiomeType[,] precomputedBiomes)
    {
        heights = precomputedHeights;
        biomes = precomputedBiomes;
        this.x = x;
        this.z = z;
        modified = false;
        columns = new Column[ChunkSize, ChunkSize];
        for (int i = 0; i < ChunkSize; i++)
        {
            for (int j = 0; j < ChunkSize; j++)
            {
                columns[i, j] = new Column(x * ChunkSize + i, z * ChunkSize + j, this);
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
}

// One of the invariants that should be preserved is this: all the chunks around the player are spawned
// this ensures that whenever a block is destroyed or placed we only need to check the targeted chunk and pass information
// to the column and its neighbors
// if chunk was not modified by the player we can optimize by spawning only top layer
// and any blocks that are not covered by other blocks from all directions

// because of added complexity to the terrain generation using layer list does not make sense anymore, SortedDictionary is used the whole time now


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
        foreach (var pair in layerHeights)
        {
            if (pair.Key >= position.y)
            {
                return pair.Value;
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


    public void NeighborDestroyed(int y, Chunk chunk)
    {
        if (!blocks.ContainsKey(y))
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
            int localX = (x + offset.Item1 - Chunk.ChunkSize * chunk.x);
            int localZ = (z + offset.Item2 - Chunk.ChunkSize * chunk.z);
            // this will likely affect performance noticeably when the player crosses the border of the chunk
            if (localX < 0 || localX >= Chunk.ChunkSize || localZ < 0 || localZ >= Chunk.ChunkSize)
            {
                minNeigbor = Math.Min(chunk.ComputeHeight(x + offset.Item1, z + offset.Item2).Item2, minNeigbor);
            }
            else
            {
                minNeigbor = Math.Min(chunk.heights[localX, localZ], minNeigbor);
            }
            
        }
        // top block of new chunk can always be seen
        
        layerHeights = chunk.ConstructLayers(x, z);
        // last entry is the top block
        var lastLayer = layerHeights.Last();
        height = lastLayer.Key;
        CreateBlock(height, lastLayer.Value, chunk);
        // top block must always be spawned, others depend on neighbors
        if (height <= minNeigbor) // no neighbors are lower so only top block is visible
        {
            return;
        }
        // find the layer of the lowest neighbor
        var enumerator = layerHeights.GetEnumerator();
        enumerator.MoveNext();
        while (enumerator.Current.Key < minNeigbor)
        {
            enumerator.MoveNext();
        } // find the layer of the lowest neighbor or until the end
        // there is at least one lower neighbour so we dont need to check enumerator state
        for (int i = minNeigbor; i <= height - 1; i++)
        {
            CreateBlock(i, enumerator.Current.Value, chunk);
            // last block of a layer was spawned so we need to move to the next one
            if (i == enumerator.Current.Key)
            {
                enumerator.MoveNext();
            }
        }
    }

    public int x;
    public int z;
    public int height;
    // having entry for every spawned cube is not ideal however spawning the Game Object is bigger compared to one entry
    private SortedDictionary<int, GameObject> blocks = new SortedDictionary<int, GameObject>();

    private SortedDictionary<int, BlockType> layerHeights;
}
