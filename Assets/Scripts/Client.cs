using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System;

public class PendingInput
{
    public Tools.NInput nInput;
    public float nTime; //When did we set that input (received it);
    public int sequenceNumber;
    public bool processed;


    public PendingInput()
    {
        this.sequenceNumber = -1;
        this.processed = false;
    }

    public PendingInput(Tools.NInput _nInput, float _nTime, int seqNumber)
    {
        this.nInput = _nInput;
        this.nTime = _nTime;
        this.sequenceNumber = seqNumber;
        this.processed = false;
    }
}

public class ClientInputState
{
    public Tools.NInput nInput;
    public float nTime; //When did we set that input (received it);
    public int simulationFrame;

    public ClientInputState()
    {
        this.simulationFrame = -1;
    }

    public ClientInputState(Tools.NInput _nInput, float _nTime, int seqNumber)
    {
        this.nInput = _nInput;
        this.nTime = _nTime;
        this.simulationFrame = seqNumber;
    }
}

public class SimulationState
{
    public int peerId;
    public int simulationFrame;
    public float animSpeed;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 camPosition;
    public Quaternion camRotation;

    public SimulationState()
    {
        peerId = 0;
        simulationFrame = 0;
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
        simulationFrame = playerStatePacket.lastProcessedInput;
        camPosition = playerStatePacket.camPosition;
        camRotation = playerStatePacket.camRotation;
    }

    public override string ToString()
    {
        return ($" Id {peerId} \n" +
            $" Pos({position.x}, {position.y}, {position.z}) \n" +
            $": Rot({rotation.x}, {rotation.y}, {rotation.z}, {rotation.w}) \n" +
            $" : Last Processed + {simulationFrame} " +
            $" : AnimSpeed : {animSpeed} ");
    }
}

public class ServerState
{
    public bool processed = false;
    public int peerId;
    public int lastProcessedInput;
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
        lastProcessedInput = playerStatePacket.lastProcessedInput;
        camPosition = playerStatePacket.camPosition;
        camRotation = playerStatePacket.camRotation;
        processed = false;
    }

    public override string ToString()
    {
        return ($" Id {peerId} \n" +
            $" Pos({position.x}, {position.y}, {position.z}) \n" +
            $": Rot({rotation.x}, {rotation.y}, {rotation.z}, {rotation.w}) \n" +
            $" : Last Processed + {lastProcessedInput} " +
            $" : AnimSpeed : {animSpeed} "); 
    }
}
public class Client : NetworkManagerClient
{
    public static Client Instance;
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

    bool isConnected = false;

    Coroutine customUpdateCoroutine;

    //List of the Player Views Here
    public Dictionary<int,  PlayerViewClient> playerViewClients;
    List<ServerState> _serverStates;
    List<ServerState> _clientStates;

    //Client Side Prediction
    private static ClientInputState defaultInputState = new ClientInputState();

    // The maximum cache size for both the ClientInputState 
    private const int STATE_CACHE_SIZE = 256;

    // The cache that stores all of the client's predicted movement reuslts. 
    private SimulationState[] simulationStateCache = new SimulationState[STATE_CACHE_SIZE];

    // The cache that stores all of the client's inputs. 
    private ClientInputState[] inputStateCache = new ClientInputState[STATE_CACHE_SIZE];

    // The last known SimulationState provided by the server. 
    private SimulationState serverSimulationState;
    // The client's current ClientInputState. 
    private ClientInputState inputState;

    // The client's current simulation frame. 
    private int simulationFrame;

    // The last simulationFrame that we Reconciled from the server. 
    private int lastCorrectedFrame;

    //Lag compensation
    float currentTime = 0f;


    private CustomFixedUpdate FU_instance;

