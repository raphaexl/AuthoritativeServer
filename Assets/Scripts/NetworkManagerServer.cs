using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;



public struct ServerRPCPacket
{
    public NetPeer netPeer;
    public RPCPacket rpcPacket;
}


public class ServerPlayer
{
    public int _id;
    public float Timestamp;
    public NetPeer _peer;
    public Transform _cameraTrans;
    public GameObject _maingameObject;
    public List<InputPacket> _clientInput;


    //Make the variables properties
    public ServerPlayer(int id, NetPeer peer, GameObject _gameObject, Transform cameraTrans)
    {
        _id = id;
        _peer = peer;
        _cameraTrans = cameraTrans;
        _maingameObject = _gameObject;
        Timestamp = -1;
        _clientInput = new List<InputPacket>();
        //_cameraTrans.SetParent(cameraTrans.gameObject.transform);
        _maingameObject.GetComponent<PlayerController>().cameraTrans = cameraTrans;
       // _maingameObject.transform.GetChild(0).gameObject.GetComponent<Camera>().gameObject.SetActive(false);
    }

    public void SetTransform(Vector3 pos, Quaternion rot)
    {
        _maingameObject.transform.position = pos;
        _maingameObject.transform.rotation = rot;
    }

   public void SetColor(Color color)
   {
        _maingameObject.transform.GetChild(1).GetComponent<Renderer>().material.color = color;
   }

    public override string ToString()
    {
        Vector3 _position = _maingameObject.GetComponent<PlayerController>().transform.position;
        Quaternion _rotation = _maingameObject.GetComponent<PlayerController>().transform.rotation;
        float _animSpeed = _maingameObject.GetComponent<PlayerController>().animSpeed;
        return ($" Id {_id} \n" +
            $" Pos({_position.x}, {_position.y}, {_position.z}) \n" +
            $": Rot({_rotation.x}, {_rotation.y}, {_rotation.z}, {_rotation.w}) \n" +
            $" : Color :-) + " +
            $" : AnimSpeed : {_animSpeed} ");
    }
}

public class NetworkManagerServer : MonoBehaviour, INetEventListener
{
    protected EventBasedNetListener netListener;
    protected NetManager _server;

    protected NetDataWriter writer;
    List<ServerPlayer> _serverPlayers;
    List<ServerRPCPacket> _rPCPackets;
    Dictionary<int, Transform> _clientsCameraTrans;
    [SerializeField]
    GameObject playerPrefab;

    private CustomFixedUpdate FU_instance;

    private Dictionary<ServerPlayer, Queue<ClientInputState>> clientInputs;


    private void Awake()
    {
       // FU_instance = new CustomFixedUpdate(1.0f / AuthServer.SERVER_UPDATE_RATE, OnFixedUpdate);
        // QualitySettings.vSyncCount = 0;
        // Application.targetFrameRate = (int)AuthServer.SERVER_UPDATE_RATE;
    }

    void Start()
    {
        _server = new NetManager(this);
        _server.AutoRecycle = true;
        writer = new NetDataWriter();
        _server.Start(AuthServer.SERVER_PORT);
        _serverPlayers = new List<ServerPlayer>();
        _rPCPackets = new List<ServerRPCPacket>();
        _clientsCameraTrans = new Dictionary<int, Transform>();
        FU_instance = new CustomFixedUpdate(1.0f / AuthServer.SERVER_UPDATE_RATE, OnFixedUpdate);
        clientInputs = new Dictionary<ServerPlayer, Queue<ClientInputState>>();
        Debug.Log($"Server Start at Port {AuthServer.SERVER_PORT}");
    }


    // this method will be called AuthServer.SERVER_UPDATE_RATE times per second
    void OnFixedUpdate(float dt)
    {
        foreach (KeyValuePair<ServerPlayer, Queue<ClientInputState>> entry in clientInputs)
        {
            ServerPlayer clientPlayer = entry.Key;
            Queue<ClientInputState> queue = entry.Value;

            ClientInputState inputState = null;
            while (queue.Count > 0 && (inputState = queue.Dequeue()) != null)
            {
                // Process the input.
                ProcessClientInputs(clientPlayer, inputState);
                SimulationState state = ClientCurrentSimulationState(clientPlayer, inputState);
                NetworkServerSendToClient(clientPlayer, state);
            }
        }
    }

