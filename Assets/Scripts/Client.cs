using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System;

public struct PendingInput
{
    public Tools.NInput nInput;
    public float nTime; //When did we set that input (received it);
    public int sequenceNumber;

    public PendingInput(Tools.NInput _nInput, float _nTime, int seqNumber)
    {
        this.nInput = _nInput;
        this.nTime = _nTime;
        this.sequenceNumber = seqNumber;
    }
}

public struct ServerState
{
    public int peerId;
    public Vector3 position;
    public Quaternion rotation;
    public int lastProcessedInput;
};

public class Client : NetworkManagerClient
{
    public static Client Instance;
    const string CONNECTION_KEY = "KEYOFCONNCETION";
    [SerializeField]
    GameObject playerGO= default;

    List<PlayerViewClient> _remotePlayers;
    PlayerViewClient _localPlayer;

    private int Id;
    private Tools.NInput currentNInput;
    private Tools.NInput previousNInput;
    //List of Pending Input
    List<PendingInput> _pendingNInputs;
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

    int _inputSeqNumber;

    const string SERVER_URL = "localhost";
    const int PORT = 9050;
    const int MAX_LENGTH = 500;
    const int CLIENT_SLEEP_TIME = 15;
    const float GAME_FPS = 50f;

    bool istantiated = false;
    bool isConnected = false;

    Coroutine customUpdateCoroutine;

    //List of the Player Views Here
    public Dictionary<int,  PlayerViewClient> playerViewClients;
    List<ServerState> _serverStates;

    //Lag compensation
    StateBuffer latestState;
    StateBuffer stateAtLastPacket;
    float currentTime = 0f;
    double currentPacketTime = 0f;
    double lastPacketTime;

