using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
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
    [Tooltip("When player is not pressing any move button movement is slowed using this value")]
    [SerializeField] private float dampingStopping;
    //[SerializeField] private float dampingJumping;
    

    [SerializeField] private Camera camera;

    private float rotationX;
    private float rotationY;

    private float lastJumpPress = float.MinValue;

    private Rigidbody rigidbody;

    void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
        rigidbody = GetComponent<Rigidbody>();
        rigidbody.freezeRotation = true;
        Debug.Log(Physics.gravity.y);
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
        rotationY = Mathf.Clamp(rotationY, -80, 80);
        camera.transform.localRotation = Quaternion.Euler(rotationY, 0, 0);
        //camera.transform.Rotate(camera.transform.right, -rotationSpeed * mouseY, Space.World);

        rotationX += rotationSpeed * mouseX;

        //camera.transform.localRotation.Set(camera.transform.localRotation.x, 0, 0, 0);
        transform.Rotate(new Vector3(0, rotationSpeed * mouseX, 0));

    }
    void FixedUpdate()
    {
        Vector3 direction = Vector3.zero;
        // small trick to allow changing gravity to make the jump feel better
        rigidbody.AddForce(0,(gravityScale - 1) * Physics.gravity.y, 0, ForceMode.Acceleration);

        if (Input.GetKey(KeyCode.W))
        {
            direction += transform.forward;
            //rigidbody.AddForce(transform.forward * speed, ForceMode.Impulse);
        }
        if (Input.GetKey(KeyCode.S))
        {
            direction -= transform.forward;
            //rigidbody.AddForce( -transform.forward * speed, ForceMode.Impulse);
        }
        if (Input.GetKey(KeyCode.A))
        {
            direction -= transform.right;
            //rigidbody.AddForce(-transform.right * speed, ForceMode.Impulse);
        }
        if (Input.GetKey(KeyCode.D))
        {
            direction += transform.right;
            //rigidbody.AddForce(transform.right * speed, ForceMode.Impulse);
        }

        
        if (Input.GetKey(KeyCode.Space))
        {
            lastJumpPress = Time.time;
            //rigidbody.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

        }
        if (groundCheck > 0 && Time.time - lastJumpPress < jumpDelay)
        {
            rigidbody.velocity = new Vector3(rigidbody.velocity.x, jumpPower, rigidbody.velocity.z);
        }

        if (direction == Vector3.zero) // no horizontal movement - use higher damping to stop almost instantly
        {
            float dampingCoefficient = 1 - dampingStopping * Time.fixedDeltaTime;
            rigidbody.velocity = new Vector3(rigidbody.velocity.x * dampingCoefficient, rigidbody.velocity.y, rigidbody.velocity.z * dampingCoefficient);
        }

        rigidbody.velocity += direction * acceleration * Time.deltaTime;


        // TODO easier and better might be let unity handle the drag and just stop player when they are not moving
        // when changing direction preserve only the component of velocity that is in the direction of movement
        // when changing direction check dot product

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

    private bool Grounded = false;
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