    void sendRPCToClients()
    {
        _rPCPackets.ForEach(serverRPCPacket =>
       {
        //   if (peer.Id == rpcPacket.senderPeerId)
               ProcessClientRPC(serverRPCPacket.netPeer,
                   serverRPCPacket.rpcPacket.rpcTarget,
                   serverRPCPacket.rpcPacket.methodName,
                   serverRPCPacket.rpcPacket.parametersOrder,
                   serverRPCPacket.rpcPacket.parameters);
       });
        
    }

  
    private void Update()
    {
        _server.PollEvents();
        FU_instance.Update();
    }

    private void OnDestroy()
    {
        ServerPlayersListClear();
        _serverPlayers.Clear();
        _rPCPackets.Clear();
        _clientsCameraTrans.Clear();
        clientInputs.Clear();
    }

    const float Zmax = 2f;
    const float Zmin = -7f;
    const float Xmax = 3f;
    const float Xmin = -5f;

    Vector3 RandomPosition()
    {
        //Cause Random Position Seems to cause Problems
        return (new Vector3(Random.Range(Xmin, Xmax), 2.0f, Random.Range(Zmin, Zmax)));
      //  return (new Vector3(0, 0.0f, 0));
    }

    Color RandomColor()
    {
        return (new Color(Random.Range(0f, 1f), Random.Range(0, 1f), Random.Range(0, 1f), 1.0f));
    }

    void SendToAllClients(NetDataWriter netData)
    {
        _server.SendToAll(netData, DeliveryMethod.ReliableOrdered);
    }

    void SendToAllClientsExcept(NetDataWriter netData, NetPeer netPeer)
    {
        _server.SendToAll(netData, DeliveryMethod.ReliableOrdered, netPeer);
    }

    IEnumerator PlayerSpawnRoutine(NetPeer netPeer)
    {
        yield return new WaitForSeconds(0.5f);
        NetDataWriter joinedPacket = new NetDataWriter();
        joinedPacket.Put((int)PacketType.Join);
        joinedPacket.Put(netPeer.Id);
        SendToAllClients(joinedPacket);
        yield return new WaitForSeconds(0.5f);
        ServerPlayersListAdd(netPeer);
        _serverPlayers.ForEach(player =>
        {
            NetDataWriter _spawnwriter = new NetDataWriter();
            SpawnPacket spawnPacket = new SpawnPacket();

            _spawnwriter.Put((int)PacketType.Spawn);
            spawnPacket.PlayerId = player._id;
            spawnPacket.Position = player._maingameObject.GetComponent<PlayerController>().transform.position;
            spawnPacket.Rotation = player._maingameObject.GetComponent<PlayerController>().transform.rotation;
            spawnPacket.Albedo = player._maingameObject.transform.GetChild(1).GetComponent<Renderer>().material.color;
            spawnPacket.CameraPosition = player._cameraTrans.position;
            spawnPacket.CameraRotation = player._cameraTrans.rotation;
            spawnPacket.Serialize(_spawnwriter);
            SendToAllClients(_spawnwriter);
        //    Debug.Log($"At The Position({player._maingameObject.GetComponent<PlayerController>().transform.position}) Rotation({player._maingameObject.GetComponent<PlayerController>().transform.rotation})");
            player._maingameObject.GetComponent<PlayerController>().isReady = true;
        });
    }

    void SpawnPlayers(NetPeer peer)
    {
        StartCoroutine(PlayerSpawnRoutine(peer));
    }

    void ServerPlayersListAdd(NetPeer peer)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        if (serverPlayer != null) { Debug.LogError("Should never happen :::::::: !"); return; }

        Vector3 position = RandomPosition();
        Color color = RandomColor();

       /* Vector3 position = new Vector3(0, 1.8f, 0);
        Color color = RandomColor();*/

