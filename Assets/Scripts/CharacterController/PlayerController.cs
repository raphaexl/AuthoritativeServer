
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
    public Transform cameraTrans { get; set; }
    public float animSpeed { get; set; }

    // Start is called before the first frame update
    private void Awake()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        cameraTrans = Camera.main.transform;
    }

    void Start()
    {
        yaw = 0;
        pitch = 0;
        targetRotation = Vector3.zero;
        speedHashCode = Animator.StringToHash("Speed");
    }

    void  LateCameraUpdatePosition()
    {
        cameraTrans.position = lookAtTargetTrans.position - distanceFromTarget * cameraTrans.forward;
    }

    private void LateCameraUpdate(Tools.NInput nInput)
    {
        yaw += nInput.MouseX * mouseSensitivity;
        pitch -= nInput.MouseY * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);
        targetRotation = new Vector3(pitch, yaw);
        cameraTrans.eulerAngles = targetRotation;
        LateCameraUpdatePosition();
    }

    public void ApplyInput(Tools.NInput nInput, float fpsTick)
    {
        if (!controller) { return; }

        
        isGrounded = Physics.CheckSphere(groundChecker.transform.position, groundCheckRadius, groundMask);
        running = nInput.Run;

        if (isGrounded && velocityY < 0f)
        { velocityY = 0f;}
        if (nInput.Jump)
        {Jump();}
        Vector2 inputDir = new Vector2(nInput.InputX, nInput.InputY).normalized;
        Move(inputDir, running, fpsTick);


        animSpeed = (running) ? currentSpeed / runSpeed : currentSpeed / walkSpeed * 0.5f;
       // animator.SetFloat(speedHashCode, animSpeed, moveSmoothTime, fpsTick);
        animator.SetFloat(speedHashCode, animSpeed);
        //        animator.SetFloat(speedHashCode, animSpeed, moveSmoothTime, Time.deltaTime);
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
        controller.Move(move * fpsTick);
      //  Debug.Log($"Before Current Speed : {currentSpeed}");
        currentSpeed = new Vector2(controller.velocity.x, controller.velocity.z). magnitude;
    //    Debug.Log($"After Current Speed : {currentSpeed}");
        velocityY += gravity * fpsTick;
    }

    void Jump()
    {
        if (isGrounded)
        {
            velocityY = Mathf.Sqrt(-2 * gravity * jumpHeight);
        }
       
    }
    float prevAnim = -1f;
    float _prevt = -1f;
    float _nowt = 1f;
    float _currTime = 0f;

    Vector3 _prevPos = new Vector3();
    Vector3 _currPos = new Vector3();

    public void SetState(Vector3 position, Quaternion rotation, float _animSpeed)
    {
        Debug.Log("Server State");

        //Debug.Log($"Current Position : ({transform.position.x}, {transform.position.y}, {transform.position.z}) Rotation : ({transform.rotation.x}, {transform.rotation.y}, {transform.rotation.z})");
        //Debug.Log($"Authorative Position : ({position.x}, {position.y}, {position.z}) Rotation : ({rotation.x}, {rotation.y}, {rotation.z})");
        if (prevAnim < 0f)
        {
            _prevt = 0f;// Time.timeSinceLevelLoad;
            prevAnim = _animSpeed;
           // _prevPos = position;
            Debug.Log("Always True");
        }
       // _nowt = Time.timeSinceLevelLoad;
        _nowt = Time.time;
        _currTime += Time.deltaTime;
        float dt_sec = _nowt - _prevt;
        float _moveSpeed = (position - _prevPos).magnitude;//  * (_nowt - _prevt);
        float __moveSpeed = Vector3.Distance(position, _prevPos);// (_nowt - _prevt);
        Debug.Log($"magnitute _move speed : {_moveSpeed} distance __move speed : {__moveSpeed} : dt_sec {dt_sec}");
        //animator.SetFloat(speedHashCode, _moveSpeed);
        //float speedAnimator = _moveSpeed > walkSpeed ? _moveSpeed / runSpeed : (_moveSpeed / walkSpeed) * 0.5f;
        // animator.SetFloat(speedHashCode,  __moveSpeed, moveSmoothTime, Time.deltaTime);
        // animator.SetFloat(speedHashCode, _moveSpeed);
        //  Debug.Log($"_move Speed Calculated : {_moveSpeed}, speedAnimator : {speedAnimator}, dt = {1.0f / (_nowt - _prevt)}");
        //animator.SetFloat(speedHashCode, speedAnimator, Mathf.Lerp(prevAnim, _animSpeed, _currTime / (_nowt - _prevt)), Time.deltaTime);
        // speedAnim = 0;
        /* float speedAnim = Vector3.Distance(position.normalized, _prevPos.normalized)/ (_nowt - _prevt);
         Debug.Log($"nowt : {_nowt} prevt ; {_prevt} anim speed : {speedAnim}");
         Debug.Log($"Controller Velocity : {controller.velocity}");*/
      //  animator.SetFloat(speedHashCode,  _animSpeed, Mathf.Lerp(prevAnim, _animSpeed, _currTime / (_nowt - _prevt)), Time.deltaTime);
        animator.SetFloat(speedHashCode, _animSpeed, moveSmoothTime, Time.deltaTime);
        transform.position = position;
        transform.rotation = rotation;
       // animator.SetFloat(speedHashCode, _animSpeed);
        LateCameraUpdatePosition();
        prevAnim = _animSpeed;
        _prevt = _nowt;
        _prevPos = position;
    }

    private void Update()
    {
        /*Tools.NInput nInput =  new Tools.NInput();
        nInput.InputX = Input.GetAxisRaw("Horizontal");
        nInput.InputY = Input.GetAxisRaw("Vertical");
        nInput.Jump = Input.GetButtonDown("Jump");
        nInput.MouseX = Input.GetAxis("Mouse X");
        nInput.MouseY = Input.GetAxis("Mouse Y");

        ApplyInput(nInput, 0.02f);*/
        Debug.DrawRay(transform.position, transform.forward * 20f);
    }
}
