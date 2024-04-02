using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cache = Unity.VisualScripting.Cache;

public class Player : MonoBehaviour
{
    [SerializeField] private float speed;
    [Tooltip("Affects how fast can player reach maximal speed")]
    [SerializeField] private float acceleration;
    [Tooltip("Cursor movement")]
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float jumpPower;
    [Tooltip("When space is pressed before touching ground, jump is done anyway if the player landed soon enough")]
    [SerializeField] private float jumpDelay;
    [SerializeField] private float gravityScale;

    //[SerializeField] private float dampingRunning;
    [Tooltip("When player is not pressing any move button movement is slowed using this value. Must be less than one and at least zero")]
    [SerializeField] private float dampingStopping;
    //[SerializeField] private float dampingJumping;

    [SerializeField] private GridControl grid;

    [SerializeField] private Camera camera_;

    // there is a collider in player layer that is used to check if the player is on the ground which should be ignored by raycasting
    [SerializeField] private LayerMask raycastingMask;

    [HideInInspector] public List<uint> blocks = new List<uint>((int)BlockType.Count);

    [SerializeField] private GameObject menu;

    [SerializeField] private Image selectedBlock;
    private TextMeshProUGUI selectedBlockCounter;
    [SerializeField] Texture2D[] blockTextures;
    Sprite[] blockSprites;

    private float rotationY;

    private float lastJumpPress = float.MinValue;

    private float actionRange = 4.25f;

    private float startedMining;
    private GameObject minedBlock;
    private BlockType minedType;

    private int currentBlockType = 0;

    private Rigidbody rigidbody_;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        for (int i = 0; i < (int)BlockType.Count; i++)
        {
            blocks.Add(0);
        }
        rigidbody_ = GetComponent<Rigidbody>();
        rigidbody_.freezeRotation = true;
        // To make damping range in editor [0,1) we rescale it here
        dampingStopping /= Time.fixedDeltaTime;
        // make sure the player spawns above ground
        var gridPosition = GridControl.grid.WorldToCell(transform.position);
        transform.position = new Vector3(gridPosition.x, Chunk.MapGenerator.GetFilteredBiomeAndHeight( gridPosition.x, gridPosition.z).Item2 + 2, gridPosition.z);
        // assign the texture to selected block image
        
        selectedBlockCounter = selectedBlock.GetComponentInChildren<TextMeshProUGUI>();
        selectedBlockCounter.text = ":" + blocks[currentBlockType];