        serverPlayer = new ServerPlayer(peer.Id, peer, Instantiate(playerPrefab, position, Quaternion.identity),
            _clientsCameraTrans[peer.Id]);
        serverPlayer.SetColor(color);
        _serverPlayers.Add(serverPlayer);
       _clientsCameraTrans.Remove(peer.Id);
    }

    void ServerPlayersListRemove(NetPeer peer)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        clientInputs[serverPlayer].Clear();
        clientInputs.Remove(serverPlayer);
        Destroy(serverPlayer._maingameObject);
        _serverPlayers.Remove(serverPlayer);
    }

    void ServerPlayersListClear()
    {
        _serverPlayers.ForEach(serverPlayer => {
            Destroy(serverPlayer._maingameObject);
        });
        _serverPlayers.Clear();
        clientInputs.Clear();
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.LogFormat("A Peer is connected Id : {0}", peer.Id);
        SpawnPlayers(peer);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        //Remove the player from the server
        ServerPlayersListRemove(peer);
        //Send The Player Leave to Every one
        NetDataWriter leavePacket = new NetDataWriter();
        leavePacket.Put((int)PacketType.Leave);
        leavePacket.Put(peer.Id);
        SendToAllClients(leavePacket);
        Debug.LogFormat("[SERVER] Peer : {0} disconnected : {0} there are {0} players remaining", peer.Id, disconnectInfo.Reason, _serverPlayers.Count);

    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        throw new System.NotImplementedException();
    }

    void ServerExecuteRPC(NetPeer senderPeer, string methodName, RPCTarget rPCTarget, object[] parameters)
    {
        Debug.Log("I want to execute the RPC As Weell !!");
    }

    public void ProcessClientRPC(NetPeer senderPeer, RPCTarget rPCTarget, string methodName, string parametersOrder, object[] parameters)
    {
        NetDataWriter rpcData;
        RPCPacket rPCPacket;

        rPCPacket = new RPCPacket();
        rpcData = new NetDataWriter();
        rpcData.Put((int)PacketType.RPC);
        rPCPacket.Serialize(rpcData);  
        //Excute The RPC on The Server
        ServerExecuteRPC(senderPeer, methodName, rPCTarget, parameters);
        if (rPCTarget == RPCTarget.ALL)
        {
            SendToAllClients(rpcData);
        }
        else
        {
            SendToAllClientsExcept(rpcData, netPeer: senderPeer);
        }
    }

    #region Packet From The Clients
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        PacketType type;

        type = (PacketType)reader.GetInt();
        switch (type)
        {
            case PacketType.CameraSetup:
                {
                    CameraSetupPacket cameraSetup = new CameraSetupPacket();
                    cameraSetup.Deserialize(reader);
                    GameObject emptyObject = new GameObject();
                    emptyObject.transform.position = cameraSetup.Position;
                    emptyObject.transform.rotation = cameraSetup.Rotation;
                    _clientsCameraTrans.Add(cameraSetup.Id, emptyObject.transform);
                   // Destroy(emptyObject); //Dont forget to get rid of those empy gameobject on the server
                }
                break;

            case PacketType.Movement:
                {
                    InputPacket inputPacket;
                    inputPacket = new InputPacket();
                    inputPacket.Deserialize(reader);
                    UpdateClientInputs(inputPacket.Id, inputPacket);
                }
                break;
            case PacketType.RPC:
                {
                    RPCPacket rpcPacket;
                    rpcPacket = new RPCPacket();
                    ServerRPCPacket serverRPC = new ServerRPCPacket();
                    rpcPacket.Deserialize(reader);

                    serverRPC.netPeer = peer;
                    serverRPC.rpcPacket = rpcPacket;
                    _rPCPackets.Add(serverRPC);
                    if (rpcPacket.parameterLength < 1) //surely void
                    { ProcessClientRPC(peer, rpcPacket.rpcTarget, rpcPacket.methodName, null, null); }
                    else { ProcessClientRPC(peer, rpcPacket.rpcTarget, rpcPacket.methodName, rpcPacket.parametersOrder, rpcPacket.parameters); }
                }
                break;
            default:
                Debug.LogWarning($"Receive Unknown packet from the client : {peer.Id}");
                break;
        }
    }
    #endregion
    public void UpdateClientInputs(int peerId, InputPacket inputPacket)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peerId);
        Debug.Log($"Peer Id {peerId} over : {_serverPlayers.Count} ");
        if (serverPlayer == null) { Debug.Log("Should Never Happpend !!! You have input from unknown peer "); return; }
        ClientInputState message = new ClientInputState();
        Tools.NInput nInput = new Tools.NInput
        {
            InputX = inputPacket.InputX,
            InputY = inputPacket.InputY,
            MouseX = inputPacket.MouseX,
            MouseY = inputPacket.MouseY,
            Jump = inputPacket.Jump,
            Run = inputPacket.Run
        };
        message.nInput = nInput;
        message.NTime = inputPacket.NTime;
        message.Timestamp = inputPacket.Timestamp;
        OnClientInputStateReceived(serverPlayer, message);
    }

    void ProcessClientInputs(ServerPlayer serverPlayer, ClientInputState inputState)
    {
        serverPlayer._maingameObject.GetComponent<PlayerController>().ApplyInput(inputState.nInput, inputState.NTime);
        serverPlayer.Timestamp = inputState.Timestamp;
    }

    SimulationState  ClientCurrentSimulationState(ServerPlayer serverPlayer, ClientInputState inputState)
    {
        if (inputState.Timestamp != serverPlayer.Timestamp) { Debug.Log(" No The Same :--("); }
        return new SimulationState
        {
            position = serverPlayer._maingameObject.GetComponent<PlayerController>().transform.position,
            rotation = serverPlayer._maingameObject.GetComponent<PlayerController>().transform.rotation,
            animSpeed = serverPlayer._maingameObject.GetComponent<PlayerController>().animSpeed,
            camPosition = serverPlayer._maingameObject.GetComponent<PlayerController>().cameraTrans.position,
            camRotation = serverPlayer._maingameObject.GetComponent<PlayerController>().cameraTrans.rotation,
            Timestamp = inputState.Timestamp
        };

    }

    //For production purpose where the server will be at 50fps
    private void FixedUpdate2()
    {
        foreach (KeyValuePair<ServerPlayer,  Queue<ClientInputState>> entry in clientInputs)
        {
            ServerPlayer clientPlayer = entry.Key;
            Queue<ClientInputState> queue = entry.Value;

            ClientInputState inputState = null;
            while (queue.Count > 0 && (inputState = queue.Dequeue()) != null)
            {
                // Process the input.
                ProcessClientInputs(clientPlayer, inputState);
                SimulationState state = ClientCurrentSimulationState(clientPlayer, inputState);
                NetworkServerSendToClient(clientPlayer, state);
            }
        }
    }

    void NetworkServerSendToClient(ServerPlayer player, SimulationState state)
    {
        PlayerStatePacket playerState = new PlayerStatePacket();
        NetDataWriter playerStateData = new NetDataWriter();

        playerState.Id = player._id;
        playerState.Position = state.position;
        playerState.Rotation = state.rotation;
        playerState.AnimSpeed = state.animSpeed;
        playerState.camPosition = state.camPosition;
        playerState.camRotation = state.camRotation;
        
        playerState.Timestamp = state.Timestamp; 
        playerStateData.Put((int)PacketType.ServerState);
        playerState.Serialize(playerStateData);
        SendToAllClients(playerStateData);
    }

    private void OnClientInputStateReceived(ServerPlayer serverPlayer, ClientInputState message)
    {
        if (clientInputs.ContainsKey(serverPlayer) == false)
        {
            clientInputs.Add(serverPlayer, new Queue<ClientInputState>());
        }
        clientInputs[serverPlayer].Enqueue(message);
    }


    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        throw new System.NotImplementedException();
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        if (peer.Tag != null)
        {
            Debug.LogFormat("Peer : {0} tag : {1} ", peer, peer.Tag);
            /*var p = (ServerPlayer)peer;
            p.Ping = latency;*/
        }
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        if (_server.PeersCount < AuthServer.MAX_CONNECTION)
            request.AcceptIfKey(AuthServer.CONNECTION_KEY);
        else
            request.Reject();
    }
}