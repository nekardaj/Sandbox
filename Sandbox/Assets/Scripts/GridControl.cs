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
    

    private int gridHeight = 4;
    

    public static Grid grid;

    // chunks that are too far from player will be disabled
    public static readonly int RenderDistance = 2;


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

    // chunks that are modified dont need to be stored when the player walks too far from them
    // sorted list and sorted dictionary are similar
    private SortedDictionary<Tuple<int, int>, Chunk> chunkSortedDictionary = new SortedDictionary<Tuple<int, int>, Chunk>(new TupleComparer());
    // Start is called before the first frame update
    void Start()
    {
        grid = new Grid(); // default constructor creates a grid with cell size 1 as desired
        GridElementPrefab = GridElementPrefab_;
        // At the start spawn all chunks that are within render distance
        grid = GetComponent<Grid>();
        var position = grid.WorldToCell(player.transform.position / Chunk.ChunkSize);
        for (int i = position.x - RenderDistance; i <= position.x + RenderDistance; i++)
        {
            for (int j = position.y - RenderDistance; j < position.y + RenderDistance; j++)
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

    public void BlockDestroyed(Vector3 position)
    {
        Vector3Int gridPosition = grid.WorldToCell(position);
        // get the chunk that contains the block
        // division rounding goes towards zero so negaative numbers need offset
        var chunk = chunkSortedDictionary[new Tuple<int, int>((gridPosition.x - (gridPosition.x < 0 ? Chunk.ChunkSize : 0)) / Chunk.ChunkSize, (gridPosition.z - (gridPosition.z < 0 ? Chunk.ChunkSize : 0)) / Chunk.ChunkSize)];
        chunk.BlockDestroyed(gridPosition);
    }
    // Update is called once per frame
    void Update()
    {
        var position = grid.WorldToCell(player.transform.position);
    }
}
