using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Linq;


public class ClientInputState
{
    public Tools.NInput nInput;
    public float NTime; //When did we set that input (received it);
    public float Timestamp;

    public ClientInputState()
    {
        this.Timestamp = -1;
    }

    public ClientInputState(Tools.NInput _nInput, float _nTime, float _timestamp)
    {
        this.nInput = _nInput;
        this.NTime = _nTime;
        this.Timestamp = _timestamp;
    }
}

public class SimulationState
{
    public int peerId;
    public float Timestamp;
    public float animSpeed;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 camPosition;
    public Quaternion camRotation;

    public SimulationState()
    {
        peerId = 0;
        animSpeed = 0;
        position = Vector3.zero;
        rotation = Quaternion.identity;
        camPosition = Vector3.zero;
        camRotation = Quaternion.identity;
    }

    public SimulationState(PlayerStatePacket playerStatePacket)
    {
        peerId = playerStatePacket.Id;
        position = playerStatePacket.Position;
        rotation = playerStatePacket.Rotation;
        animSpeed = playerStatePacket.AnimSpeed;
        Timestamp = playerStatePacket.Timestamp;
        camPosition = playerStatePacket.camPosition;
        camRotation = playerStatePacket.camRotation;
    }

    public override string ToString()
    {
        return ($" Id {peerId} \n" +
            $" Pos({position.x}, {position.y}, {position.z}) \n" +
            $": Rot({rotation.x}, {rotation.y}, {rotation.z}, {rotation.w}) \n" +
            $" : Timestamp + {Timestamp} " +
            $" : AnimSpeed : {animSpeed} ");
    }
}

public class ServerState
{
    public int peerId;
    public  float  Timestamp;
    public float animSpeed;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 camPosition;
    public Quaternion camRotation;

    public ServerState(PlayerStatePacket playerStatePacket)
    {
        peerId = playerStatePacket.Id;
        position = playerStatePacket.Position;
        rotation = playerStatePacket.Rotation;
        animSpeed = playerStatePacket.AnimSpeed;
        Timestamp = playerStatePacket.Timestamp;
        camPosition = playerStatePacket.camPosition;
        camRotation = playerStatePacket.camRotation;
    }

    public override string ToString()
    {
        return ($" Id {peerId} \n" +
            $" Pos({position.x}, {position.y}, {position.z}) \n" +
            $": Rot({rotation.x}, {rotation.y}, {rotation.z}, {rotation.w}) \n" +
            $" : Timestamp + {Timestamp} " +
            $" : AnimSpeed : {animSpeed} "); 
    }
}

public class Client : NetworkManagerClient
{
    public static Client Instance;
    [SerializeField]
    GameObject playerGO = default;

    List<PlayerViewClient> _remotePlayers;
    PlayerViewClient _localPlayer;

    private int Id;
    private Tools.NInput currentNInput;
    private Tools.NInput previousNInput;
    //List of Predicted Packages
    List<ClientState> _predictedPackages;
    float _previousTime;
    //List of Positions for Lag Come

    [SerializeField]
    float Lag;
    [SerializeField]
    bool clientSidePrediction = default;
    [SerializeField]
    bool serverReconciliation = default;
    [SerializeField]
    public bool interpolation = default;


    bool isConnected = false;

    Coroutine customUpdateCoroutine;

    //List of the Player Views Here
    public Dictionary<int, PlayerViewClient> playerViewClients;
    private CustomFixedUpdate FU_instance;

    private Dictionary<int, GameObject> _clients;

    private float m_sendSpeed;
    public float SendSpeed
    {
        get
        {
            if (m_sendSpeed < 0.1f)
            {
                m_sendSpeed = 0.1f;
            }
            return m_sendSpeed;
        }
        set
        {
            m_sendSpeed = value;
        }
    }
    private float nextTick = 0f;

    public void Tick()
    {
        nextTick += (1.0f / (this.SendSpeed * Time.fixedDeltaTime));
        if (nextTick > 1.0f)//&& Packages.Count > 0)
        {
            nextTick = 0f;
            /* if (OnRequiredPackageTransmit != null)
             {
                 byte[] bytes = CreateBytes();
                 Packages.Clear();
                 OnRequiredPackageTransmit(bytes);
             }*/
        }
    }


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    // this method will be called AuthSetrver.GAME_FPS times per second
    void OnFixedUpdate(float dt)
    {
        if (!_localPlayer || !_localPlayer.isReady) { return; }
        ProcessInputs();
    }


