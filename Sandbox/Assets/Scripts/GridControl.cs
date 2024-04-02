using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TerrainGenerationPCG;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using static UnityEngine.InputManagerEntry;

// Blocks will be in the same order as in biome type so we can use either for indexing
public enum BlockType {
    TundraDefault,
    TaigaDefault,
    TemperateGrasslandDefault,
    TermperateForestDefault,
    TropicalSeasonalForestDefault,
    DesertDefault,
    SavannaDefault,
    TropicalRainforestDefault,
    Rock, // Mountain
    DeepWater,
    ShallowWater,
    //Snow,
    //Grass,
    Count
}
// trick that allows using block type as an index in an array, even when new types are inserted before Count everything will work
// layer of type Count means empty space



/// <summary>
/// columns should have two types - modified by player and unmodified
/// latter one can be stored using 3 numbers
/// adding reference to its neighbors will allow every column to control spawning of its blocks only when they can be seen by player
/// ie they are not covered by other blocks from all directions
/// chunks too far from player should be disabled
/// </summary>


public class GridControl : MonoBehaviour
{
    // when the player changes the chunk they are in 2n+1 chunks should disabled and 2n+1 enabled
    
    // chunks and columns need access to this so it should be static(accessible for everyone)
    public static GameObject[] GridElementPrefab;
    [SerializeField] private GameObject[] GridElementPrefab_;

    [SerializeField] private Vector3Int gridSize;

    [SerializeField] private Player player;

    // bedrock should be infinite layer so we move big object under player
    [SerializeField] private GameObject bedrock;

    public static readonly int terrainLayer = 8;

    private Vector3Int lastChunk;

    public static Grid grid;

    // chunks that are too far from player will be disabled
    //public static readonly int RenderDistance = 5;
    [Range(1, 32),SerializeField] private int RenderDistance = 3;

    public static readonly Tuple<int, int>[] Directions = new Tuple<int, int>[]
    {
        // Same order as in Direction enum - NEWS, in this order 3 - index gets opposite one
        new Tuple<int, int>(0,1),
        new Tuple<int, int>(1,0),
        new Tuple<int, int>(-1,0),
        new Tuple<int, int>(0,-1)
    };

    public static readonly float[] MiningTimes = new float[(int)BlockType.Count] // Count ensures every used block has its entry
    {
        1.0f,
        1.0f,
        1.0f,
        1.0f,
        1.0f,
        1.0f,
        1.0f,
        1.75f,
        1.0f,
        1.0f,
        1.0f,
    };

    public BlockType GetTypeAt(Vector3 position)
    {
        var gridPosition = grid.WorldToCell(position);
        var chunkCoords = CellToChunk(gridPosition);
        var chunk = chunkSortedDictionary[new Tuple<int, int>(chunkCoords.x, chunkCoords.z)];
        return chunk.GetTypeAt(gridPosition);
    }

    // We need to create chunks dynamically as the player walks near them
    // The coordinates of chunks that need to be active are arbitrary(based on player movement)
    // So to avoid wasting memory we will create a list of chunks that are active
    // We will often need to check if a chunk is active or not

    /// <summary>
    /// Compares tuples based on their distance from the origin
    /// </summary>
    class TupleComparer : IComparer<Tuple<int, int>>
    {
        public int Compare(Tuple<int, int> first, Tuple<int, int> second)
        {
            if (first == null || second == null)
            {
                return 0;
            }
            // use absolute value
            var x = new Tuple<int, int>(Math.Abs(first.Item1), Math.Abs(first.Item2));
            var y = new Tuple<int, int>(Math.Abs(second.Item1), Math.Abs(second.Item2));
            // chunks are sorted by their distance from the origin
            if (x.Item1 + x.Item2 < y.Item1 + y.Item2)
            {
                return -1;
            }
            // if the distance is the same we sort by x coordinate if same by y
            if (x.Item1 + x.Item2 > y.Item1 + y.Item2)
            {
                return 1;
            }

            if (first.Item1 - second.Item1 != 0)
            {
                return first.Item1 - second.Item1;
            }
            return first.Item2 - second.Item2;
        }
    }

