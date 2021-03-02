using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;


public class RemotePlayer
{
    public int Id;
    public GameObject go;
    public Vector3 []position_buff;

    public RemotePlayer(int id, GameObject _go)
    {
        Id = id;
        go = _go;
        go.GetComponent<Player>().ID = id;
        go.GetComponent<Player>().Id = id;
        //Disable the camera of the enemy
        go.transform.GetChild(0).gameObject.GetComponent<Camera>().gameObject.SetActive(false);
        go.GetComponent<Player>().IsLocalPlayer = false;
    }
}

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
    const string CONNECTION_KEY = "KEYOFCONNCETION";

    [SerializeField]
    GameObject playerPrefab;

    List<RemotePlayer> _remotePlayers;
    GameObject _localPlayer;

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
    bool clientSidePrediction;
    [SerializeField]
    bool serverReconciliation;
    [SerializeField]
    bool interpolation;

    int _inputSeqNumber;

    const string SERVER_URL = "localhost";
    const int PORT = 9050;
    const int MAX_LENGTH = 500;
    const int CLIENT_SLEEP_TIME = 15;
    const float GAME_FPS = 50f;

    

    bool istantiated = false;
    bool isConnected = false;

    Coroutine customUpdateCoroutine;

    // Start is called before the first frame update
   new void Start()
    {
        Id = -1;
        base.Start();
        StartClient();
        _localPlayer = null;
        _remotePlayers = new List<RemotePlayer>();
        isConnected = false;
        clientSidePrediction = false;
        serverReconciliation = false;
        interpolation = false;
        _inputSeqNumber = 0;
        _pendingNInputs = new List<PendingInput>();
        _previousTime = Time.time;
    }

    private void OnEnable()
    {
        customUpdateCoroutine = StartCoroutine(CustomUpdate(GAME_FPS));
    }

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
            yield return wait;
        }
    }

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


    public void PlayersInstanciation(int id, Vector3 position, Quaternion rotation, Color color, bool isLocalPlayer)
        {
        if (isLocalPlayer)
        {
            if (_localPlayer) { return; }
        }
        RemotePlayer foundPlayer = _remotePlayers.Find(player => player.Id == id);
        if (foundPlayer == null)
        {
            GameObject go = Instantiate(playerPrefab, position, rotation);
            go.transform.GetChild(1).GetComponent<MeshRenderer>().material.color = color;
            if (isLocalPlayer)
            {

                if (istantiated)
                {
                    if (_localPlayer == null) { Debug.Log("Yopu found me"); }
                }
                Debug.LogFormat("Instantiation of Self {0} instanciated : {1}", _localPlayer != null, istantiated);
                if (_localPlayer != null) { Debug.Log("Not normal");

                }
                else
                {
                    istantiated = true;
                }
                _localPlayer = go;
                _localPlayer.GetComponent<Player>().ID = id;
                _localPlayer.GetComponent<Player>().Id = id;
                _localPlayer.GetComponent<Player>().IsLocalPlayer = true;
            }
            else
            {
                _remotePlayers.Add(new RemotePlayer(id, go));
                Debug.LogFormat("Instantiation of an enemy Position : ({0}, {0}, {0}) ", go.transform.position.x, go.transform.position.y, go.transform.position.z);
            }

        }
    }

    void MoveLocalplayer(Tools.NInput nInput, float fpsRate)
    {
        if (!_localPlayer) { return; }
        _localPlayer.GetComponent<PlayerController>().ApplyInput(nInput, fpsRate);
    }

    //Note the input is not smooth....
    bool UpdateInput()
    {
        float deltaInputX = Mathf.Abs(currentNInput.inputX - previousNInput.inputX);
        float deltaInputY = Mathf.Abs(currentNInput.inputY - previousNInput.inputY);
        float deltaMouseX = 0; //Mathf.Abs(currentNInput.mouseX - previousNInput.mouseX);
        float deltaMouseY = 0;// Mathf.Abs(currentNInput.mouseY - previousNInput.mouseY);
        float epsilon = 1e-3f;

        return (
            currentNInput.jump || deltaInputX > epsilon || deltaInputY > epsilon
            || deltaMouseX > epsilon || deltaMouseY > epsilon || Mathf.Abs(currentNInput.inputX) > epsilon
            || Mathf.Abs(currentNInput.inputY) > epsilon
            );
    }

   
    void processInputs()
    {
      //  float currentTime = Time.realtimeSinceStartup; //time;
        float currentTime = Time.time;
        float dt_sec = currentTime - _previousTime;

        if (!UpdateInput()) { return; } //Nothing to process
        PendingInput pendingInput;

        pendingInput = new PendingInput(currentNInput, dt_sec, _inputSeqNumber++);
        //Send the input to the server here
        SendInputToServer(pendingInput);
        {
            //If there is client side prediction enabled do it
            if (clientSidePrediction)
            {
                MoveLocalplayer(currentNInput, dt_sec); //may be Time.deltaTime should do as well ???
            }
        }
        _pendingNInputs.Add(pendingInput);
        _previousTime = currentTime;
        previousNInput = currentNInput;
    }
    List<PlayerViewClient>Listofplayerviews;

    foreach(PlayerViewClient P in Listofplayerviews)
        {
        if (idofrpc == P.id)
        P.ReceiveRPC(params);
}
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
                    int peerId = reader.GetInt();
                    {
                        Vector3 position;
                        Quaternion rotation;
                        Color color =  new Color();

                        Debug.Log($"Spawn : {peerId}");

                        position.x = reader.GetFloat();
                        position.y = reader.GetFloat();
                        position.z = reader.GetFloat();
                        rotation.x = reader.GetFloat();
                        rotation.y = reader.GetFloat();
                        rotation.z = reader.GetFloat();
                        rotation.w = reader.GetFloat();

                        color.r = reader.GetFloat();
                        color.g = reader.GetFloat();
                        color.b = reader.GetFloat();
                        PlayersInstanciation(peerId, position, rotation, color, Id == peerId);
                    }
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
                    if (serverState.peerId == Id) //Server Reconciliation
                    {
                        ServerAuthoritativeState(serverState);
                        ServerReconciliation(serverState);
                    }
                    else
                    {
                        RemotePlayersMove(serverState);
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

    void ServerAuthoritativeState(ServerState serverState)
    {
        //Set server Authoritative Position and rotation
        /*  transform.position = serverState.position;
          transform.rotation = serverState.rotation;*/
        /* _localPlayer.transform.position = serverState.position;
         _localPlayer.transform.rotation = serverState.rotation;*/
       // Debug.Log($"Authoritative State --> Position : (x : {serverState.position.x} y : {serverState.position.y} z : {serverState.position.z})");
       _localPlayer.GetComponent<PlayerController>().ApplyTransform(serverState.position, serverState.rotation);
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
                    Debug.Log($"NOT PROCESSED BY THE SERVER {pendingInput.sequenceNumber} last processed by the server : ({serverState.lastProcessedInput})");
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

    void RemotePlayersMove(ServerState serverState)
    {
        RemotePlayer foundPlayer = _remotePlayers.Find(player => player.Id == serverState.peerId);
        if (foundPlayer == null) { Debug.Log("Should never happen !"); return; }
        if (!interpolation)
        {
            foundPlayer.go.transform.position = serverState.position;
            foundPlayer.go.transform.rotation = serverState.rotation;
        }
        else
        {
            //Add the state to the positions buffer
            float timestamp = Time.time;
            //foundPlayer.position_buff.add({ timestamp,  position, rotation })
        }
       
    }

    void EntitiesInterpolation()
    {
        //Interpolation for remote clients
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
        currentNInput.inputX = Input.GetAxis("Horizontal");
        currentNInput.inputY = Input.GetAxis("Vertical");
        currentNInput.jump = Input.GetButtonDown("Jump");
        currentNInput.mouseX = Input.GetAxis("Mouse X");
        currentNInput.mouseY = Input.GetAxis("Mouse Y");
        //Never do this but :-)
        clientSidePrediction = ClientUIController.Instance.clientSidePrediction;
        serverReconciliation = ClientUIController.Instance.serverReconcilation;
        interpolation = ClientUIController.Instance.lagCompensation;
    }

    void OthersRemove(int id)
    {
        RemotePlayer toDelete = _remotePlayers.Find(player => player.Id == id);
        Destroy(toDelete.go);
        _remotePlayers.Remove(toDelete);
    }

    

    void ReceiveInputFromServer()
    {
        //Debug.LogFormat(" id : {0} ", dataReader.GetInt());
        //Debug.LogFormat(" Analog 1 : {0} ", dataReader.GetFloat());
        //Debug.LogFormat(" Analog 2 : {0} ", dataReader.GetFloat());
        //dataReader.Recycle();
    }

  
}
