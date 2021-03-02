using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private Transform groundChecker;
    private float groundCheckRadius = 0.4f;
    [SerializeField]
    private LayerMask groundMask;

    float jumpHeight = 3f;

    bool isGrounded;

    CharacterController controller;
    float speed = 10f;
    float gravity = -9.81f;
    Vector3 velocity;
    float mouseSensitivity = 100f;
    private float xRotation;
    private Transform cameraTrans;

    // Start is called before the first frame update
    void Start()
    {
        xRotation = 0;
        controller = GetComponent<CharacterController>();
        cameraTrans = transform.GetChild(0).transform;
    }

    public void ApplyInput(Tools.NInput nIpunt, float fpsTick)
    {
        if (!controller) { return; }

        isGrounded = Physics.CheckSphere(groundChecker.transform.position, groundCheckRadius, groundMask);

        if (isGrounded && velocity.y < 0f)
        { velocity.y = -1f;}

        if (nIpunt.jump)
        { velocity.y = Mathf.Sqrt(-2 * gravity * jumpHeight);}

        Vector3 move = transform.right * nIpunt.inputX + transform.forward * nIpunt.inputY;
        controller.Move(move * speed * fpsTick);

        nIpunt.mouseX *= mouseSensitivity * fpsTick;
        nIpunt.mouseY *= mouseSensitivity * fpsTick;
       
        xRotation -= nIpunt.mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        // transform.Rotate(Vector3.up * nIpunt.mouseX);
        //cameraTrans.localRotation = Quaternion.Euler(xRotation, 0, 0);

         /*velocity.y += gravity * fpsTick;
         controller.Move(velocity * fpsTick);*/
    }

    public void ApplyTransform(Vector3 position, Quaternion rotation)
    {
        //Debug.Log($"Current Position : ({transform.position.x}, {transform.position.y}, {transform.position.z}) Rotation : ({transform.rotation.x}, {transform.rotation.y}, {transform.rotation.z})");
        //Debug.Log($"Authorative Position : ({position.x}, {position.y}, {position.z}) Rotation : ({rotation.x}, {rotation.y}, {rotation.z})");
        transform.position = position;
        transform.rotation = rotation;
    }

    public void ApplyCameraRotation(float mouseX, float mouseY)
    {

    }


    private void Update()
    {
        Debug.DrawRay(transform.position, transform.forward * 20f);

    }
}
