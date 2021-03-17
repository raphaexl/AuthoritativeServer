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
    public NetPeer _peer;
    public GameObject _maingameObject;
    public List<PendingInput> _clientInput;
    public int _lastProcessedInput;


    //Make the variables properties

    public ServerPlayer(int id, NetPeer peer, GameObject _gameObject)
    {
        _id = id;
        _peer = peer;
        _maingameObject =_gameObject;
        _maingameObject.transform.GetChild(0).gameObject.GetComponent<Camera>().gameObject.SetActive(false);
        _clientInput = new List<PendingInput>();
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
        return ($" Id {_id} \n" +
            $" Pos({_maingameObject.transform.position.x}, {_maingameObject.transform.position.y}, {_maingameObject.transform.position.z}) \n" +
            $": Rot({_maingameObject.transform.rotation.x}, {_maingameObject.transform.rotation.y}, {_maingameObject.transform.rotation.z}, {_maingameObject.transform.rotation.w}) \n" +
            $" : Color :-) ");
       // return base.ToString();
    }
}

public class NetworkManagerServer : MonoBehaviour, INetEventListener
{
    protected EventBasedNetListener netListener;
    protected NetManager _server;
    const int SERVER_PORT = 9050;
    const int MAX_CONNECTION = 10;
    const string CONNECTION_KEY = "KEYOFCONNCETION";
    const float SERVER_UPDATE_RATE = .3f;
    private Coroutine updateRoutine;

    protected NetDataWriter writer;
    List<ServerPlayer> _serverPlayers;
    List<ServerRPCPacket> _rPCPackets;
    [SerializeField]
    GameObject playerPrefab;


    void Start()
    {
        _server = new NetManager(this);
        writer = new NetDataWriter();
        _server.Start(SERVER_PORT);
        _serverPlayers = new List<ServerPlayer>();
        _rPCPackets = new List<ServerRPCPacket>();
        Debug.Log($"Server Start at Port {SERVER_PORT}");
        updateRoutine = StartCoroutine(serverUpdate(SERVER_UPDATE_RATE));
    }

    /*private void OnEnable()
    {
        updateRoutine = StartCoroutine(serverUpdate(SERVER_UPDATE_RATE));
    }
    private void OnDisable()
    {
        StopCoroutine(updateRoutine);
        updateRoutine = null;
    }*/

    void ReceiveClientData()
    {

    }

    IEnumerator serverUpdate(float update_rate)
    {
        var wait = new WaitForSecondsRealtime(1.0f / update_rate);
        while (true)
        {
            // server update loop
            processClientsInput();  //Process each client input 
            sendWorldStateToClients(); //sendWorldState to all client
            //Render world if we were to
            yield return wait;
        }
    }


    //Basic Rule Checker
    bool checkInputValidity(PendingInput pendingInput)
    {
        if (Mathf.Abs(pendingInput.nTime) < 1 / 140) //let pretend 120
                                                  // if (Mathf.Abs(pendingInput.nTime) > 1 / 40)
        {
            return false;
        }
        return true;
    }

    void ApplyClientPendingInputs(ServerPlayer serverPlayer)
    {
        serverPlayer._clientInput.ForEach(pendingInput =>
        {
            if (checkInputValidity(pendingInput))
            {
                //Apply  client pending Inputs
             //   Debug.Log("Applying pending Input");
                serverPlayer._maingameObject.GetComponent<PlayerController>().ApplyInput(pendingInput.nInput, pendingInput.nTime);
                serverPlayer._lastProcessedInput = pendingInput.sequenceNumber;
            }
              else
              {
                  Debug.Log($"Player : {serverPlayer._id} is cheating");
              }
        });
        serverPlayer._clientInput.Clear();
    }

    void processClientsInput()
    {
        _serverPlayers.ForEach(serverPlayer =>
        {
            ApplyClientPendingInputs(serverPlayer);
        });
    }