    private  Dictionary<int, GameObject> _clients;

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
        return;
        if (!_localPlayer || !_localPlayer.isReady) { return; }
        processInputs2();
        processServerState2();
        EntitiesInterpolation();
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
       // FU_instance = new CustomFixedUpdate(1.0f / AuthServer.GAME_FPS, OnFixedUpdate);
        _clients = new Dictionary<int, GameObject>();
        //Should be OnEnable
        // customUpdateCoroutine = StartCoroutine(CustomUpdate(AuthServer.GAME_FPS));
        simulationFrame = 0;
        lastCorrectedFrame = 0;
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

   
    private void FixedUpdate2()
    {
  
        if (!_localPlayer || !_localPlayer.isReady) { return; }

        // processInputs2();
        //processServerState2();

        //processInputs();
        //processServerState();
        // EntitiesInterpolation();
    }
    

    IEnumerator CustomUpdate(float fpsRate) 
    {
       // WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1.0f / fpsRate);
        WaitForSecondsRealtime wait = new WaitForSecondsRealtime(1.0f);
        while (true)
        {
            if (!_localPlayer || !_localPlayer.isReady) { yield return wait; }
            processInputs();
            processServerState();
            EntitiesInterpolation();
            yield return wait;
        }
    }

    void processServerState()
    {
        List<ServerState> toRemove;

        //may be store and remove the applied states ??
        toRemove = new List<ServerState>();
       // Debug.Log($"Before Server State Cunt : { _serverStates.Count}");

        _serverStates.ForEach(serverState =>
        {
            if (serverState.peerId == Id  && !serverState.processed) //Server Reconciliation
            {
                ServerAuthoritativeState(serverState);
                if (serverState.processed)
                {
                   ServerReconciliation(serverState);
                    toRemove.Add(serverState);
                }
            }
        });
        toRemove.ForEach(serverState =>
       {
           if (serverState.processed) { _serverStates.Remove(serverState); }
       });
        toRemove.Clear();
       // Debug.Log($"After Server State Cunt : { _serverStates.Count}");
    }
    //Won't allow update from inherited

    new private void Update()
    {

        base.Update();
        if (!isConnected) { return; }

//        FU_instance.Update();

        if (!_localPlayer) { return; }
        if (!UpdateInput()) { return; }

        inputState = new ClientInputState();
        inputState.nInput = currentNInput;
        //inputState.nTime = Time.deltaTime;
        //Never do this but :-)
        clientSidePrediction = ClientUIController.Instance.clientSidePrediction;
        serverReconciliation = ClientUIController.Instance.serverReconcilation;
        interpolation = ClientUIController.Instance.lagCompensation;
    }
    #region Another Attenpt
    void NetworkClientSend(ClientInputState inputState)
    {
        PendingInput pendingInput = new PendingInput();
        pendingInput.nInput = inputState.nInput;
        pendingInput.nTime = inputState.nTime;
        pendingInput.sequenceNumber = inputState.simulationFrame;
        SendInputToServer(pendingInput);
    }

    private void FixedUpdate()
    {
        if (inputState == null)
        {
            return;
        }
        else
        {
            Debug.Log("Applying State From Here");
        }
        inputState.nTime = Time.fixedDeltaTime;
        inputState.simulationFrame = simulationFrame;
        ProcessInputs(inputState);
        NetworkClientSend(inputState);
        if (serverSimulationState != null) Reconciliate();
        SimulationState simulationState = CurrentSimulationState(inputState);
        int cacheIndex = simulationFrame % STATE_CACHE_SIZE;
        simulationStateCache[cacheIndex] = simulationState;
        inputStateCache[cacheIndex] = inputState;
        ++simulationFrame;
        previousNInput = currentNInput;
    }

    public void ProcessInputs(ClientInputState state)
    {
        if (state == null)
        {
            state = defaultInputState;
        }
        _localPlayer.GetComponent<PlayerController>().ApplyInput(state.nInput, state.nTime);
    }