    // Start is called before the first frame update
    new void Start()
    {
        Id = -1;
        base.Start();
        StartClient();
        _localPlayer = null;
        _remotePlayers = new List<PlayerViewClient>();
        isConnected = false;
        clientSidePrediction = false;
        serverReconciliation = false;
        interpolation = false;
        _previousTime = Time.time;
        playerViewClients = new Dictionary<int, PlayerViewClient>();
        // FU_instance = new CustomFixedUpdate(1.0f / AuthServer.GAME_FPS, OnFixedUpdate);
        _clients = new Dictionary<int, GameObject>();
        _predictedPackages = new List<ClientState>();
        //Should be OnEnable
        // customUpdateCoroutine = StartCoroutine(CustomUpdate(AuthServer.GAME_FPS));
    }

    /*
    private void OnEnable()
    {
        customUpdateCoroutine = StartCoroutine(CustomUpdate(GAME_FPS));
    }*/

    public void ConnectToServer()
    {
        ClientUIController.Instance.onClientConnected.text = "Connecting...";
        ClientUIController.Instance.onClientReceiveFromServer.text = "";
        base.Connect(AuthServer.CONNECTION_KEY);
    }

    private void FixedUpdate()
    {

        if (!_localPlayer || !_localPlayer.isReady) { return; }
        ProcessInputs();
        LagCompensation();
    }