    void sendTransformsToClients()
    {
        _serverPlayers.ForEach(player =>
        {
            NetDataWriter transformData = new NetDataWriter();

            Vector3 position = player._maingameObject.transform.position;
            Quaternion rotation = player._maingameObject.transform.rotation;

            transformData.Put((int)PacketType.ServerState);
            transformData.Put(player._id);
            transformData.Put(player._lastProcessedInput);
            transformData.Put(position.x);
            transformData.Put(position.y);
            transformData.Put(position.z);
            transformData.Put(rotation.x);
            transformData.Put(rotation.y);
            transformData.Put(rotation.z);
            transformData.Put(rotation.w);
            //Debug.Log($" New Position :({position.x}, {position.y}, {position.z})");
            SendToAllClients(transformData);

            //$$$$$$$
        });
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

    void sendWorldStateToClients()
    {
        sendTransformsToClients();
    }

    private void Update()
    {
        _server.PollEvents();
    }

    private void OnDestroy()
    {
        if (_server != null) { _server.Stop(); }
        StopCoroutine(updateRoutine);
        updateRoutine = null;
    }

    const float Zmax = 5f;
    const float Zmin = -5f;
    const float Xmax = 5f;
    const float Xmin = -5f;

    Vector3 RandomPosition()
    {
        return (new Vector3(Random.Range(Xmin, Xmax), 2.0f, Random.Range(Zmin, Zmax)));
        //return (new Vector3(0, 2.0f, 0));
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

    IEnumerator PlayerSpawnRoutine()
    {
        yield return new WaitForSeconds(1f);
        _serverPlayers.ForEach(player =>
        {
            NetDataWriter _spawnwriter = new NetDataWriter();
            SpawnPacket spawnPacket = new SpawnPacket();

            _spawnwriter.Put((int)PacketType.Spawn);
            spawnPacket.PlayerId = player._id;
            spawnPacket.Position = player._maingameObject.transform.position;
            spawnPacket.Rotation = player._maingameObject.transform.rotation;
            spawnPacket.Albedo = player._maingameObject.transform.GetChild(1).GetComponent<Renderer>().material.color;
            spawnPacket.Serialize(_spawnwriter);
            SendToAllClients(_spawnwriter);
        });
    }

    void SpawnPlayers()
    {
        StartCoroutine(PlayerSpawnRoutine());
    }

    void ServerPlayersListAdd(NetPeer peer)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        if (serverPlayer != null) { Debug.LogError("Should never happen :::::::: !"); return; }

        Vector3 position = RandomPosition();
        Color color = RandomColor();

        serverPlayer = new ServerPlayer(peer.Id, peer, Instantiate(playerPrefab, position, Quaternion.identity));
        serverPlayer.SetColor(color);
        _serverPlayers.Add(serverPlayer);
    }

    void ServerPlayersListRemove(NetPeer peer)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        Destroy(serverPlayer._maingameObject);
        _serverPlayers.Remove(serverPlayer);
    }

    public void OnPeerConnected(NetPeer peer)
    {
        Debug.LogFormat("A Peer is connected Id : {0}", peer.Id);
        ServerPlayersListAdd(peer);
        NetDataWriter joinedPacket = new NetDataWriter();
        joinedPacket.Put((int)PacketType.Join);
        joinedPacket.Put(peer.Id);
        SendToAllClients(joinedPacket);
        SpawnPlayers();
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
            case PacketType.Movement:
                {
                    PendingInput pendingInput;
                    int senderPeerId = reader.GetInt();
                    pendingInput = new PendingInput();

                    pendingInput.sequenceNumber = reader.GetInt();
                    pendingInput.nTime = reader.GetFloat();
                    pendingInput.nInput.InputX = reader.GetFloat();
                    pendingInput.nInput.InputY = reader.GetFloat();
                    pendingInput.nInput.Jump = reader.GetBool();
                    pendingInput.nInput.MouseX = reader.GetFloat();
                    pendingInput.nInput.MouseY = reader.GetFloat();
                    UpdateClientInputPendingList(senderPeerId, pendingInput);
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
    public void UpdateClientInputPendingList(int peerId, PendingInput pendingInput)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peerId);
        if (serverPlayer == null) { return; }
        serverPlayer._clientInput.Add(pendingInput);
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
        if (_server.PeersCount < MAX_CONNECTION)
            request.AcceptIfKey(CONNECTION_KEY);
        else
            request.Reject();
    }
}
