using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DebugInfo : MonoBehaviour
{
    [SerializeField] Player player;
    TextMeshProUGUI text;
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        string inventory = "";
        for (int i = 0; i < (int)BlockType.Count; i++)
        {
            inventory += (BlockType)i + ": " + player.blocks[i] + "\n";
        }
        text.text = "Position: " + player.transform.position
                                 + "\nChunk: " + GridControl.WorldToChunk((Vector3)player.transform.position)
                                 + "\nBlocks:" + inventory;
    }
}