    IEnumerator CustomUpdate(float fpsRate)
    {
        // WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1.0f / fpsRate);
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1.0f);
        while (true)
        {
            if (!_localPlayer || !_localPlayer.isReady) { yield return wait; }
            ProcessInputs();
            LagCompensation();
            yield return wait;
        }
    }
    //Won't allow update from inherited

    new private void Update()
    {
        base.Update();
        if (!isConnected) { return; }
        //FU_instance.Update();
        clientSidePrediction = ClientUIController.Instance.clientSidePrediction;
        serverReconciliation = ClientUIController.Instance.serverReconcilation;
        interpolation = ClientUIController.Instance.lagCompensation;
    }

    #region LiteNetLib overloads
    private void OnDisable()
    {
        if (customUpdateCoroutine != null) { StopCoroutine(customUpdateCoroutine); }
        customUpdateCoroutine = null;
    }

    public void DiconnectToServer()
    {
        ClientUIController.Instance.onClientConnected.text = "Disconnected";
        base.Connect(AuthServer.CONNECTION_KEY);
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        ClientUIController.Instance.onClientConnected.text = $"Disconned from the Server";
    }

    public override void OnPeerConnected(NetPeer peer)
    {
        ClientUIController.Instance.OnEnableToogle();
        Debug.Log(" You are connected is it to the Server ?? " + peer.Id);
    }
    #endregion

    #region Spawning and Instantiation of players
    void SpawnPlayer(SpawnPacket spawnPacket)
    {
        PlayersInstanciation(spawnPacket.PlayerId, spawnPacket.Position,
            spawnPacket.Rotation, spawnPacket.Albedo, spawnPacket.CameraPosition,
            spawnPacket.CameraRotation, spawnPacket.PlayerId == Id);
    }

    public void PlayersInstanciation(int id, Vector3 position, Quaternion rotation, Color color, Vector3 CameraPosition, Quaternion CameraRotation, bool isLocalPlayer)
    {
        PlayerViewClient foundPlayer = _remotePlayers.Find(player => player.Id == id);
        if (foundPlayer == null)
        {
            if (isLocalPlayer && _localPlayer != null) { return; }
            GameObject camEmpty = new GameObject();
            GameObject go = Instantiate(playerGO);
            camEmpty.name = "CameraTransForm";
            camEmpty.transform.position = CameraPosition;
            camEmpty.transform.rotation = CameraRotation;
            if (isLocalPlayer)
            {
                go.name = "LocalPlayer";
                camEmpty.transform.SetParent(go.transform);
                _clients.Add(id, go);
                _localPlayer = go.AddComponent<PlayerViewClient>();
                _localPlayer.Spawn(isLocalPlayer, id, position, rotation, color, camEmpty.transform);
                if (_localPlayer != null) {
                    _localPlayer.isReady = true;
                    _localPlayer.isMine = true;
                }
            }
            else
            {
                go.name = "RemotePlayer";
                _clients.Add(id, go);
                PlayerViewClient remotePlayer = go.AddComponent<PlayerViewClient>();
                remotePlayer.isReady = true;
                remotePlayer.isMine = false;
                remotePlayer.Spawn(isLocalPlayer, id, position, rotation, color, camEmpty.transform);
                _remotePlayers.Add(remotePlayer);
            }
        }

    }
    #endregion
    #region Inputs Of The Clients
    bool UpdateInput()
    {
        float deltaInputX = Mathf.Abs(currentNInput.InputX - previousNInput.InputX);
        float deltaInputY = Mathf.Abs(currentNInput.InputY - previousNInput.InputY);
        float deltaMouseX = Mathf.Abs(currentNInput.MouseX - previousNInput.MouseX);
        float deltaMouseY = Mathf.Abs(currentNInput.MouseY - previousNInput.MouseY);
        float epsilon = 0f;

        return (
             currentNInput.Jump != previousNInput.Jump || currentNInput.Run != previousNInput.Run || deltaInputX > epsilon || deltaInputY > epsilon
             || deltaMouseX > epsilon || deltaMouseY > epsilon || Mathf.Abs(currentNInput.InputX) > epsilon
             || Mathf.Abs(currentNInput.InputY) > epsilon
             );
        //return currentNInput != previousNInput;
    }
    public void setPlayerInputs(Tools.NInput nInput)
    {
        currentNInput = nInput;
    }
    #endregion
    #region Attempt of tick ! Works :-)

    //System.Random random = new System.Random();

    void ProcessInputs()
    {
        if (!UpdateInput())
            return;
        InputPacket inputPacket = new InputPacket
        {
            InputX = currentNInput.InputX,
            InputY = currentNInput.InputY,
            MouseX = currentNInput.MouseX,
            MouseY = currentNInput.MouseY,
            Jump = currentNInput.Jump,
            Run = currentNInput.Run
        };
        inputPacket.NTime = Time.fixedDeltaTime;
        float timestamp = Time.time;
        inputPacket.Timestamp = timestamp;
        // if (random.Next(10, 20) < 15)
        SendInputToServer(inputPacket);
        //Client side prediction
        if (clientSidePrediction)
        {
            _localPlayer.GetComponent<PlayerController>().ApplyInput(currentNInput, inputPacket.NTime);
            _predictedPackages.Add(new ClientState
            {
                Position = _localPlayer.GetComponent<PlayerController>().transform.position,
                Rotation = _localPlayer.GetComponent<PlayerController>().transform.rotation,
                AnimSpeed = _localPlayer.GetComponent<PlayerController>().animSpeed,
                CamPosition = _localPlayer.GetComponent<PlayerController>().cameraTrans.position,
                CamRotation = _localPlayer.GetComponent<PlayerController>().cameraTrans.rotation,
                Timestamp = timestamp,
            });
        }
        previousNInput = currentNInput;
    }

    #endregion
    #region Packets from The Server

    public override void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        PacketType type;

        type = (PacketType)reader.GetInt();
        switch (type)
        {
            case PacketType.Join:
                {
                    int peerId = reader.GetInt();
                    if (!isConnected) {
                        isConnected = true;
                        netPeer = peer;
                        Id = peerId;
                        ClientUIController.Instance.onClientConnected.text = $"connected to the server with : {peerId}";
                        SendCameraSetupToServer();
                    }
                    else
                    {
                        string msg = ClientUIController.Instance.onClientReceiveFromServer.text;
                        if (Id != peerId)
                        {
                            msg += $"[SERVER] :  A Player Joined with id : {peerId}";
                        }
                        ClientUIController.Instance.onClientReceiveFromServer.text = msg;
                    }
                }
                break;
            case PacketType.Spawn:
                {
                    SpawnPacket spawnPacket = new SpawnPacket();
                    spawnPacket.Deserialize(reader);
                    SpawnPlayer(spawnPacket);
                }
                break;
            case PacketType.Leave:
                {
                    int peerId = reader.GetInt();
                    if (peerId == Id)
                    {
                        SelfRemove();
                    }
                    else
                    {
                        OthersRemove(peerId);
                    }
                }
                break;
            case PacketType.ServerState:
                {
                    PlayerStatePacket playerStatePacket = new PlayerStatePacket();
                    playerStatePacket.Deserialize(reader);
                    ServerState serverState = new ServerState(playerStatePacket);
                    OnServerStatePacketReceive(serverState);
                    Debug.Log($"Server State Received For Player : {serverState.peerId}");
                }
                break;
            case PacketType.RPC:
                {
                    RPCPacket rPCPacket;
                    rPCPacket = new RPCPacket();
                    rPCPacket.Deserialize(reader);
                    if (rPCPacket.parameterLength == 0)
                    {
                        ReceiveRPCFromServer(rPCPacket.methodName, rPCPacket.senderPeerId, new object[0]);
                    }
                    else {
                        ReceiveRPCFromServer(rPCPacket.methodName, rPCPacket.senderPeerId, rPCPacket.parameters);
                    }
                }
                break;
            default:
                ClientUIController.Instance.onClientReceiveFromServer.text = "UNKNOWN PACKET";
                Debug.LogWarning("Received Unknown Packet From The Server");
                break;
        }
        reader.Recycle();
    }
    #endregion
    #region Client Side Prediction and Server Reconciliation

    void ServerAuthoritativeState(ServerState serverState)
    {
        if (_localPlayer) {
            //Reconciliation will be needed here
            if (clientSidePrediction && serverReconciliation)
            {
                float correctionThreshold = 0.001f;
                var transmittedPackage = _predictedPackages.Where(x => x.Timestamp == serverState.Timestamp).FirstOrDefault();
                if (transmittedPackage == null)
                {
                    //Debug.Log("You should do something here :-) ");
                    // _localPlayer.ApplyServerState(serverState.position, serverState.rotation, serverState.animSpeed, serverState.camPosition, serverState.camRotation);
                    return;
                }
                if (Vector3.Distance(transmittedPackage.Position,
                    serverState.position) > correctionThreshold)
                {
                    //Debug.Log("Reconciliation");
                    _localPlayer.ApplyServerState(serverState.position, serverState.rotation, serverState.animSpeed, serverState.camPosition, serverState.camRotation);
                }
                //Clear all pedicted
                _predictedPackages.RemoveAll(x => x.Timestamp <= serverState.Timestamp);
            }
            else
            {
                _localPlayer.ApplyServerState(serverState.position, serverState.rotation, serverState.animSpeed, serverState.camPosition, serverState.camRotation);
            }
        }
        else { Debug.Log("But Why ???"); return; }
    }

    #endregion
    #region OnServerStatePacket Receive for clients states

     void LagCompensation()
    {
        _remotePlayers.ForEach(remotePlayer =>
        {
        float latestAnimSpeed = remotePlayer.remoteClientState.latestAnimSpeed;
        Vector3 latestPos = remotePlayer.remoteClientState.latestPos;
        Quaternion latestRot = remotePlayer.remoteClientState.latestRot;
        Vector3 latestCamPos = remotePlayer.remoteClientState.latestCamPos;
        Quaternion latestCamRot = remotePlayer.remoteClientState.latestCamRot;
        //Lag compensation
        float currentTime = remotePlayer.remoteClientState.currentTime;
        double currentPacketTime = remotePlayer.remoteClientState.currentPacketTime;
        double lastPacketTime = remotePlayer.remoteClientState.lastPacketTime;
        float animSpeedAtLastPacket = remotePlayer.remoteClientState.animSpeedAtLastPacket;
        Vector3 positionAtLastPacket = remotePlayer.remoteClientState.positionAtLastPacket;
        Quaternion rotationAtLastPacket = remotePlayer.remoteClientState.rotationAtLastPacket;
        Vector3 camPositionAtLastPacket = remotePlayer.remoteClientState.camPositionAtLastPacket;
        Quaternion camRotationAtLastPacket = remotePlayer.remoteClientState.camRotationAtLastPacket;

        currentTime += Time.deltaTime;
        double timeToReachGoal = currentPacketTime - lastPacketTime;
        //Update remote player
        remotePlayer.ApplyServerState(
            Vector3.Lerp(positionAtLastPacket, latestPos, (float)(currentTime / timeToReachGoal)),
       Quaternion.Lerp(rotationAtLastPacket, latestRot, (float)(currentTime / timeToReachGoal)),
       Mathf.Lerp(animSpeedAtLastPacket, latestAnimSpeed, (float)(currentTime / timeToReachGoal)),
       Vector3.Lerp(camPositionAtLastPacket, latestCamPos, (float)(currentTime / timeToReachGoal)),
       Quaternion.Lerp(camRotationAtLastPacket, latestCamRot, (float)(currentTime / timeToReachGoal)));
        remotePlayer.remoteClientState.currentTime = currentTime;
    });
}

