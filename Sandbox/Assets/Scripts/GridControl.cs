using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BlockType { Rock, Grass, Snow }




/// <summary>
/// columns should have two types - modified by player and unmodified
/// latter one can be stored using 3 numbers
/// adding reference to its neighbors will allow every column to control spawning of its blocks only when they can be seen by player
/// ie they are not covered by other blocks from all directions
/// chunks too far from player should be disabled
/// </summary>


public class GridControl : MonoBehaviour
{
    public GameObject GridElementPrefab;

    [SerializeField]
    Vector3Int gridSize;
    private Grid grid;

    /// <summary>
    /// We need to create chunks dynamically as the player walks near them
    /// The coordinates of chunks that need to be active are arbitrary(based on player movement)
    /// So to avoid wasting memory we will create a list of chunks that are active
    /// We will often need to check if a chunk is active or not
    /// </summary>
    private List<BlockType> chunks;

    // Start is called before the first frame update
    void Start()
    {
        grid = GetComponent<Grid>();
        for (int i = 0; i < gridSize.x; i++)
        {
            for (int j = 0; j < gridSize.z; j++)
            {
                GameObject gridElement = Instantiate(GridElementPrefab, transform);
                gridElement.transform.position = grid.GetCellCenterWorld(new Vector3Int(i, 0, j));
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