        // create sprites from textures
        blockSprites = new Sprite[blockTextures.Length];
        for (int i = 0; i < blockTextures.Length; i++)
        {
            blockSprites[i] = Sprite.Create(blockTextures[i], new Rect(0, 0, selectedBlock.sprite.rect.width, selectedBlock.sprite.rect.height), new Vector2(0.5f, 0.5f));
        }
    }

    void Update()
    {
        // rotate the player based on mouse movement
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        // rotate the camera and the player itself
        // if the player can rotate along y only we can use its forward and right vector as a projection of view to xz plane

        // we want to rotate with respect to right vector of the player so when we rotate whole player around y camera local x is right
        rotationY -= rotationSpeed * mouseY;
        rotationY = Mathf.Clamp(rotationY, -80, 90);
        camera_.transform.localRotation = Quaternion.Euler(rotationY, 0, 0);
        

        if (Input.GetKey(KeyCode.Mouse0))
        {
            if (Physics.Raycast(camera_.transform.position, camera_.transform.forward, out var hitInfo_, actionRange, raycastingMask) && hitInfo_.collider.gameObject.layer == GridControl.terrainLayer)
            {
                
                if (minedBlock != hitInfo_.collider.gameObject)
                {
                    startedMining = Time.time;
                    if (minedBlock != null)
                    {
                        // reset color of the previous block
                        var renderer = minedBlock.GetComponent<MeshRenderer>();
                        renderer.material.color = Color.white;
                    }
                    minedBlock = hitInfo_.collider.gameObject;
                    minedType = grid.GetTypeAt(hitInfo_.collider.gameObject.transform.position);
                }
                else
                {
                    var renderer = minedBlock.GetComponent<MeshRenderer>();
                    // make the block red when mined
                    renderer.material.color = Color.Lerp(Color.white, new Color(1.0f,0.75f,0.75f), (Time.time - startedMining) / GridControl.MiningTimes[(int)minedType]);
                    if (Time.time - startedMining >= GridControl.MiningTimes[(int)minedType])
                    {
                        var blockMined = grid.BlockDestroyed(hitInfo_.collider.gameObject.transform.position);
                        blocks[(int)blockMined] += 1;
                        selectedBlockCounter.text = ":" + blocks[currentBlockType];
                        // when the player is standing on top of the block it messes up ground checking
                        // so we need to check if the player is standing on top of the block
                        // if so we need to manually decrease ground check
                        // does not always prevent this for happening, player can enjoy "flying" then
                        var diff = transform.position - hitInfo_.collider.gameObject.transform.position;
                        if (diff.y < 1.475f && diff.y > 1.455f && Math.Abs(diff.x) < 0.5f && Math.Abs(diff.z) < 0.5f)
                        {
                            Debug.Log("Destroyed block under player");
                            groundCheck -= 1; // I wanna fly
                        }
                        Destroy(hitInfo_.collider.gameObject);
                    }
                }
                // Destroy the block
            }
            else
            {
                if (minedBlock != null)
                {
                    // reset color of the previous block
                    var renderer = minedBlock.GetComponent<MeshRenderer>();
                    renderer.material.color = Color.white;
                }
                minedBlock = null;
                minedType = BlockType.Count;
            }
            
        }
        else
        {
            if (minedBlock != null)
            {
                // reset color of the previous block
                var renderer = minedBlock.GetComponent<MeshRenderer>();
                renderer.material.color = Color.white;
            }
            minedBlock = null;
            minedType = BlockType.Count;
        }
        /*
        if ( && Physics.Raycast(camera.transform.position, camera.transform.forward, out var hitInfo, actionRange))
        {
            // Raycast hit some object
            
        }
        */
        if (Input.GetKeyDown(KeyCode.Mouse1) && Physics.Raycast(camera_.transform.position, camera_.transform.forward, out var rayInfo, actionRange, raycastingMask))
        {
            // Raycast hit some object
            if (rayInfo.collider.gameObject.layer == 8)
            {
                // inventory check
                if (blocks[(int) currentBlockType] > 0)
                {
                    // Create a block
                    // the new block is shifted by one unit in the direction of the normal
                    
                    var normal = rayInfo.normal;
                    var position = rayInfo.collider.gameObject.transform.position + normal;
                    var blockGridPosition = GridControl.grid.WorldToCell(position);
                    // player cannot spawn block at the same position as they are standing in
                    if (position.y < Chunk.ChunkHeight && ! (Math.Abs(transform.position.x - position.x) < 0.949f && Math.Abs(transform.position.z - position.z) < 0.949f && Math.Abs(transform.position.y - blockGridPosition.y) < 1.0f))
                    {
                        blocks[(int)currentBlockType] -= 1;
                        selectedBlockCounter.text = ":" + blocks[currentBlockType];
                        grid.AddBlock(position, (BlockType)currentBlockType);
                    }
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (menu.activeSelf)
            {
                menu.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Time.timeScale = 1;
            }
            else
            {
                menu.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Time.timeScale = 0;
            }
        }

        // scrolling mouse switches type of placed blocks
        if (Input.mouseScrollDelta.y != 0)
        {
            currentBlockType = (currentBlockType - (int)Input.mouseScrollDelta.y) % (int)BlockType.Count;
            if (currentBlockType < 0)
            {
                currentBlockType += (int)BlockType.Count;
            }
            selectedBlock.sprite = blockSprites[currentBlockType];
            selectedBlockCounter.text = ":" + blocks[currentBlockType];
        }
        transform.Rotate(new Vector3(0, rotationSpeed * mouseX, 0));
        //grid.UpdatePosition(transform.position);
        grid.UpdatePositionParallel(transform.position);

    }
    void FixedUpdate()
    {
        Vector3 direction = Vector3.zero;
        // small trick to allow changing gravity to make the jump feel better
        rigidbody_.AddForce(0,(gravityScale - 1) * Physics.gravity.y, 0, ForceMode.Acceleration);

        if (Input.GetKey(KeyCode.W))
        {
            direction += transform.forward;
        }
        if (Input.GetKey(KeyCode.S))
        {
            direction -= transform.forward;
        }
        if (Input.GetKey(KeyCode.A))
        {
            direction -= transform.right;
        }
        if (Input.GetKey(KeyCode.D))
        {
            direction += transform.right;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            lastJumpPress = Time.time;
        }
        if (groundCheck > 0 && Time.time - lastJumpPress < jumpDelay && rigidbody_.velocity.y < 0.5f)
        {
            rigidbody_.velocity = new Vector3(rigidbody_.velocity.x, jumpPower, rigidbody_.velocity.z);
        }

        if (direction == Vector3.zero) // no horizontal movement - use higher damping to stop almost instantly
        {
            float dampingCoefficient = 1 - dampingStopping * Time.fixedDeltaTime;
            rigidbody_.velocity = new Vector3(rigidbody_.velocity.x * dampingCoefficient, rigidbody_.velocity.y, rigidbody_.velocity.z * dampingCoefficient);
        }

        rigidbody_.velocity += direction * acceleration * Time.deltaTime;
        Vector3 horizontalVelocity = new Vector3(rigidbody_.velocity.x, 0, rigidbody_.velocity.z);
        // clamp magnitude of horizontal velocity
        if (horizontalVelocity.magnitude > speed)
        {
            horizontalVelocity = horizontalVelocity.normalized * speed;
        }
        rigidbody_.velocity = new Vector3(horizontalVelocity.x, rigidbody_.velocity.y,
            horizontalVelocity.z);
    }

    private int groundCheck = 0;

    private void OnTriggerEnter()
    {
        groundCheck += 1;
    }

    private void OnTriggerExit()
    {
        groundCheck -= 1;
    }

    private void OnTriggerStay()
    {

    }
}