    private void Reconciliate()
    {
        if (serverSimulationState.simulationFrame <= lastCorrectedFrame) return;
        int cacheIndex = serverSimulationState.simulationFrame % STATE_CACHE_SIZE;
        ClientInputState cachedInputState = inputStateCache[cacheIndex];
        SimulationState cachedSimulationState = simulationStateCache[cacheIndex];
        if (cachedInputState == null || cachedSimulationState == null)
        {
            _localPlayer.GetComponent<PlayerController>().transform.position = serverSimulationState.position;
            lastCorrectedFrame = serverSimulationState.simulationFrame;
            return;
        }
        float positionErrordX = Mathf.Abs(cachedSimulationState.position.x -  serverSimulationState.position.x);
        float positionErrordY = Mathf.Abs(cachedSimulationState.position.y -  serverSimulationState.position.y);
        float positionErrordZ = Mathf.Abs(cachedSimulationState.position.z - serverSimulationState.position.z);
        float tolerance = 0F;

        if (positionErrordX > tolerance || positionErrordY > tolerance || positionErrordZ > tolerance)
        {
            _localPlayer.GetComponent<PlayerController>().transform.position = serverSimulationState.position;
            _localPlayer.GetComponent<PlayerController>().transform.rotation = serverSimulationState.rotation;
            int rewindFrame = serverSimulationState.simulationFrame;
            while (rewindFrame < simulationFrame)
            {
                int rewindCacheIndex = rewindFrame % STATE_CACHE_SIZE;
                ClientInputState rewindCachedInputState = inputStateCache[rewindCacheIndex];
                SimulationState rewindCachedSimulationState = simulationStateCache[rewindCacheIndex];
                if (rewindCachedInputState == null || rewindCachedSimulationState == null)
                {
                    ++rewindFrame;
                    continue;
                }
                ProcessInputs(rewindCachedInputState);
                SimulationState rewoundSimulationState = CurrentSimulationState(inputState); //Not Sure !!!!
                rewoundSimulationState.simulationFrame = rewindFrame;
                simulationStateCache[rewindCacheIndex] = rewoundSimulationState;
                ++rewindFrame;
            }
        }
        lastCorrectedFrame = serverSimulationState.simulationFrame;
    }

    public SimulationState CurrentSimulationState(ClientInputState inputState)
    {
        return new SimulationState
        {
            position = _localPlayer.GetComponent<PlayerController>().transform.position,
            rotation = _localPlayer.GetComponent<PlayerController>().transform.rotation,
            simulationFrame = inputState.simulationFrame
        };
    }
    #endregion
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

