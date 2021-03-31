using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField]
    private bool lockCursor; 
    [SerializeField]
    private float mouseSensitivity = 5f;
    [SerializeField]
    private Transform targetTransform;
    [SerializeField]
    private float distanceFromTarget = 2f;
    private float yaw = 0f;
    private float pitch = 0f;
    private Vector2 pitchMinMax = new Vector2(-40f, 80f);

    private Vector3 currentRotation;
    private Vector3 rotationVelocity;
    private float rotationSmoothTime = 0.1f;

   
    // Start is called before the first frame update
    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

        Vector3 targetRotation = new Vector3(pitch, yaw);

        currentRotation = Vector3.SmoothDamp(currentRotation, targetRotation, ref rotationVelocity, rotationSmoothTime);

        transform.eulerAngles = currentRotation;
        transform.position = targetTransform.position - distanceFromTarget * transform.forward;
    }
}
