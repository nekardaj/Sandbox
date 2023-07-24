using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [SerializeField] private Camera camera;

    // there is a collider in player layer that is used to check if the player is on the ground which should be ignored by raycasting
    [SerializeField] private LayerMask raycastingMask;

    [HideInInspector] public List<uint> blocks = new List<uint>((int)BlockType.Count);

    [SerializeField] private GameObject menu;

    // We need to enable chunks dynamically when the player walks near them

    private float rotationY;

    private float lastJumpPress = float.MinValue;

    private float actionRange = 4.0f;

    private float startedMining;
    private GameObject minedBlock;
    private BlockType minedType;

    private int currentBlockType = 0;

    private Rigidbody rigidbody;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        for (int i = 0; i < (int)BlockType.Count; i++)
        {
            blocks.Add(0);
        }
        rigidbody = GetComponent<Rigidbody>();
        rigidbody.freezeRotation = true;
        // To make damping range in editor [0,1) we rescale it here
        dampingStopping /= Time.fixedDeltaTime;
        // make sure the player spawns above ground
        var gridPosition = GridControl.grid.WorldToCell(transform.position);
        transform.position = new Vector3(0, Chunk.PerlinNoise(gridPosition.x, gridPosition.z) + 2, 0);
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
        camera.transform.localRotation = Quaternion.Euler(rotationY, 0, 0);
        //camera.transform.Rotate(camera.transform.right, -rotationSpeed * mouseY, Space.World);

        if (Input.GetKey(KeyCode.Mouse0))
        {
            if (Physics.Raycast(camera.transform.position, camera.transform.forward, out var hitInfo_, actionRange, raycastingMask) && hitInfo_.collider.gameObject.layer == GridControl.terrainLayer)
            {
                if (minedBlock != hitInfo_.collider.gameObject)
                {
                    startedMining = Time.time;
                    minedBlock = hitInfo_.collider.gameObject;
                    minedType = grid.GetTypeAt(hitInfo_.collider.gameObject.transform.position);
                }
                else
                {
                    if (Time.time - startedMining >= GridControl.MiningTimes[(int)minedType])
                    {
                        var blockMined = grid.BlockDestroyed(hitInfo_.collider.gameObject.transform.position);
                        blocks[(int)blockMined] += 1;
                        // when the player is standing on top of the block it messes up ground checking
                        // so we need to check if the player is standing on top of the block
                        // if so we need to manually decrease ground check
                        // does not always prevent this for happening, player can enjoy "flying" then
                        var diff = transform.position - hitInfo_.collider.gameObject.transform.position;
                        if (diff.y < 0.52f && diff.y > 0.49f && Math.Abs(diff.x) < 0.5f && Math.Abs(diff.z) < 0.5f)
                        {
                            Debug.Log("Destroyed block under player");
                            //groundCheck -= 1; // I wanna fly
                        }
                        Destroy(hitInfo_.collider.gameObject);
                    }
                }
                // Destroy the block
                
            }
            else
            {
                minedBlock = null;
                minedType = BlockType.Count;
            }
        }
        else
        {
            minedBlock = null;
            minedType = BlockType.Count;
        }
        /*
        if ( && Physics.Raycast(camera.transform.position, camera.transform.forward, out var hitInfo, actionRange))
        {
            // Raycast hit some object
            
        }
        */
        if (Input.GetKeyDown(KeyCode.Mouse1) && Physics.Raycast(camera.transform.position, camera.transform.forward, out var rayInfo, actionRange, raycastingMask))
        {
            // Raycast hit some object
            if (rayInfo.collider.gameObject.layer == 8)
            {
                // Create a block
                // the new block is shifted by one unit in the direction of the normal
                var normal = rayInfo.normal;
                var position = rayInfo.collider.gameObject.transform.position + normal;
                if (position.y < Chunk.ChunkHeight)
                {
                    grid.AddBlock(position, (BlockType)currentBlockType);
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

        if (Input.mouseScrollDelta.y != 0)
        {
            currentBlockType = (currentBlockType + (int)Input.mouseScrollDelta.y) % (int)BlockType.Count;
            if (currentBlockType < 0)
            {
                currentBlockType += (int)BlockType.Count;
            }
        }
        transform.Rotate(new Vector3(0, rotationSpeed * mouseX, 0));
        grid.UpdatePosition(transform.position);

    }
    void FixedUpdate()
    {
        Vector3 direction = Vector3.zero;
        // small trick to allow changing gravity to make the jump feel better
        rigidbody.AddForce(0,(gravityScale - 1) * Physics.gravity.y, 0, ForceMode.Acceleration);

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
        if (groundCheck > 0 && Time.time - lastJumpPress < jumpDelay && rigidbody.velocity.y < 0.5f)
        {
            rigidbody.velocity = new Vector3(rigidbody.velocity.x, jumpPower, rigidbody.velocity.z);
        }

        if (direction == Vector3.zero) // no horizontal movement - use higher damping to stop almost instantly
        {
            float dampingCoefficient = 1 - dampingStopping * Time.fixedDeltaTime;
            rigidbody.velocity = new Vector3(rigidbody.velocity.x * dampingCoefficient, rigidbody.velocity.y, rigidbody.velocity.z * dampingCoefficient);
        }

        rigidbody.velocity += direction * acceleration * Time.deltaTime;

        // horizontal velocity
        Vector3 horizontalVelocity = new Vector3(rigidbody.velocity.x, 0, rigidbody.velocity.z);
        // clamp magnitude of horizontal velocity
        if (horizontalVelocity.magnitude > speed)
        {
            horizontalVelocity = horizontalVelocity.normalized * speed;
        }
        rigidbody.velocity = new Vector3(horizontalVelocity.x, rigidbody.velocity.y,
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
}