    public override void  OnPeerConnected(NetPeer peer)
    {
        ClientUIController.Instance.OnEnableToogle();
        Debug.Log(" You are connected is it to the Server ?? "+ peer.Id);
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
                    //Debug.Log("Local Player is Created");
                    //Debug.Log($"To At The Position({position}) To Rotation({rotation})");
                    //Debug.Log($"At The Position({_localPlayer.GetComponent<PlayerController>().transform.position}) Rotation({_localPlayer.GetComponent<PlayerController>().transform.rotation})");
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

    void processPendingInput(PendingInput pendingInput)
    {
        MoveLocalplayer(pendingInput.nInput, pendingInput.nTime);
    }

    void processInputs()
    {
        float currentTime = Time.timeSinceLevelLoad;
        float dt_sec = currentTime - _previousTime;
        //!!!!!!!_pendingNInputs.Count == 0
        //bool _needUpdate = UpdateInput();
        //if (! _needUpdate) { return; } //Nothing to process
        PendingInput pendingInput;

        pendingInput = new PendingInput(currentNInput, dt_sec, _inputSeqNumber);
        //Send the input to the server here
        SendInputToServer(pendingInput);
       
        //If there is client side prediction enabled do it
        //if (clientSidePrediction)
        {
            processPendingInput(pendingInput);
        }
        _pendingNInputs.Add(pendingInput);
        _inputSeqNumber++;
        _previousTime = currentTime;
        previousNInput = currentNInput;
    }
    #endregion
    #region Attempt of tick ! Works :-)
    public struct ClientState
    {
        public Vector3 position;
        public Quaternion rotation;
    };
    public struct ClientInput
    {
        public Tools.NInput inputs;
        public ClientInput(Tools.NInput input)
        {
            this.inputs = input;
        }
    };
    private ClientState[] client_state_buffer = new ClientState[1024];
    private ClientInput[] client_input_buffer = new ClientInput[1024];

    System.Random random = new System.Random();

    void processInputs2()
    {
        if (!UpdateInput())
            return;

        if (random.Next(10, 20) < 15)
            SendInputToServer(new PendingInput(currentNInput, Time.fixedDeltaTime, _inputSeqNumber));
        int buffer_slot = this._inputSeqNumber % 1024;
        client_input_buffer[buffer_slot].inputs = currentNInput;
        client_state_buffer[buffer_slot].position = _localPlayer.GetComponent<PlayerController>().transform.position;
        client_state_buffer[buffer_slot].rotation = _localPlayer.GetComponent<PlayerController>().transform.rotation;

        _localPlayer.GetComponent<PlayerController>().ApplyInput(currentNInput, Time.fixedDeltaTime);
        previousNInput = currentNInput;
        _inputSeqNumber++;
    }

    void MayBeProcessItDirectly(ServerState state)
    {
      
        //_localPlayer.GetComponent<PlayerController>().transform.position = state.position;
        //_localPlayer.GetComponent<PlayerController>().transform.rotation = state.rotation;
        //return;
        int buffer_slot = state.lastProcessedInput % 1024;
        if (buffer_slot < 0)
        {
            Debug.Log($"Requesting Buffer Slot {buffer_slot}");
            Debug.Log($"state : {state}");
            _localPlayer.GetComponent<PlayerController>().transform.position = state.position;
            _localPlayer.GetComponent<PlayerController>().transform.rotation = state.rotation;
            return;
        }
        Vector3 position_error = state.position - client_state_buffer[buffer_slot].position;
        if (position_error.sqrMagnitude > 1e-6f)
        {
            //Debug.Log($" Local Player Position is wrong but lastProcessed {state.lastProcessedInput} but current tick : {_inputSeqNumber}");
            _localPlayer.GetComponent<PlayerController>().transform.position = state.position;
            _localPlayer.GetComponent<PlayerController>().transform.rotation = state.rotation;
            int rewind_ticks = state.lastProcessedInput;
            while (rewind_ticks < _inputSeqNumber)
            {
                Debug.Log("Rewinding");
                buffer_slot = rewind_ticks % 1024;
                client_input_buffer[buffer_slot].inputs = currentNInput;
                client_state_buffer[buffer_slot].position = state.position;
                client_state_buffer[buffer_slot].rotation = state.rotation;
                _localPlayer.GetComponent<PlayerController>().ApplyInput(currentNInput, Time.fixedDeltaTime);
                rewind_ticks++;
            }
        }
    }

    void processServerState2()
    {
        _serverStates.ForEach(state =>
        {
            if (state.peerId == Id)
            {
                int buffer_slot = state.lastProcessedInput % 1024;
                if (buffer_slot < 0) {
                    Debug.Log($"Requesting Buffer Slot {buffer_slot}");
                    Debug.Log($"state : {state}");
                    return;
                }
                Vector3 position_error = state.position - client_state_buffer[buffer_slot].position;
                if (position_error.sqrMagnitude > 1e-6f)
                {
                    Debug.Log(" Local Player ");
                    _localPlayer.GetComponent<PlayerController>().transform.position = state.position;
                    _localPlayer.GetComponent<PlayerController>().transform.rotation = state.rotation;

                    int rewind_ticks = state.lastProcessedInput;
                    while (rewind_ticks < _inputSeqNumber)
                    {
                        Debug.Log("Rewinding");
                        buffer_slot = rewind_ticks % 1024;
                        client_input_buffer[buffer_slot].inputs = currentNInput;
                        client_state_buffer[buffer_slot].position = state.position;
                        client_state_buffer[buffer_slot].rotation = state.rotation;
                        _localPlayer.GetComponent<PlayerController>().ApplyInput(currentNInput, Time.fixedDeltaTime);
                        rewind_ticks++;
                    }
                }
            }
        });
        _serverStates.Clear();
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
                    PlayerStatePacket playerStatePacket = new PlayerStatePacket();
                    playerStatePacket.Deserialize(reader);
                    ServerState serverState =  new ServerState(playerStatePacket);
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
            _localPlayer.ApplyServerState(serverState.position, serverState.rotation, serverState.animSpeed, serverState.camPosition, serverState.camRotation);
            serverState.processed = true;
        }
        else { Debug.Log("But Why ???"); return; }
    }

   