    private Chunk CreateChunk(int x, int z)
    {
        GameObject object_ = new GameObject();
        object_.layer = terrainLayer;
        object_.name = "Chunk_" + x + "," + z;
        Chunk chunk_ = object_.AddComponent<Chunk>();
        chunkSortedDictionary.Add(new Tuple<int, int>(x, z), chunk_);
        // New chunk was created, notify all neighbors that already exist
        for (int i = 0; i < 4; i++)
        {
            Tuple<int, int> offset = GridControl.Directions[i];
            if (chunkSortedDictionary.TryGetValue(new Tuple<int, int>(x + offset.Item1, z + offset.Item2), out Chunk neighbor))
            {
                // the direction is antisymetric: 3 - i swaps NS and EW because if we went north this chunk on south of its neighbor 
                neighbor.RegisterNeighbor(chunk_, (Direction)3 - i);
                chunk_.RegisterNeighbor(neighbor, (Direction)i);
            }
        }
        return chunk_;
    }

    /// <summary>
    /// Attempt at utilizing unity job system for parallelizing chunk creation
    /// </summary>
    /// <param name="position"></param>
    public void UpdatePositionParallel(Vector3 position)
    {
        Vector3Int chunk = WorldToChunk(position);
        // move bedrock under player
        bedrock.transform.position = new Vector3(grid.WorldToCell(position).x, bedrock.transform.position.y, grid.WorldToCell(position).z);
        // disable chunk that are now too far from player and enable or instantiate new ones that are close
        List<Tuple<int,int>> chunksToPreprocess = new List<Tuple<int,int>>();
        


        if (chunk.x != lastChunk.x || chunk.z != lastChunk.z)
        {
            // disable chunks that are too far from player and enable new ones
            // this assumes player can only move one chunk at a time but since xz speed is capped it should be ok
            int offset = RenderDistance;
            if (lastChunk.x != chunk.x)
            {
                offset = lastChunk.x < chunk.x ? RenderDistance : -RenderDistance;
                for (int i = lastChunk.z - RenderDistance; i <= lastChunk.z + RenderDistance; i++)
                {
                    if (chunkSortedDictionary.ContainsKey(new Tuple<int, int>(chunk.x + offset, i)))
                    {
                        chunkSortedDictionary[new Tuple<int, int>(chunk.x + offset, i)].gameObject.SetActive(true);
                    }
                    else
                    {
                        chunksToPreprocess.Add(new Tuple<int, int>(chunk.x + offset, i));
                    }
                    // disable chunk, there must be entry in dictionary since we were there and it must have been spawned
                    var key = new Tuple<int, int>(lastChunk.x - offset, i);
                    if (!chunkSortedDictionary.ContainsKey(key))
                    {
                        Debug.Log("Missing chunk " + key);
                    }
                    else
                    {
                        chunkSortedDictionary[key].gameObject.SetActive(false);
                    }
                    //CreateChunk(chunk.x + offset, i);
                }
            }

            if (lastChunk.z != chunk.z)
            {
                offset = lastChunk.z < chunk.z ? RenderDistance : -RenderDistance;
                for (int i = lastChunk.x - RenderDistance; i <= lastChunk.x + RenderDistance; i++)
                {
                    if (chunkSortedDictionary.ContainsKey(new Tuple<int, int>(i, chunk.z + offset)))
                    {
                        chunkSortedDictionary[new Tuple<int, int>(i, chunk.z + offset)].gameObject.SetActive(true);
                    }
                    else
                    {
                        chunksToPreprocess.Add(new Tuple<int, int>(i, chunk.z + offset));
                    }
                    var key = new Tuple<int, int>(i, lastChunk.z - offset);
                    if (!chunkSortedDictionary.ContainsKey(key))
                    {
                        Debug.Log("Missing chunk " + key);
                    }
                    else
                    {
                        chunkSortedDictionary[key].gameObject.SetActive(false);
                    }
                    //CreateChunk(i, chunk.z + offset);
                }
            }

            // Preprocess all chunks that need to be created
            PrecomputeJob job = new PrecomputeJob();
            // chunks to preprocess cannot be native array since size is not known at the time of creation
            var preprocessData = new NativeArray<Vector2Int>(chunksToPreprocess.Count, Allocator.TempJob);
            for (int i = 0; i < chunksToPreprocess.Count; i++)
            {
                preprocessData[i] = new Vector2Int(chunksToPreprocess[i].Item1, chunksToPreprocess[i].Item2);
            }
            job.chunks = preprocessData;
            job.heightsData = new NativeArray<int>(Chunk.ChunkSize * Chunk.ChunkSize * chunksToPreprocess.Count, Allocator.TempJob);
            job.biomesData = new NativeArray<BiomeType>(Chunk.ChunkSize * Chunk.ChunkSize * chunksToPreprocess.Count, Allocator.TempJob);
            var jobHandle = job.Schedule(chunksToPreprocess.Count, 1);
            Chunk[] chunks = new Chunk[chunksToPreprocess.Count];
            // instantiate GOs while waiting for the job to complete
            for (int i = 0; i < chunksToPreprocess.Count; i++)
            {
                chunks[i] = CreateChunk(chunksToPreprocess[i].Item1, chunksToPreprocess[i].Item2);
            }
            jobHandle.Complete();
            for (int i = 0; i < preprocessData.Length; i++)
            {
                // cast native arrays to managed arrays
                int[,] heights = new int[Chunk.ChunkSize, Chunk.ChunkSize];
                BiomeType[,] biomes = new BiomeType[Chunk.ChunkSize, Chunk.ChunkSize];
                for (int j = 0; j < Chunk.ChunkSize; j++)
                {
                    for (int k = 0; k < Chunk.ChunkSize; k++)
                    {
                        int flatIndex = j + Chunk.ChunkSize * k + i * Chunk.ChunkSize * Chunk.ChunkSize;
                        heights[j, k] = job.heightsData[flatIndex];
                        biomes[j, k] = job.biomesData[flatIndex];
                    }
                }

                chunks[i].Initialize(preprocessData[i].x, preprocessData[i].y, heights, biomes);
            }
            // dispose of native arrays
            preprocessData.Dispose();
            job.heightsData.Dispose();
            job.biomesData.Dispose();

            lastChunk = chunk;

        }
    }