    //Values that will be synced over network
    Vector3 latestPos;
    Quaternion latestRot;
    //Lag compensation
   // float currentTime = 0;
    //double currentPacketTime = 0;
  //  double lastPacketTime = 0;
    Vector3 positionAtLastPacket = Vector3.zero;
    Quaternion rotationAtLastPacket = Quaternion.identity;

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
        _inputSeqNumber = 0;
        _pendingNInputs = new List<PendingInput>();
        _previousTime = Time.time;
        playerViewClients = new Dictionary<int, PlayerViewClient>();
        _serverStates = new List<ServerState>();
       //Should be OnEnable
       customUpdateCoroutine = StartCoroutine(CustomUpdate(GAME_FPS));
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
        base.Connect(CONNECTION_KEY);
    }


    IEnumerator CustomUpdate(float fpsRate)
    {
        WaitForSeconds wait = new WaitForSeconds(1.0f / fpsRate);

        while (true)
        {
            processInputs();
            processServerState();
            yield return wait;
        }
    }

    void processServerState()
    {
        _serverStates.ForEach(serverState =>
        {
            if (serverState.peerId == Id) //Server Reconciliation
            {
                ServerAuthoritativeState(serverState);
                ServerReconciliation(serverState);
            }
            else
            {
                RemotePlayersMove(serverState);
            }
        });
        _serverStates.Clear();
    }

    #region LiteNetLib overloads
    private void OnDisable()
    {
        StopCoroutine(customUpdateCoroutine);
        customUpdateCoroutine = null;
    }

    public void DiconnectToServer()
    {
        ClientUIController.Instance.onClientConnected.text = "Disconnected";
        base.Connect(CONNECTION_KEY);
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        ClientUIController.Instance.onClientConnected.text = $"Disconned from the Server";
    }

    public override void  OnPeerConnected(NetPeer peer)
    {
        ClientUIController.Instance.OnEnableToogle();
        Debug.Log(" You are connected is it to the Server ?? "+ peer.Id);
    }
    #endregion

    #region Spawning and Instantiation of players
    void SpawnPlayer(SpawnPacket spawnPacket)
    {
        //yield return new WaitForSeconds(1f);
        PlayersInstanciation(spawnPacket.PlayerId, spawnPacket.Position, spawnPacket.Rotation, spawnPacket.Albedo, spawnPacket.PlayerId == Id);
    }

    public void PlayersInstanciation(int id, Vector3 position, Quaternion rotation, Color color, bool isLocalPlayer)
    {

        if (isLocalPlayer)
        {

            if (_localPlayer)
            {
                return; }
        }
       PlayerViewClient foundPlayer = _remotePlayers.Find(player => player.Id == id);

        if (foundPlayer == null)
        {
            if (isLocalPlayer)
            {
                if (istantiated)
                {
                    if (_localPlayer == null) { Debug.Log("Yopu found me"); }
                }
                if (_localPlayer != null) { Debug.Log("Not normal");}
                else
                {istantiated = true;}
                // _localPlayer = new ClientPlayer(id, true); Not working I think it makes no sence to call a constructor to initilize a prefab in another class !!!
                GameObject go = Instantiate(playerGO);
                if (!go.GetComponent<PlayerViewClient>())
                {
                    _localPlayer = go.AddComponent<PlayerViewClient>();
                }
                _localPlayer.Spawn(isLocalPlayer, id, position, rotation, color);
                //Then run something to construct the go on the client player
                if (_localPlayer != null) { Debug.Log("Local Player is Created"); }
                else { Debug.Log("The Local could not be created"); }
            }
            else
            {
                GameObject go = Instantiate(playerGO);
                PlayerViewClient remotePlayer = go.AddComponent<PlayerViewClient>();
                remotePlayer.Spawn(isLocalPlayer, id, position, rotation, color);
                _remotePlayers.Add(remotePlayer);
            }
        }
        
    }
    #endregion
    #region Inputs Of The Clients
    //Note the input is not smooth....
    // bool UpdateInput() => currentNInput != previousNInput;

    bool UpdateInput()
    {
        float deltaInputX = Mathf.Abs(currentNInput.InputX - previousNInput.InputX);
        float deltaInputY = Mathf.Abs(currentNInput.InputY - previousNInput.InputY);
        float deltaMouseX = Mathf.Abs(currentNInput.MouseX - previousNInput.MouseX);
        float deltaMouseY = Mathf.Abs(currentNInput.MouseY - previousNInput.MouseY);
        float epsilon = 0f;// 1e-3f;

        return (
            currentNInput.Jump || deltaInputX > epsilon || deltaInputY > epsilon
            || deltaMouseX > epsilon || deltaMouseY > epsilon || Mathf.Abs(currentNInput.InputX) > epsilon
            || Mathf.Abs(currentNInput.InputY) > epsilon
            );
    }
    public void setPlayerInputs(Tools.NInput nInput)
    {
        currentNInput = nInput;
    }

    bool _needUpdate = false;

    void processInputs()
    {
      //  float currentTime = Time.realtimeSinceStartup; //time;
        float currentTime = Time.time;
        float dt_sec = currentTime - _previousTime;

        //!!!!!!!_pendingNInputs.Count == 0
        _needUpdate = UpdateInput();
        if (! _needUpdate && _pendingNInputs.Count == 0) { return; } //Nothing to process

        if (!_needUpdate && _pendingNInputs.Count > 0)
        {
            Debug.Log("Check This Case Please ");
            return;
        }
        PendingInput pendingInput;

        pendingInput = new PendingInput(currentNInput, dt_sec, _inputSeqNumber++);
        //Send the input to the server here
        SendInputToServer(pendingInput);
        //If there is client side prediction enabled do it
        if (clientSidePrediction)
        {
            MoveLocalplayer(currentNInput, dt_sec); //may be Time.deltaTime should do as well ???
        }
        _pendingNInputs.Add(pendingInput);
        _previousTime = currentTime;
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
                    if (!isConnected){
                        isConnected = true;
                        netPeer = peer;
                        Id = peerId;
                        ClientUIController.Instance.onClientConnected.text = $"connected to the server with : {peerId}";
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
                    SpawnPacket spawnPacket =  new SpawnPacket();
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
                    ServerState serverState =  new ServerState();

                    serverState.peerId = reader.GetInt();
                    serverState.lastProcessedInput = reader.GetInt();
                    serverState.position.x = reader.GetFloat();
                    serverState.position.y = reader.GetFloat();
                    serverState.position.z = reader.GetFloat();
                    serverState.rotation.x = reader.GetFloat();
                    serverState.rotation.y = reader.GetFloat();
                    serverState.rotation.z = reader.GetFloat();
                    serverState.rotation.w = reader.GetFloat();
                    OnServerStatePacketReceive(serverState);
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
        //Set server Authoritative Position and rotation
        /*  transform.position = serverState.position;
          transform.rotation = serverState.rotation;*/
        /* _localPlayer.transform.position = serverState.position;
         _localPlayer.transform.rotation = serverState.rotation;*/
        if (_localPlayer) { _localPlayer.SetTransform(serverState.position, serverState.rotation); }
    }

    void ServerReconciliation(ServerState serverState)
    {

        if (serverReconciliation)
        {
            List<PendingInput> toRemove = new List<PendingInput>();
            _pendingNInputs.ForEach(pendingInput =>
            {
                if (pendingInput.sequenceNumber <= serverState.lastProcessedInput)
                {
                    toRemove.Add(pendingInput);
                    // _pendingNInputs.Remove(pendingInput);
                }
                else
                {
                    //Debug.Log($"NOT PROCESSED BY THE SERVER {pendingInput.sequenceNumber} last processed by the server : ({serverState.lastProcessedInput})");
                    //Apply the input
                    MoveLocalplayer(pendingInput.nInput, pendingInput.nTime);
                }
            });
            toRemove.ForEach(pendingInput =>
            {
                _pendingNInputs.Remove(pendingInput);
            });
            toRemove.Clear();
        }
        else
        {
            //Drop all recorded inputs
            _pendingNInputs.Clear();
        }
    }
    #endregion

    void OnServerStatePacketReceive(ServerState serverState)
    {
        if (serverState.peerId != Id)
        {
            if (_remotePlayers.Count < 1) { return; }
            PlayerViewClient foundPlayer = _remotePlayers.Find(player => player.Id == serverState.peerId);
            if (foundPlayer == null) { Debug.Log($"Should never happen ! Remote Players Number : {_remotePlayers.Count}"); return; }
            {
                //foundPlayer.SetTransform(serverState.position, serverState.rotation);
                //Add the state to the positions buffer
                float timestamp = Time.time;
                StateBuffer stateBuffer = new StateBuffer();
                stateBuffer.Position = serverState.position;
                stateBuffer.Rotation = serverState.rotation;
                stateBuffer.Timestamp = timestamp;

                latestPos = serverState.position;
                latestRot = serverState.rotation;

                currentTime = 0f;
                lastPacketTime = currentPacketTime;
                currentPacketTime = Time.time;//Noramlly info.SentServerTime (meaning time when it was sent to the server or client ?)
                stateAtLastPacket = new StateBuffer();
                stateAtLastPacket.Position = foundPlayer.GetComponent<PlayerController>().transform.position;
                stateAtLastPacket.Rotation = foundPlayer.GetComponent<PlayerController>().transform.rotation;

                positionAtLastPacket = stateAtLastPacket.Position;
                rotationAtLastPacket = stateAtLastPacket.Rotation;

               /* positionAtLastPacket = transform.position;
                rotationAtLastPacket = transform.rotation;*/
                //foundPlayer.position_buff.add({ timestamp,  position, rotation })
            }
        }
        _serverStates.Add(serverState);
    }

    void MoveLocalplayer(Tools.NInput nInput, float fpsRate)
    {
        if (!_localPlayer) { return; }
        _localPlayer.HandleInput(nInput, fpsRate);
    }

    void RemotePlayersMove(ServerState serverState)
    {
        PlayerViewClient foundPlayer = _remotePlayers.Find(player => player.Id == serverState.peerId);
        if (!foundPlayer)
        {
            return;
        }
      if (!interpolation)
        {
           foundPlayer.SetTransform(serverState.position, serverState.rotation);
            Debug.Log("Lag Compensation is off");
        }
        else
        {
            Debug.Log("Lag Compensation is on");
           EntityInterpolation(foundPlayer);
        }
    }

    void EntityInterpolation(PlayerViewClient remotePlayer)
    {
        double timeToReachGoal = currentPacketTime - lastPacketTime;
        currentTime += Time.deltaTime;
        remotePlayer.GetComponent<PlayerController>().transform.position = Vector3.Lerp(positionAtLastPacket, latestPos, (float)(currentTime / timeToReachGoal));
        remotePlayer.GetComponent<PlayerController>().transform.rotation = Quaternion.Lerp(rotationAtLastPacket, latestRot, (float)(currentTime / timeToReachGoal));
    }
    void SelfRemove()
    {
        Destroy(_localPlayer);
       // Destroy(this.gameObject);
    }
    //Won't allow update from inherited
    new private void Update()
    {
        base.Update();
        if (!isConnected){ return; }
       
        //Never do this but :-)
        clientSidePrediction = ClientUIController.Instance.clientSidePrediction;
        serverReconciliation = ClientUIController.Instance.serverReconcilation;
        interpolation = ClientUIController.Instance.lagCompensation;
    }

    void OthersRemove(int id)
    {
        PlayerViewClient toDelete = _remotePlayers.Find(player => player.Id == id);
        if (toDelete != null)
        {
            Debug.Log("Removed");
        }
        else
        {
            Debug.Log("Not removed");
        }
        _remotePlayers.Remove(toDelete);
        Destroy(toDelete);
        toDelete = null;
        playerViewClients.Remove(id);
    }
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
    //protected void SendInputToServer(InputPacket packet)
    protected void SendInputToServer(PendingInput pendingInput)
    {
        if (netPeer == null)
        { return; }
        // NetDataWriter netData = inputPacket.
        NetDataWriter inputData = new NetDataWriter();
        inputData.Put((int)PacketType.Movement);
        inputData.Put(Id);
        inputData.Put(pendingInput.sequenceNumber);
        inputData.Put(pendingInput.nTime);
        inputData.Put(pendingInput.nInput.InputX);
        inputData.Put(pendingInput.nInput.InputY);
        inputData.Put(pendingInput.nInput.Jump);
        inputData.Put(pendingInput.nInput.MouseX);
        inputData.Put(pendingInput.nInput.MouseY);
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