    void ServerReconciliation(ServerState serverState)
    {
     //   if (serverReconciliation)
        {
            List<PendingInput> toRemove = new List<PendingInput>();
            _pendingNInputs.ForEach(pendingInput =>
            {
                if (pendingInput.sequenceNumber <= serverState.lastProcessedInput)
                {
                    toRemove.Add(pendingInput);
                    //_pendingNInputs.Remove(pendingInput);
                }
               else
                {
                    //Apply the input
                    {
                       processPendingInput(pendingInput);
                    }
                }

            });
            toRemove.ForEach(pendingInput =>
            {
                _pendingNInputs.Remove(pendingInput);
            });
            toRemove.Clear();
        }
      // else
        {
            //Drop all recorded inputs
         //   _pendingNInputs.Clear();
        }

    }
    #endregion
    #region OnServerStatePacket Receive for clients states
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
                StateBuffer stateBuffer = new StateBuffer();
                stateBuffer.Position = serverState.position;
                stateBuffer.Rotation = serverState.rotation;
                stateBuffer.CamPosition = serverState.camPosition;
                stateBuffer.CamRotation = serverState.camRotation;
                stateBuffer.AnimSpeed = serverState.animSpeed;
                stateBuffer.Timestamp = Time.timeSinceLevelLoad;

                //latestPos = serverState.position;
                //latestRot = serverState.rotation;

                //currentTime = 0f;
                //lastPacketTime = currentPacketTime;
                //currentPacketTime = Time.time;//Noramlly info.SentServerTime (meaning time when it was sent to the server or client ?)
                //stateAtLastPacket = new StateBuffer();
                //stateAtLastPacket.Position = foundPlayer.GetComponent<PlayerController>().transform.position;
                //stateAtLastPacket.Rotation = foundPlayer.GetComponent<PlayerController>().transform.rotation;

                //positionAtLastPacket = stateAtLastPacket.Position;
                //rotationAtLastPacket = stateAtLastPacket.Rotation;

