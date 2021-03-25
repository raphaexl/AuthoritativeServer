
using UnityEngine;

public class PlayerController : MonoBehaviourRPC
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
    private float yRotation;

    // Start is called before the first frame update
    void Start()
    {
        xRotation = 0;
        yRotation = 0;
        controller = GetComponent<CharacterController>();
    }

    public void ApplyInput(Tools.NInput nInput, float fpsTick)
    {
        if (!controller) { return; }

        isGrounded = Physics.CheckSphere(groundChecker.transform.position, groundCheckRadius, groundMask);

        if (isGrounded && velocity.y < 0f)
        { velocity.y = -1f;}

        if (nInput.Jump)
        {velocity.y = Mathf.Sqrt(-2 * gravity * jumpHeight);}

        Vector3 move = transform.right * nInput.InputX + transform.forward * nInput.InputY;
      //  Vector3 move = Vector3.right * nInput.InputX + Vector3.forward * nInput.InputY;
         controller.Move(move * speed * fpsTick);

        nInput.MouseX *= mouseSensitivity * fpsTick;
        nInput.MouseY *= mouseSensitivity * fpsTick;
       
        xRotation -= nInput.MouseY;
        xRotation = Mathf.Clamp(xRotation, -40f, 80f);
        yRotation += nInput.MouseX;
    //    yRotation = Mathf.Clamp(yRotation, -180f, 180f);

        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0);
        //  transform.Rotate(Vector3.up * nInput.MouseX);
        //   cameraTrans.localRotation = Quaternion.Euler(xRotation, 0, 0);
        velocity.y += gravity * fpsTick;
         controller.Move(velocity * fpsTick);
       // transform.Translate(move * speed * fpsTick);
      //  Debug.Log($" Rotation :({transform.rotation.x}, {transform.rotation.x}, {transform.rotation.z}, {transform.rotation.w})");
    }

    public void ApplyTransform(Vector3 position, Quaternion rotation)
    {
        //Debug.Log($"Current Position : ({transform.position.x}, {transform.position.y}, {transform.position.z}) Rotation : ({transform.rotation.x}, {transform.rotation.y}, {transform.rotation.z})");
        //Debug.Log($"Authorative Position : ({position.x}, {position.y}, {position.z}) Rotation : ({rotation.x}, {rotation.y}, {rotation.z})");
        transform.position = position;
        transform.localRotation = rotation;
    }

    public void ApplyCameraRotation(float mouseX, float mouseY)
    {

    }


    private void Update()
    {
        Debug.DrawRay(transform.position, transform.forward * 20f);
    }
}