    // chunks that are modified dont need to be stored when the player walks too far from them
    // sorted list and sorted dictionary are similar
    private SortedDictionary<Tuple<int, int>, Chunk> chunkSortedDictionary = new SortedDictionary<Tuple<int, int>, Chunk>(new TupleComparer());
    // Start is called before the first frame update
    void Awake()
    {
        grid = new Grid(); // default constructor creates a grid with cell size 1 as desired
        GridElementPrefab = GridElementPrefab_;
        // At the start spawn all chunks that are within render distance
        grid = GetComponent<Grid>();
        lastChunk = WorldToChunk(player.transform.position);

        int length = (2 * RenderDistance + 1);
        // number of chunks to be preprocessed
        int chunksCount = length * length;

        var preprocessData = new NativeArray<Vector2Int>(chunksCount, Allocator.TempJob);
        var job = new PrecomputeJob();
        job.chunks = preprocessData;
        job.heightsData = new NativeArray<int>(Chunk.ChunkSize * Chunk.ChunkSize * chunksCount, Allocator.TempJob);
        job.biomesData = new NativeArray<BiomeType>(Chunk.ChunkSize * Chunk.ChunkSize * chunksCount, Allocator.TempJob);

        for (int i = lastChunk.x - RenderDistance; i <= lastChunk.x + RenderDistance; i++)
        {
            for (int j = lastChunk.z - RenderDistance; j <= lastChunk.z + RenderDistance; j++)
            {
                preprocessData[(i - (lastChunk.x - RenderDistance)) + (j - (lastChunk.z - RenderDistance)) * length] = new Vector2Int(i,j);
            }
        }

        var jobHandle = job.Schedule(chunksCount, 1);
        Chunk[] chunks = new Chunk[chunksCount];

        // instantiate GOs while waiting for the job to complete
        for (int i = lastChunk.x - RenderDistance; i <= lastChunk.x + RenderDistance; i++)
        {
            for (int j = lastChunk.z - RenderDistance; j <= lastChunk.z + RenderDistance; j++)
            {
                chunks[(i - (lastChunk.x - RenderDistance)) + (j - (lastChunk.z - RenderDistance)) * length] = CreateChunk(i, j);
            }
        }

        jobHandle.Complete();
        for (int i = lastChunk.x - RenderDistance; i <= lastChunk.x + RenderDistance; i++)
        {
            for (int j = lastChunk.z - RenderDistance; j <= lastChunk.z + RenderDistance; j++)
            {

                int[,] heights = new int[Chunk.ChunkSize, Chunk.ChunkSize];
                BiomeType[,] biomes = new BiomeType[Chunk.ChunkSize, Chunk.ChunkSize];
                for (int k = 0; k < Chunk.ChunkSize; k++)
                {
                    for (int l = 0; l < Chunk.ChunkSize; l++)
                    {
                        int flatIndex = k + Chunk.ChunkSize * l + ((i - (lastChunk.x - RenderDistance)) + (j - (lastChunk.z - RenderDistance)) * length) * Chunk.ChunkSize * Chunk.ChunkSize;
                        heights[k, l] = job.heightsData[flatIndex];
                        biomes[k, l] = job.biomesData[flatIndex];
                    }
                }
                chunks[(i - (lastChunk.x - RenderDistance)) + (j - (lastChunk.z - RenderDistance)) * length].Initialize(i, j, heights, biomes);
            }
        }
        // dispose of native arrays
        preprocessData.Dispose();
        job.heightsData.Dispose();
        job.biomesData.Dispose();
    }

