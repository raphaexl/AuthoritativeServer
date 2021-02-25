using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;


public class ServerPlayer
{
    public int _id;
    public NetPeer _peer;
    public GameObject _maingameObject;

    //Make the variables properties

    public ServerPlayer(int id, NetPeer peer, GameObject _gameObject)
    {
        _id = id;
        _peer = peer;
        _maingameObject = _gameObject;
        _maingameObject.transform.GetChild(0).gameObject.GetComponent<Camera>().gameObject.SetActive(false);
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
    const int SERVER_SLEEP_TIME = 15;
    const float FPS_TICK = 0.02f;
    protected NetDataWriter writer;
    List<ServerPlayer> _serverPlayers;
    [SerializeField]
    GameObject playerPrefab;


    void Start()
    {
        _server = new NetManager(this);
        writer = new NetDataWriter();
        _server.Start(SERVER_PORT);
        _serverPlayers = new List<ServerPlayer>();
        Debug.Log($"Server Start at Port {SERVER_PORT}");
    }

  
   
    void ReceiveClientData()
    {
      
    }

  
    private void Update()
    {
        _server.PollEvents();
    }

    private void OnDestroy()
    {
        if (_server != null) { _server.Stop(); }
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


    void PlayersPositionSynchronization()
    {
        _serverPlayers.ForEach(player =>
        {
            NetDataWriter transformData = new NetDataWriter();

            Vector3 position = player._maingameObject.transform.position;
            Quaternion rotation = player._maingameObject.transform.rotation;

            transformData.Put((int)PacketType.ServerState);
            transformData.Put(player._id);
            transformData.Put(position.x);
            transformData.Put(position.y);
            transformData.Put(position.z);
            transformData.Put(rotation.x);
            transformData.Put(rotation.y);
            transformData.Put(rotation.z);
            transformData.Put(rotation.w);
            SendToAllClients(transformData);
        });
    }

    void SpawnPlayers()
    {
       
        _serverPlayers.ForEach(player =>
        {
            NetDataWriter spawnPacket = new NetDataWriter();

           // Debug.Log(player);
            spawnPacket.Put((int)PacketType.Spawn);
            spawnPacket.Put(player._id);

            Vector3 position  = player._maingameObject.transform.position;
            Quaternion rotation = player._maingameObject.transform.rotation;
            Color color = player._maingameObject.transform.GetChild(1).GetComponent<Renderer>().material.color;

            
            spawnPacket.Put(position.x);
            spawnPacket.Put(position.y);
            spawnPacket.Put(position.z);

            spawnPacket.Put(rotation.x);
            spawnPacket.Put(rotation.y); 
            spawnPacket.Put(rotation.z);
            spawnPacket.Put(rotation.w);

            spawnPacket.Put(color.r);
            spawnPacket.Put(color.g);
            spawnPacket.Put(color.b);
            SendToAllClients(spawnPacket);
        });
    }

    void   ServerPlayersListAdd(NetPeer peer)
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

    void ServerPlayersListMovePlayers(NetPeer peer, Tools.NInput nInput)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        
        if (serverPlayer != null)
        {
           // serverPlayer._maingameObject.GetComponent<Player>
            serverPlayer._maingameObject.GetComponent<PlayerController>().ApplyInput(nInput, FPS_TICK);
            //serverPlayer._maingameObject.transform.Translate(Vector3.forward * inputY * speed * FPS_TICK);
            //serverPlayer._maingameObject.transform.Translate(Vector3.right * inputX * speed * FPS_TICK);
        }
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

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        PacketType type;

        type = (PacketType)reader.GetInt();
        switch (type)
        {
            case PacketType.Movement:
                {
                    int peerId = reader.GetInt();
                    Tools.NInput nInput;

                    nInput.inputX = reader.GetFloat();
                    nInput.inputY = reader.GetFloat();
                    nInput.jump = reader.GetBool();
                    nInput.mouseX = reader.GetFloat();
                    nInput.mouseY = reader.GetFloat();

                    ServerPlayersListMovePlayers(peer, nInput);
                    PlayersPositionSynchronization();
                }
                break;
            default:
                Debug.Log($"Receive Unknown packet from the client : {peer.Id}");
                break;
        }
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
