using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(CharacterController))]
public class PlayerNetwork : NetworkPackageController
{
    bool isLocalPlayer = false;
    bool isServer = false;

    [SerializeField]
    private float moveSpeed = .4f;
    [SerializeField]
    [Range(0.1f, 1.0f)]
    private float networkSendRate = 0.5f;
    [SerializeField]
    private bool isPredictionEnabled = default;
    [SerializeField]
    private float correctionThreshold = 0.01f;

    CharacterController controller;
    List<ReceivedPackage> predictedPackages;
    Vector3 lastPosition;

    private float nextCorrectTime;

    // Start is called before the first frame update
    void Start()
    {
        controller = GetComponent<CharacterController>();
        PackageManager.SendSpeed = networkSendRate;
        ServerPackageManager.SendSpeed = networkSendRate;
        predictedPackages = new List<ReceivedPackage>();
    }

    // Update is called once per frame
    void Update()
    {
        LocalPlayerUpdate();
        ServerPlayerUpdate();
        RemoteClientUpdate();
    }

    void LocalPlayerUpdate()
    {
        float inputX = Input.GetAxis("Horizontal");
        float inputY = Input.GetAxis("Vertical");

        if (!isLocalPlayer)
            return;
        if (inputX != 0 || inputY != 0)
        {
            float timestamp = Time.time;
            PackageManager.AddPackage(
                new SendPackage
                {
                    Horizontal = inputX,
                    Vertical = inputY,
                    Timestamp = timestamp
                });
            if (isPredictionEnabled)
            {
                Move(inputX * moveSpeed, inputY * moveSpeed);
                predictedPackages.Add(new ReceivedPackage
                {
                    Position = transform.position,
                    Timestamp = timestamp
                });
            }
        }

    }

    void ServerPlayerUpdate()
    {
        if (!isServer || isLocalPlayer)
            return;
        SendPackage packageData = PackageManager.GetNextDataReceived();
        if (packageData == null) { return; }
        Move(packageData.Horizontal * moveSpeed, packageData.Vertical * moveSpeed);
        if (transform.position == lastPosition)
            return;
        lastPosition = transform.position;

        ServerPackageManager.AddPackage(new ReceivedPackage
        {
            Position = transform.position,
            Timestamp = packageData.Timestamp
        }) ;
    }

    void Move(float horizontal, float vertical)
    {
        controller.Move(new Vector3(horizontal, 0, vertical));
    }

    private void RemoteClientUpdate()
    {
        if (isServer) { return; }
        ReceivedPackage data = ServerPackageManager.GetNextDataReceived();
        if (data == null)
            return;
        if (isLocalPlayer && isPredictionEnabled)
        {
            var transmittedPackage = predictedPackages.Where(x => x.Timestamp == data.Timestamp).FirstOrDefault();
            if (transmittedPackage == null)
            {
                //You should do something here :-) 
                return;
            }
            if (Vector3.Distance(transmittedPackage.Position,
                data.Position) > correctionThreshold)
            {
                transform.position = data.Position;
            }
            //Clear all pedicted
            predictedPackages.RemoveAll(x => x.Timestamp <= data.Timestamp);
        }
        else { transform.position = data.Position; }

    }
}