    /// <summary>
    /// Gets sign bit of a number
    /// </summary>
    /// <param name="x"></param>
    /// <returns>0 for nonegative numbers -1 for negative</returns>
    // the method is so short that it should get always inlined
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static int GetSignBit(int x)
    {
        return x >> 31;
    }

    /// <summary>
    /// Because integer division rounds towards zero we need to apply offset to negative numbers
    /// I am keeping the value as vector 3 to avoid confusion of axis
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public static Vector3Int CellToChunk(Vector3Int gridPosition)
    {
        // shift to the right by 31 bits copies the sign bit to all bits - 0 for positive numbers and -1 for negative
        // this is definitely unnecessary optimization, but I like it
        return new Vector3Int((gridPosition.x + (Chunk.ChunkSize - 1) * (GetSignBit(gridPosition.x))) / Chunk.ChunkSize, gridPosition.y / Chunk.ChunkSize ,(gridPosition.z + (Chunk.ChunkSize - 1) * GetSignBit(gridPosition.z)) / Chunk.ChunkSize);
    }
    public static Vector3Int WorldToChunk(Vector3 position)
    {
        Vector3Int gridPosition = grid.WorldToCell(position);
        return new Vector3Int((gridPosition.x + (Chunk.ChunkSize - 1) * GetSignBit(gridPosition.x)) / Chunk.ChunkSize, gridPosition.y / Chunk.ChunkSize, (gridPosition.z + (Chunk.ChunkSize - 1) * GetSignBit(gridPosition.z)) / Chunk.ChunkSize);
    }

    public void AddBlock(Vector3 position, BlockType blockType)
    {
        var gridPosition = grid.WorldToCell(position);
        var chunkCoords = CellToChunk(gridPosition);
        var chunk = chunkSortedDictionary[new Tuple<int, int>(chunkCoords.x, chunkCoords.z)];
        chunk.BlockPlaced(gridPosition, blockType);
    }

    public BlockType BlockDestroyed(Vector3 position)
    {
        var gridPosition = grid.WorldToCell(position);
        var chunkCoords = CellToChunk(gridPosition);
        var chunk = chunkSortedDictionary[new Tuple<int, int>(chunkCoords.x, chunkCoords.z)];
        return chunk.BlockDestroyed(gridPosition);
    }
    // Update is called once per frame
    void Update()
    {
        var position = grid.WorldToCell(player.transform.position);
    }
}