void OnServerStatePacketReceive(ServerState serverState)
{
    if (serverState.peerId != Id)
    {
        Debug.Log("Not me");
        if (_remotePlayers.Count < 1) { return; }
        PlayerViewClient foundPlayer = _remotePlayers.Find(player => player.Id == serverState.peerId);
        if (foundPlayer == null) { Debug.Log($"Should never happen ! Remote Players Number : {_remotePlayers.Count}"); return; }
        {

            foundPlayer.remoteClientState.currentTime = 0.0f;
            foundPlayer.remoteClientState.lastPacketTime = foundPlayer.remoteClientState.currentPacketTime;
            foundPlayer.remoteClientState.currentPacketTime = Time.timeSinceLevelLoad;

            foundPlayer.remoteClientState.latestAnimSpeed = serverState.animSpeed;
            foundPlayer.remoteClientState.latestPos = serverState.position;
            foundPlayer.remoteClientState.latestRot = serverState.rotation;
            foundPlayer.remoteClientState.latestCamPos = serverState.camPosition;
            foundPlayer.remoteClientState.latestCamRot = serverState.camRotation;

            foundPlayer.remoteClientState.animSpeedAtLastPacket = foundPlayer.GetComponent<PlayerController>().animSpeed;
            foundPlayer.remoteClientState.positionAtLastPacket = foundPlayer.GetComponent<PlayerController>().transform.position;
            foundPlayer.remoteClientState.rotationAtLastPacket = foundPlayer.GetComponent<PlayerController>().transform.rotation;
            foundPlayer.remoteClientState.camPositionAtLastPacket = foundPlayer.GetComponent<PlayerController>().cameraTrans.position;
            foundPlayer.remoteClientState.camRotationAtLastPacket = foundPlayer.GetComponent<PlayerController>().cameraTrans.rotation;
        }
    }
    else
    {
        ServerAuthoritativeState(serverState);
    }

}

    #endregion
    #region Remove Self or Client on disconnection
    void SelfRemove()
    {
        _remotePlayers.Clear();
        playerViewClients.Clear();
        _clients.Clear();
        _predictedPackages.Clear();
        Destroy(_localPlayer.GetComponent<PlayerViewClient>());
        Destroy(_localPlayer);
        _clients.Remove(Id);
        Destroy(gameObject);
    }

    void OthersRemove(int id)
    {
        PlayerViewClient toDelete = _remotePlayers.Find(player => player.Id == id);
        if (toDelete != null)
        {
            Destroy(toDelete.GetComponent<PlayerViewClient>());
            _remotePlayers.Remove(toDelete);
            Destroy(toDelete);
            toDelete = null;
            playerViewClients.Remove(id);
            Debug.Log("Removed");
        }
        else
        {
            Debug.Log("Not removed Because not found");
        }
        _clients.Remove(Id);
    }
    #endregion
    #region RPC Helper
    string RPCParametersOrder(params object[] parameters)
    {
        System.Text.StringBuilder paramsOrder = new System.Text.StringBuilder();

        for (int i = 0; i < parameters.Length; i++)
        {
            Type type = parameters[i].GetType();

            if (type.Equals(typeof(float)))
            {
                paramsOrder.Append(RPCParametersTypes.FLOAT);
            }
            else if (type.Equals(typeof(double)))
            {
                paramsOrder.Append(RPCParametersTypes.DOUBLE);
            }
            else if (type.Equals(typeof(long)))
            {
                paramsOrder.Append(RPCParametersTypes.LONG);
            }
            else if (type.Equals(typeof(ulong)))
            {
                paramsOrder.Append(RPCParametersTypes.ULONG);
            }
            else if (type.Equals(typeof(int)))
            {
                paramsOrder.Append(RPCParametersTypes.INT);
            }
            else if (type.Equals(typeof(uint)))
            {
                paramsOrder.Append(RPCParametersTypes.UINT);
            }
            else if (type.Equals(typeof(char)))
            {
                paramsOrder.Append(RPCParametersTypes.CHAR);
            }
            else if (type.Equals(typeof(ushort)))
            {
                paramsOrder.Append(RPCParametersTypes.USHORT);
            }
            else if (type.Equals(typeof(short)))
            {
                paramsOrder.Append(RPCParametersTypes.SHORT);
            }
            else if (type.Equals(typeof(sbyte)))
            {
                paramsOrder.Append(RPCParametersTypes.BYTE);
            }
            else if (type.Equals(typeof(byte)))
            {
                paramsOrder.Append(RPCParametersTypes.BYTE);
            }
            else if (type.Equals(typeof(bool)))
            {
                paramsOrder.Append(RPCParametersTypes.BOOL);
            }
            else if (type.Equals(typeof(string)))
            {
                paramsOrder.Append(RPCParametersTypes.STRING);
            }
            else if (type.Equals(typeof(string)))
            {
                paramsOrder.Append(RPCParametersTypes.STRING);
            }
            else
            {
                Debug.LogError("An RPC with unsupported parameters type");
            }
        }
        return paramsOrder.ToString();

    }
    #endregion

    #region Other things
    // Send the camera transform
    protected void SendCameraSetupToServer()
    {
        CameraSetupPacket cameraSetup =  new CameraSetupPacket();
        cameraSetup.Id = Id;
        cameraSetup.Position = Camera.main.transform.position;
        cameraSetup.Rotation = Camera.main.transform.rotation;
        NetDataWriter cameraTransData = new NetDataWriter();
        cameraTransData.Put((int)PacketType.CameraSetup);
        cameraSetup.Serialize(cameraTransData);
        netPeer.Send(cameraTransData, DeliveryMethod.ReliableOrdered);
    }

    //protected void SendInputToServer(InputPacket packet)
    protected void SendInputToServer(InputPacket inputPacket)
    {
        if (netPeer == null)
        {
            Debug.Log(" Not connected Yet ");
            return;
        }
        NetDataWriter inputData = new NetDataWriter();
        inputPacket.Id = Id;
        inputData.Put((int)PacketType.Movement);
        inputPacket.Serialize(inputData);
        netPeer.Send(inputData, DeliveryMethod.ReliableOrdered);
    }

    public void AddPlayerView(PlayerViewClient playerViewClient)
    {
        playerViewClients.Add(playerViewClient.Id, playerViewClient);
    }

    public void RequestRPC(int id, string methodName, RPCTarget rpcTarget, params object[] parameters)
    {
       if (playerViewClients.ContainsKey(id))
        {
            SendRPCToServer(methodName, rpcTarget, parameters);
        }
    }

    private void SendRPCToServer(string methodName, RPCTarget rpcTarget, params object[] parameters)
    {
        NetDataWriter rpcData = new NetDataWriter();
        RPCPacket rPCPacket;

        rPCPacket = new RPCPacket();
        rpcData.Put((int)PacketType.RPC);
        rPCPacket.Serialize(rpcData);
        netPeer.Send(rpcData, DeliveryMethod.ReliableOrdered);
        Debug.Log("I sent some RPC to the server");
    }

    void ReceiveRPCFromServer( string methodName, int idOfSender,params object[] parameters)
    {
        //idOfSender : is the peer id , received in the server 
        Debug.Log("I received RPC from the server");
        playerViewClients[idOfSender].ReceiveRPC(methodName, parameters);
    }
    #endregion
}
