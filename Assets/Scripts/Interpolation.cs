using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TransformState
{
    public Vector3 Position;
    //public Quaternion Rotation;

    public TransformState(Vector3 _position)
    {
        Position = _position;
    }
}

public class Interpolation : MonoBehaviour
{
    [SerializeField]
    Transform Path = default;
   [SerializeField]
    private float smoothTime = 0.3F;
    List<TransformState> transformStates;
    private Vector3 velocity = Vector3.zero;
    bool finished = false;
    Vector3 targetPosition;
    int nextWayPointIndex = 0;
    Vector3[] waypoints;
    int lastTransformState = 0;

    private void Start()
    {
        InitializeWayPoints();
        transform.position = waypoints[0];
        targetPosition = waypoints[1];
        transformStates = new List<TransformState>();


        //target.position = position2;
    }

    void InitializeWayPoints()
    {
        
        waypoints = new Vector3[Path.childCount];
        int index = 0;
        foreach (Transform waypoint in Path)
        {
            waypoints[index++] = waypoint.position;
        }
    }

    private void UpdateTransformState()
    {
        if (lastTransformState < waypoints.Length - 1)
        {
            transformStates.Add(new TransformState(waypoints[lastTransformState]));
            transformStates.Add(new TransformState(waypoints[++lastTransformState]));
            Debug.Log("Adding New Stuff");

            nextWayPointIndex = 0;
        }
        else
        {
            Debug.Log("The States Buffer is full");
        }
    }

    void UpdateTargetPosition()
    {
        if (finished && transformStates.Count > 0 && nextWayPointIndex < transformStates.Count)
        {
          //  targetPosition = waypoints[nextWayPointIndex];
            finished = false;
            //nextWayPointIndex = (nextWayPointIndex + 1) % waypoints.Length;
            targetPosition = transformStates[nextWayPointIndex].Position;
            nextWayPointIndex = (nextWayPointIndex + 1);
            {
                if (transformStates.Count > 2)
                {
                    Debug.Log($"Removed : next Way Point Index {nextWayPointIndex} ");
                    transformStates.RemoveAt(1);
                    transformStates.RemoveAt(0);
                    nextWayPointIndex = 0;
                }
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // target.TransformPoint(new Vector3(0, 5, -10));
        }
        if (!finished)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
        if (transform.position == targetPosition)
        {
            finished = true;
             UpdateTargetPosition();
          //  UpdateTransformState();

        }
        UpdateTransformState();
    }

    private void OnDrawGizmos()
    {
        Vector3 startPosition = Path.GetChild(0).position;
        Vector3 previousPosition = startPosition;
        foreach (Transform waypoint in Path)
        {
            Gizmos.DrawSphere(waypoint.position, 0.5f);
            Gizmos.DrawLine(previousPosition, waypoint.position);
            previousPosition = waypoint.position;
        }
    }
}