                ///* positionAtLastPacket = transform.position;
                // rotationAtLastPacket = transform.rotation;*/
                ////foundPlayer.position_buff.add({ timestamp,  position, rotation })
                foundPlayer.StateBuffers.Add(stateBuffer);
            }
        }
        else {
            SimulationState message = new SimulationState();
            message.position = serverState.position;
            message.rotation = serverState.rotation;
            message.simulationFrame = serverState.lastProcessedInput;
            message.animSpeed = serverState.animSpeed;
            message.camPosition = serverState.camPosition;
            message.camRotation = serverState.camRotation;
            message.peerId = serverState.peerId;
            /*     Debug.Log($"Position : ({message.position.x}, {message.position.y}, {message.position.z})" +
                     $"Rotation({message.rotation.x}, {message.rotation.y}, {message.rotation.z}, {message.rotation.z})");*/
              OnServerSimulationStateReceived(message);
            //MayBeProcessItDirectly(serverState);
          //  _serverStates.Add(serverState);
        }
        
    }
    private void OnServerSimulationStateReceived(SimulationState message)
    {
        if (serverSimulationState == null)
        {
            serverSimulationState = new SimulationState();
            serverSimulationState.simulationFrame = -1;
        }
        // Only register newer SimulationState's. 
        if (serverSimulationState?.simulationFrame < message.simulationFrame)
        {
            serverSimulationState = message;
            _localPlayer.GetComponent<PlayerController>().transform.position = message.position;
            _localPlayer.GetComponent<PlayerController>().transform.rotation = message.rotation;
         //   Debug.Log("Registering New Server State");
        }
        else
        {
            if (serverSimulationState != null) { Debug.Log($"Received Server State " + message.simulationFrame + " current State : " + serverSimulationState.simulationFrame); }
        }
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
           foundPlayer.ApplyServerState(serverState.position, serverState.rotation, serverState.animSpeed, serverState.camPosition, serverState.camRotation);
            Debug.Log("Lag Compensation is off");
        }
        else
        {
            Debug.Log("Lag Compensation is on");
           EntityInterpolation(foundPlayer, 0f);
        }
    }

    void EntityInterpolation(PlayerViewClient remotePlayer, float render_timestamp)
    {
        Vector3 pos1 = remotePlayer.StateBuffers[0].Position;
        Vector3 pos2 = remotePlayer.StateBuffers[1].Position;
        Quaternion rot1 = remotePlayer.StateBuffers[0].Rotation;
        Quaternion rot2 = remotePlayer.StateBuffers[1].Rotation;
        Vector3 _camPos1 = remotePlayer.StateBuffers[0].CamPosition;
        Vector3 _camPos2 = remotePlayer.StateBuffers[1].CamPosition;
        Quaternion _camRot1 = remotePlayer.StateBuffers[0].CamRotation;
        Quaternion _camRot2 = remotePlayer.StateBuffers[1].CamRotation;
        float _animSpeed1 = remotePlayer.StateBuffers[0].AnimSpeed;
        float _animSpeed2 = remotePlayer.StateBuffers[1].AnimSpeed;

        float timeToReachGoal = remotePlayer.StateBuffers[1].Timestamp - remotePlayer.StateBuffers[0].Timestamp;
        currentTime = render_timestamp - remotePlayer.StateBuffers[0].Timestamp;
        float t__ = (currentTime / timeToReachGoal);

        Vector3 _newPos = Vector3.Lerp(pos1, pos2, t__);
        Quaternion _newRot = Quaternion.Lerp(rot1, rot2, t__);
        Vector3 _newCamPos = Vector3.Lerp(_camPos1, _camPos2, t__);
        Quaternion _newCamRot = Quaternion.Lerp(_camRot1, _camRot2, t__);
        float _newAnimSpeed = Mathf.Lerp(_animSpeed1, _animSpeed2, t__);
        remotePlayer.ApplyServerState(_newPos, _newRot, _newAnimSpeed, _newCamPos, _newCamRot);
    }

    void EntitiesInterpolation()
    {
        float now = Time.timeSinceLevelLoad;
        float render_timestamp = now - 1.0f / AuthServer.SERVER_UPDATE_RATE;
        _remotePlayers.ForEach(remotePlayer =>
        {
            while (remotePlayer.StateBuffers.Count >= 2 && (remotePlayer.StateBuffers[1].Timestamp <= render_timestamp))
            {
                remotePlayer.StateBuffers.RemoveAt(0);
            }
            if (remotePlayer.StateBuffers.Count >= 2 && (remotePlayer.StateBuffers[0].Timestamp <= render_timestamp && render_timestamp < remotePlayer.StateBuffers[1].Timestamp))
            {
                EntityInterpolation(remotePlayer, render_timestamp);
            }
        });
    }
    #endregion
    #region Remove Self or Client on disconnection
    void SelfRemove()
    {
        Destroy(_localPlayer.GetComponent<PlayerViewClient>());
        Destroy(_localPlayer);
        _clients.Remove(Id);
        // Destroy(this.gameObject);
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
    protected void SendInputToServer(PendingInput pendingInput)
    {
        if (netPeer == null)
        {
            Debug.Log(" Not connected Yet ");
            return;
        }
        // NetDataWriter netData = inputPacket.
        NetDataWriter inputData = new NetDataWriter();
        inputData.Put((int)PacketType.Movement);
        inputData.Put(Id);
        inputData.Put(pendingInput.sequenceNumber);
        inputData.Put(pendingInput.nTime);
        inputData.Put(pendingInput.nInput.InputX);
        inputData.Put(pendingInput.nInput.InputY);
        inputData.Put(pendingInput.nInput.Jump);
        inputData.Put(pendingInput.nInput.Run);
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
