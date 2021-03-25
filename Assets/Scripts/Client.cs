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
        this.sequenceNumber = -1000;
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

public class ServerState
{
    public int peerId;
    public Vector3 position;
    public Quaternion rotation;
    public int lastProcessedInput;
    public bool processed = false;
};

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

    bool istantiated = false;
    bool isConnected = false;

    Coroutine customUpdateCoroutine;

    //List of the Player Views Here
    public Dictionary<int,  PlayerViewClient> playerViewClients;
    List<ServerState> _serverStates;
    List<ServerState> _clientStates;

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
        _clientStates = new List<ServerState>();
       //Should be OnEnable
       customUpdateCoroutine = StartCoroutine(CustomUpdate(AuthServer.GAME_FPS));
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
        processInputs();
      //  processServerState();
    }
    
    IEnumerator CustomUpdate(float fpsRate)
    {
        WaitForSeconds wait = new WaitForSeconds(1.0f / fpsRate);

        while (true)
        {
           // processInputs();
            processServerState();
           // EntitiesInterpolation();
            yield return wait;
        }
    }

    void processServerState()
    {
        List<ServerState> toRemove;

        //may be store and remove the applied states ??
        toRemove = new List<ServerState>();

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
        Debug.Log($"Server State Cunt : { _serverStates.Count}");
    }
    //Won't allow update from inherited

    new private void Update()
    {

        base.Update();
        if (!isConnected) { return; }

        if (Input.GetKey(KeyCode.P))
        {
            displayMarked();
        }

        //Never do this but :-)
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
        float epsilon = 1e-6f; //0f;

        /*  return (
              currentNInput.Jump || deltaInputX > epsilon || deltaInputY > epsilon
              || deltaMouseX > epsilon || deltaMouseY > epsilon || Mathf.Abs(currentNInput.InputX) > epsilon
              || Mathf.Abs(currentNInput.InputY) > epsilon
              );
        */
        return currentNInput != previousNInput;
    }
    public void setPlayerInputs(Tools.NInput nInput)
    {
        currentNInput = nInput;
    }

    bool _needUpdate = false;


    List<int> markedSeq = new List<int>();

    void processPendingInput(PendingInput pendingInput, bool check)
    {
       /* if (check)
        {
            if (markedSeq.Exists(foundIndex => foundIndex == pendingInput.sequenceNumber))
            {
                Debug.Log("Already Processed Input");
                //return;
            }
        }else
        markedSeq.Add(pendingInput.sequenceNumber);*/
        MoveLocalplayer(pendingInput.nInput, pendingInput.nTime);
    }

    List<ServerState> _serverRec = new List<ServerState>();
    List<ServerState> _corespondingRec = new List<ServerState>();
    List<ServerState> _recordedRec = new List<ServerState>();
    void displayMarked()
    {
        /*  markedSeq.ForEach((index) =>
          {
              Debug.Log($" Client state : Position : ({_clientStates[index].position.x}, {_clientStates[index].position.y}, {_clientStates[index].position.z}) " +
                $" Rotation({_clientStates[index].rotation.x}, {_clientStates[index].rotation.y}, {_clientStates[index].rotation.z})");
          });
          _serverRec.ForEach((serverState) => {
              Debug.Log($" Applied state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.y}, {serverState.rotation.z}),");
          }
          );
          _corespondingRec.ForEach((serverState) => {
              Debug.Log($" Server state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.y}, {serverState.rotation.z}),");
          }
         );
          */
        _corespondingRec.ForEach((serverState) => {
            Debug.Log($" Client state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.y}, {serverState.rotation.z}),");
        }
        );
        _recordedRec.ForEach((serverState) => {
            Debug.Log($" Server state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.y}, {serverState.rotation.z}),");
        }
      );
    }

    void processInputs()
    {
        float currentTime = Time.timeSinceLevelLoad;
        float dt_sec = currentTime - _previousTime;

        if (!_localPlayer) { return; }
        /*
         *  This works but updating does'nt
         * */
       /* currentNInput.InputX = Input.GetAxis("Horizontal");
        currentNInput.InputY = Input.GetAxis("Vertical");
        currentNInput.Jump = Input.GetButtonDown("Jump");
        currentNInput.MouseX = Input.GetAxis("Mouse X");
        currentNInput.MouseY = Input.GetAxis("Mouse Y");
        */
        //!!!!!!!_pendingNInputs.Count == 0
        //_needUpdate = UpdateInput();
        //if (! _needUpdate) { return; } //Nothing to process
        PendingInput pendingInput;

        pendingInput = new PendingInput(currentNInput, dt_sec, _inputSeqNumber);
        //Send the input to the server here
        SendInputToServer(pendingInput);

        //If there is client side prediction enabled do it
        //if (clientSidePrediction)
        {
            //MoveLocalplayer(currentNInput, dt_sec);
            //Debug.Log($"Sent Input inputX : {pendingInput.nInput.InputX} inputY : {pendingInput.nInput.InputY} mouseX : {pendingInput.nInput.MouseX} mouseY {pendingInput.nInput.MouseY} nTime {pendingInput.nTime} ");
            processPendingInput(pendingInput, false);
            ServerState clientState = new ServerState();
            clientState.position = _localPlayer.GetComponent<PlayerController>().transform.position;
            clientState.rotation = _localPlayer.GetComponent<PlayerController>().transform.rotation;
            clientState.lastProcessedInput = _inputSeqNumber;
      //      Debug.Log($" New Rotation :({clientState.rotation.x}, {clientState.rotation.x}, {clientState.rotation.z}, {clientState.rotation.w})");
            _clientStates.Add(clientState);
        }
        _pendingNInputs.Add(pendingInput);
        _inputSeqNumber++;
        _previousTime = currentTime;
        previousNInput = currentNInput;
    }
    #endregion
    #region Attempt of tick !
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

    void processInputs2()
    {
        SendInputToServer(new PendingInput(currentNInput, 0.1f, _inputSeqNumber));
        int buffer_slot = this._inputSeqNumber % 1024;
        client_input_buffer[buffer_slot].inputs = currentNInput;
        client_state_buffer[buffer_slot].position = _localPlayer.GetComponent<PlayerController>().transform.position;
        client_state_buffer[buffer_slot].rotation = _localPlayer.GetComponent<PlayerController>().transform.rotation;

        _localPlayer.GetComponent<PlayerController>().ApplyInput(currentNInput, Time.deltaTime);

    }

    void processReceivedState()
    {
        _serverStates.ForEach(state =>
        {
            if (state.peerId == Id)
            {
                int buffer_slot = state.lastProcessedInput % 1024;
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
                        _localPlayer.GetComponent<PlayerController>().ApplyInput(currentNInput, Time.deltaTime);
                        rewind_ticks++;
                    }
                }
            }
        });
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

       /* Debug.Log($" server state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.x}, {serverState.rotation.z}),");
       
        Debug.Log($" current state : Position : ({_localPlayer.GetComponent<PlayerController>().transform.position.x}, {_localPlayer.GetComponent<PlayerController>().transform.position.y}, {_localPlayer.GetComponent<PlayerController>().transform.position.z})  " +
            $"Rotation({_localPlayer.GetComponent<PlayerController>().transform.rotation.x}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.y}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.z})");*/
        if (_localPlayer) {
            _localPlayer.SetTransform(serverState.position, serverState.rotation);
            serverState.processed = true;
            //if (serverState.lastProcessedInput >= 0 && _recordedRec.Find(state => state == serverState) == null)
            //{
            //    _recordedRec.Add(serverState);
            //    _corespondingRec.Add(_clientStates[serverState.lastProcessedInput]);
            //}
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
                    // _pendingNInputs.Remove(pendingInput);
                }
               else
                {
                    //Apply the input
                    {
                     //  Debug.Log($"NOT PROCESSED BY THE SERVER {pendingInput.sequenceNumber} last processed by the server : ({serverState.lastProcessedInput})");
                    //    Debug.Log($" server state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.y}, {serverState.rotation.z}, {serverState.rotation.w}),");
                      //  Debug.Log($" current state : Position : ({_localPlayer.GetComponent<PlayerController>().transform.position.x}, {_localPlayer.GetComponent<PlayerController>().transform.position.y}, {_localPlayer.GetComponent<PlayerController>().transform.position.z})  " +
                        //    $"Rotation({_localPlayer.GetComponent<PlayerController>().transform.rotation.x}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.y}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.z}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.w})");
                       processPendingInput(pendingInput, true);
                        ServerState _cState = new ServerState();
                        _cState.position = _localPlayer.GetComponent<PlayerController>().transform.position;
                        _cState.rotation = _localPlayer.GetComponent<PlayerController>().transform.rotation;
                        _cState.lastProcessedInput = pendingInput.sequenceNumber;
                        // Debug.Log($" New Rotation :({clientState.rotation.x}, {clientState.rotation.x}, {clientState.rotation.z}, {clientState.rotation.w})");
                        _serverRec.Add(_cState);
                        //Debug.Log($"Correct state {serverState.lastProcessedInput} lastInput :{_inputSeqNumber - 1} pending : {pendingInput.sequenceNumber}");
                    }
                }
               // if (pendingInput.sequenceNumber == serverState.lastProcessedInput)
               // {
               //     //     Debug.Log($" Last state : Position : ({_localPlayer.GetComponent<PlayerController>().transform.position.x}, {_localPlayer.GetComponent<PlayerController>().transform.position.y}, {_localPlayer.GetComponent<PlayerController>().transform.position.z})  Rotation({_localPlayer.GetComponent<PlayerController>().transform.rotation.x}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.y}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.z})");
               //     //    MoveLocalplayer(pendingInput.nInput, pendingInput.nTime);
               //   /*  _clientStates.ForEach(clientState =>
               //     {
               //         if (clientState.position == serverState.position)
               //         {
               //             Debug.Log($"The Corresponding Input was :  {clientState.lastProcessedInput} but the server  {serverState.lastProcessedInput}");
               //         }
               //     });*/
               ///*     Debug.Log($" server state : Position : ({serverState.position.x}, {serverState.position.y}, {serverState.position.z})  Rotation({serverState.rotation.x}, {serverState.rotation.x}, {serverState.rotation.z}),");
               //     Debug.Log($" recorded state : Position : ({_clientStates[pendingInput.sequenceNumber].position.x}, {_clientStates[pendingInput.sequenceNumber].position.y}, {_clientStates[pendingInput.sequenceNumber].position.z}) " +
               //         $" Rotation({_clientStates[pendingInput.sequenceNumber].rotation.x}, {_clientStates[pendingInput.sequenceNumber].rotation.y}, {_clientStates[pendingInput.sequenceNumber].rotation.z})");
               //     Debug.Log($" current state : Position : ({_localPlayer.GetComponent<PlayerController>().transform.position.x}, {_localPlayer.GetComponent<PlayerController>().transform.position.y}, {_localPlayer.GetComponent<PlayerController>().transform.position.z})  " +
               //         $"Rotation({_localPlayer.GetComponent<PlayerController>().transform.rotation.x}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.y}, {_localPlayer.GetComponent<PlayerController>().transform.rotation.z})");
               //     Debug.Log($"NOT PROCESSED BY THE SERVER {_inputSeqNumber - 1 - pendingInput.sequenceNumber} last processed by the server : ({serverState.lastProcessedInput})");
               //     */
               //     toRemove.Add(pendingInput);
               //   // _localPlayer.SetTransform(serverState.position, serverState.rotation);
               // }
            });
            toRemove.ForEach(pendingInput =>
            {
                _pendingNInputs.Remove(pendingInput);
            });
            toRemove.Clear();
        }
      //  else
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
                stateBuffer.Timestamp = Time.timeSinceLevelLoad;

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
                foundPlayer.StateBuffers.Add(stateBuffer);
            }
        }
        else {
            _serverStates.Add(serverState); }
        
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
                Vector3 pos1 = remotePlayer.StateBuffers[0].Position;
                Vector3 pos2 = remotePlayer.StateBuffers[1].Position;
                Quaternion rot1 = remotePlayer.StateBuffers[0].Rotation;
                Quaternion rot2 = remotePlayer.StateBuffers[1].Rotation;

                float timeToReachGoal = remotePlayer.StateBuffers[1].Timestamp - remotePlayer.StateBuffers[0].Timestamp;
                currentTime = render_timestamp - remotePlayer.StateBuffers[0].Timestamp;
                float t__ = render_timestamp - remotePlayer.StateBuffers[0].Timestamp;
                remotePlayer.GetComponent<PlayerController>().transform.position = Vector3.Lerp(pos1, pos2, (float)(currentTime / timeToReachGoal));
                remotePlayer.GetComponent<PlayerController>().transform.rotation = Quaternion.Lerp(rot1, rot2, (float)(currentTime / timeToReachGoal));
            }
        });
    }
    #endregion
    #region Remove Self or Client on disconnection
    void SelfRemove()
    {
        Destroy(_localPlayer);
       // Destroy(this.gameObject);
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
    //protected void SendInputToServer(InputPacket packet)
    int packetSent = 0;
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
       // inputData.Put(Id);
        inputData.Put(netPeer.Id);
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
