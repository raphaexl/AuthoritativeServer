
using UnityEngine;

[RequireComponent(typeof (Animator),typeof (CharacterController))]
public class PlayerController : MonoBehaviourRPC
{
    Animator animator;
    CharacterController controller;
    int speedHashCode;
    [SerializeField]
    private Transform lookAtTargetTrans;
    [SerializeField]
    private float distanceFromTarget = 2f;
    [SerializeField]
    private float walkSpeed = 4f;
    [SerializeField]
    private float runSpeed = 6f;
    [SerializeField]
    private Transform groundChecker;
    private float groundCheckRadius = 0.6f;
    [SerializeField]
    private LayerMask groundMask;

    float jumpHeight = 3f;

    bool isGrounded;

    float currentSpeed = 0f;
    float gravity = -9.81f;
    float velocityY;
    float mouseSensitivity = 5f;
    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 pitchMinMax = new Vector2(-40f, 80f);
    private Vector3 targetRotation;

    bool running = false;
    float moveSmoothTime = 0.12f;

    /*
     *  Properties */

    //public bool OrbitControls { get; set; } // False for Any Player except Local Player
    public bool isReady { get; set; }
    public Transform cameraTrans { get; set; }
    public float animSpeed { get; set; }

    // Start is called before the first frame update
    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        speedHashCode = Animator.StringToHash("Speed");
    }

    void Start()
    {
        yaw = 0;
        pitch = 0;
        targetRotation = Vector3.zero;
    }

    void  LateCameraUpdatePosition()
    {
        cameraTrans.position = lookAtTargetTrans.position - distanceFromTarget * cameraTrans.forward;
    }

    private void LateCameraUpdate(Tools.NInput nInput)
    {
        yaw += nInput.MouseX * mouseSensitivity;
        pitch -= nInput.MouseY * mouseSensitivity;
      /*  yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;*/
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);
        targetRotation = new Vector3(pitch, yaw);
        cameraTrans.eulerAngles = targetRotation;
        LateCameraUpdatePosition();
    }

    public void ApplyInput(Tools.NInput nInput, float fpsTick)
    {
        if (!controller) { return; }
        if (!isReady) { return; }


        isGrounded = Physics.CheckSphere(groundChecker.transform.position, groundCheckRadius, groundMask);
        running = nInput.Run;

        if (isGrounded && velocityY < 0f)
        { velocityY = 0f;}
        if (nInput.Jump)
        {Jump();}
        Vector2 inputDir = new Vector2(nInput.InputX, nInput.InputY).normalized;
        Move(inputDir, running, fpsTick);
        //animSpeed = (running) ? currentSpeed / runSpeed : (currentSpeed / walkSpeed) * 0.5f;
        animSpeed = ((running) ? 1.0f :  0.5f) * inputDir.magnitude;
        // animator.SetFloat(speedHashCode, animSpeed, moveSmoothTime, fpsTick);
         animator.SetFloat(speedHashCode, animSpeed);
      //  animator.SetFloat(speedHashCode, animSpeed, moveSmoothTime, Time.deltaTime);
        LateCameraUpdate(nInput);
    }

    void Move(Vector2 inputDir, bool running, float fpsTick)
    {
        if (inputDir.magnitude > 0f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.y) * Mathf.Rad2Deg + cameraTrans.transform.eulerAngles.y;
            transform.eulerAngles = Vector3.up * targetAngle;
        }
        float targetSpeed = ((running) ? runSpeed : walkSpeed) * inputDir.magnitude;
        currentSpeed = targetSpeed; 
        Vector3 move = transform.forward * currentSpeed + velocityY * Vector3.up;
        // transform.Translate(move * fpsTick);
        controller.Move(move * fpsTick);
        //  Debug.Log($"Before Current Speed : {currentSpeed}");
        currentSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
      //  Debug.Log($"After Current Speed : {currentSpeed}");
        velocityY += gravity * fpsTick;
    }

    void Jump()
    {
        if (isGrounded)
        {
            velocityY = Mathf.Sqrt(-2 * gravity * jumpHeight);
        }
       
    }

    public void Spawn(Vector3 position, Quaternion rotation, Vector3 camPos, Quaternion camRot)
    {
        transform.position = position;
        transform.rotation = rotation;
        cameraTrans.position = camPos;
        cameraTrans.rotation = camRot;
    }

    public void SetState(Vector3 position, Quaternion rotation, float _animSpeed, Vector3 camPos, Quaternion camRot)
    {
        if (!isReady) { return; }
        transform.position = position;
        transform.rotation = rotation;
        cameraTrans.position = camPos;
        cameraTrans.rotation = camRot;
        animSpeed = _animSpeed;
        animator.SetFloat(speedHashCode, _animSpeed);
        //LateCameraUpdatePosition();
    }

    private void Update()
    {
        if (!isReady) { return; }
        Debug.DrawRay(transform.position, transform.forward * 20f);
    }

    private void OnDestroy()
    {
        Destroy(gameObject);
        Destroy(this);
    }
}
