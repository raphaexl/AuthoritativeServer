using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Animator))]
public class BasePlayerController : MonoBehaviour
{
    CharacterController controller;
    Animator animator;
    int speedHascode;

    [SerializeField]
    private float gravity = -12f;
    [SerializeField]
    private float jumpHeight = 2f;
    [SerializeField]
    [Range(0, 1)]
    private float airControlTime = 0.5f;
    private float velocityY = 0f;
    [SerializeField]
    private float walkSpeed = 4f;
    [SerializeField]
    private float runSpeed = 6f;

    private float turnSmoothTime = .6f;
    private float turnSmoothVelocity;
    private float currentTrunSpeed = 0f;

    private float moveSmoothTime = 0.12f;
    private float moveSmoothVelocity;
    private float currentMoveSpeed = 0f;

    private Transform cameraTrans;


    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        speedHascode = Animator.StringToHash("Speed");
        cameraTrans = Camera.main.transform;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 inputDir = input.normalized;
        bool running = Input.GetKey(KeyCode.LeftShift);
        Move(inputDir, running);
        if (Input.GetButtonDown("Jump"))
        {
            Jump();
        }
        float animSpeed = ((running) ? currentMoveSpeed / runSpeed : (currentMoveSpeed / walkSpeed) * 0.5f);
        animator.SetFloat(speedHascode, animSpeed, moveSmoothTime, Time.deltaTime);
    }

    void Move(Vector2 inputDir, bool running)
    {
        if (inputDir.magnitude > 0f)
        {
            float turnTargetRotation = Mathf.Atan2(inputDir.x, inputDir.y) * Mathf.Rad2Deg + cameraTrans.eulerAngles.y;
            currentTrunSpeed = Mathf.SmoothDampAngle(currentTrunSpeed, turnTargetRotation, ref turnSmoothVelocity, GetSmoothTime(turnSmoothTime));
            transform.eulerAngles = Vector3.up * currentTrunSpeed;
        }
        float moveTargetSpeed = (running ? runSpeed : walkSpeed) * inputDir.magnitude;
        currentMoveSpeed = Mathf.SmoothDamp(currentMoveSpeed, moveTargetSpeed, ref moveSmoothVelocity, GetSmoothTime(moveSmoothTime));

        velocityY += gravity * Time.deltaTime;
        Vector3 move = transform.forward * currentMoveSpeed + velocityY * Vector3.up;
        controller.Move(move * Time.deltaTime);
        currentMoveSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
        if (controller.isGrounded)
        {
            velocityY = 0f;
        }
        //transform.Translate(transform.forward * currentMoveSpeed * Time.deltaTime, Space.World);
    }

    void Jump()
    {
        if (controller.isGrounded)
        {
            velocityY = Mathf.Sqrt(-2f * gravity * jumpHeight);
        }
    }

    float GetSmoothTime(float smoothTime)
    {
        if (controller.isGrounded)
        {
            return smoothTime;
        }
        else
        {
            if (airControlTime == 0f) { return  float.MaxValue; }
            else
            {
                return (smoothTime / airControlTime);
            }
        }
    }
}
