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

    private List<uint> blocks = new List<uint>((int)BlockType.Count);

    // We need to enable chunks dynamically when the player walks near them

    private float rotationY;

    private float lastJumpPress = float.MinValue;

    private Rigidbody rigidbody;

    void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
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
        rotationY = Mathf.Clamp(rotationY, -90, 80);
        camera.transform.localRotation = Quaternion.Euler(rotationY, 0, 0);
        //camera.transform.Rotate(camera.transform.right, -rotationSpeed * mouseY, Space.World);

        if (Input.GetKeyDown(KeyCode.Mouse0) && Physics.Raycast(camera.transform.position, camera.transform.forward, out var hitInfo, 10f))
        {
            // Raycast hit some object
            if (hitInfo.collider.gameObject.layer == GridControl.terrainLayer)
            {
                // Destroy the block
                grid.BlockDestroyed(hitInfo.collider.gameObject.transform.position);
                // when the player is standing on top of the block it messes up ground checking
                // so we need to check if the player is standing on top of the block
                // if so we need to manually decrease ground check
                // does not always prevent this for happening, player can enjoy "flying" then
                var diff = transform.position - hitInfo.collider.gameObject.transform.position;
                if (diff.y < 0.52f && diff.y > 0.49f && Math.Abs(diff.x) < 0.5f && Math.Abs(diff.z) < 0.5f)
                {
                    Debug.Log("Destroyed block under player");
                    //groundCheck -= 1; // I wanna fly
                }
                Destroy(hitInfo.collider.gameObject);
            }
        }
        if (Input.GetKeyDown(KeyCode.Mouse1) && Physics.Raycast(camera.transform.position, camera.transform.forward, out var rayInfo, 10f))
        {
            // Raycast hit some object
            if (rayInfo.collider.gameObject.layer == 8)
            {
                // Destroy the block
                grid.BlockDestroyed(rayInfo.collider.gameObject.transform.position);
                Destroy(rayInfo.collider.gameObject);

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
