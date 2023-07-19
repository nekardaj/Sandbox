using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum BlockType { Rock, Grass, Snow, Count }
// trick that allows using block type as an index in an array, even when new types are inserted before Count everything will work




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

    public static readonly int terrainLayer = 8;

    private Vector3Int lastChunk;

    private int gridHeight = 4;
    

    public static Grid grid;

    // chunks that are too far from player will be disabled
    public static readonly int RenderDistance = 1;


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
    /// <summary>
    /// Every time the player moves we need to update the chunks which are active
    /// </summary>
    /// <param name="position"></param>
    public void UpdatePosition(Vector3 position)
    {
        Vector3Int chunk = WorldToChunk(position);
        // disable chunk that are now too far from player and enable or instantiate new ones that are close
        void ActivateChunk(int x, int z)
        {
            if (chunkSortedDictionary.ContainsKey(new Tuple<int, int>(x, z)))
            {
                chunkSortedDictionary[new Tuple<int, int>(x, z)].gameObject.SetActive(true);
            }
            else
            {
                GameObject object_ = new GameObject();
                object_.layer = terrainLayer;
                object_.name = "Chunk_" + x + "," + z;
                Chunk chunk_ = object_.AddComponent<Chunk>();
                chunk_.Initialize(x, z);
                chunkSortedDictionary.Add(new Tuple<int, int>(x, z), chunk_);
            }
        }

        if (chunk.x != lastChunk.x || chunk.z != lastChunk.z)
        {
            // disable chunks that are too far from player and enable new ones
            // this assumes player can only move one chunk at a time but since xz speed is capped it should be ok
            if (lastChunk.x < chunk.x)
            {
                for (int i = lastChunk.z - RenderDistance; i <= lastChunk.z + RenderDistance; i++)
                {
                    // disable chunk, there must be entry in dictionary since we were there and it must have been spawned
                    chunkSortedDictionary[new Tuple<int, int>(lastChunk.x - RenderDistance, i)].gameObject.SetActive(false);
                    ActivateChunk(chunk.x + RenderDistance, i);
                }
            }
            if (lastChunk.x > chunk.x)
            {
                for (int i = lastChunk.z - RenderDistance; i <= lastChunk.z + RenderDistance; i++)
                {
                    chunkSortedDictionary[new Tuple<int, int>(lastChunk.x + RenderDistance, i)].gameObject.SetActive(false);
                    ActivateChunk(chunk.x - RenderDistance, i);
                }
            }
            if (lastChunk.z < chunk.z)
            {
                for (int i = lastChunk.x - RenderDistance; i <= lastChunk.x + RenderDistance; i++)
                {
                    chunkSortedDictionary[new Tuple<int, int>(i, lastChunk.z - RenderDistance)].gameObject.SetActive(false);
                    ActivateChunk(i, chunk.z + RenderDistance);
                }
            }
            if (lastChunk.z > chunk.z)
            {
                for (int i = lastChunk.x - RenderDistance; i <= lastChunk.x + RenderDistance; i++)
                {
                    chunkSortedDictionary[new Tuple<int, int>(i, lastChunk.z + RenderDistance)].gameObject.SetActive(false);
                    ActivateChunk(i, chunk.z - RenderDistance);
                }
            }
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
        var position = grid.WorldToCell(player.transform.position / Chunk.ChunkSize);
        for (int i = position.x - RenderDistance; i <= position.x + RenderDistance; i++)
        {
            for (int j = position.z - RenderDistance; j <= position.z + RenderDistance; j++)
            {
                GameObject object_ = new GameObject();
                object_.layer = terrainLayer;
                object_.name = "Chunk_" + i + "," + j;
                Chunk chunk = object_.AddComponent<Chunk>();
                chunk.Initialize(i,j);
                chunkSortedDictionary.Add(new Tuple<int, int>(i,j), chunk);
            }
        }
    }

    /// <summary>
    /// Because integer division rounds towards zero we need to apply offset to negative numbers
    /// I am keeping the value as vector 3 to avoid confusion of axis
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public static Vector3Int CellToChunk(Vector3Int gridPosition)
    {
        return new Vector3Int((gridPosition.x - (gridPosition.x < 0 ? Chunk.ChunkSize - 1 : 0)) / Chunk.ChunkSize, gridPosition.y / Chunk.ChunkSize ,(gridPosition.z - (gridPosition.z < 0 ? Chunk.ChunkSize - 1 : 0)) / Chunk.ChunkSize);
    }
    public static Vector3Int WorldToChunk(Vector3 position)
    {
        Vector3Int gridPosition = grid.WorldToCell(position);
        return new Vector3Int((gridPosition.x - (gridPosition.x < 0 ? Chunk.ChunkSize - 1 : 0)) / Chunk.ChunkSize, gridPosition.y / Chunk.ChunkSize, (gridPosition.z - (gridPosition.z < 0 ? Chunk.ChunkSize - 1 : 0)) / Chunk.ChunkSize);
    }

    public void BlockDestroyed(Vector3 position)
    {
        var gridPosition = grid.WorldToCell(position);
        var chunkCoords = CellToChunk(gridPosition);
        var chunk = chunkSortedDictionary[new Tuple<int, int>(chunkCoords.x, chunkCoords.z)];
        chunk.BlockDestroyed(gridPosition);
        /*
        Vector3Int gridPosition = grid.WorldToCell(position);
        // get the chunk that contains the block
        // division rounding goes towards zero so negative numbers need offset
        var chunk = chunkSortedDictionary[new Tuple<int, int>((gridPosition.x - (gridPosition.x < 0 ? Chunk.ChunkSize : 0)) / Chunk.ChunkSize, (gridPosition.z - (gridPosition.z < 0 ? Chunk.ChunkSize : 0)) / Chunk.ChunkSize)];
        chunk.BlockDestroyed(gridPosition);
        */
    }
    // Update is called once per frame
    void Update()
    {
        var position = grid.WorldToCell(player.transform.position);
    }
}
