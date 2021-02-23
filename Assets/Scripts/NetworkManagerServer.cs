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

    public ServerPlayer(int id, NetPeer peer, GameObject gameObject)
    {
        _id = id;
        _peer = peer;
        _maingameObject = gameObject;
    }

    public void SetTransform(Vector3 pos, Quaternion rot)
    {
        _maingameObject.transform.position = pos;
        _maingameObject.transform.rotation = rot;
    }

   public void SetColor(Color color)
    {
        _maingameObject.GetComponent<Renderer>().material.color = color;
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
    private int Ids = 0;
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

    void SendInputToClient(int id, float analogOne, float analogTwo)
    {
        netListener.PeerConnectedEvent += (peer) =>
        {
            writer.Put(id);
            writer.Put(analogOne);
            writer.Put(analogTwo);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        };
    }

    void ReceiveInputFromClient()
    {
        netListener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
        {
            Debug.LogFormat(" id : {0} ", dataReader.GetInt());
            Debug.LogFormat(" Analog 1 : {0} ", dataReader.GetFloat());
            Debug.LogFormat(" Analog 2 : {0} ", dataReader.GetFloat());
            dataReader.Recycle();
        };
    }


    void SendToClient() 
    {
        SendInputToClient(0, 2.5f, 3.0f);
    }

    void ReceiveClientData()
    {
        ReceiveInputFromClient();
    }

  
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _server.Stop();
        }
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
        return (new Vector3(Random.Range(Xmin, Xmax), 0.5f, Random.Range(Zmin, Zmax)));
    }

    Quaternion RandomRotation()
    {
        Quaternion quat = Quaternion.identity;
        quat.eulerAngles = new Vector3(Random.Range(0f, 180f), Random.Range(0f, 180f), Random.Range(0, 180f));
        return (quat);
    }

    Color RandomColor()
    {
        return (new Color(Random.Range(0f, 1f), Random.Range(0, 1f), Random.Range(0, 1f), 1.0f));
    }

    

    void SpawnPlayers()
    {
       
        _serverPlayers.ForEach(player =>
        {
            NetDataWriter spawnPacket = new NetDataWriter();

           // Debug.Log(player);
            spawnPacket.Put((int)PacketType.Spawn);
            spawnPacket.Put((int)player._id);
            //Debug.Log($"The Peer Should Spawn Id : {player._id}");

            Vector3 position  = player._maingameObject.transform.position;
            Quaternion rotation = player._maingameObject.transform.rotation;
            Color color = player._maingameObject.GetComponent<Renderer>().material.color;

            
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
            _server.SendToAll(spawnPacket, DeliveryMethod.ReliableOrdered);
        });
    }

    void   ServerPlayersListAdd(NetPeer peer)
    {
       
        Vector3 position = RandomPosition();
        Quaternion rotation = RandomRotation();
        Color color = RandomColor();

        ServerPlayer serverPlayer = new ServerPlayer(Ids, peer, Instantiate(playerPrefab, position, rotation));
       // serverPlayer.SetTransform(position, rotation);
        serverPlayer.SetColor(color);

        _serverPlayers.Add(serverPlayer);
    }

    void ServerPlayersListRemove(NetPeer peer)
    {
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        Destroy(serverPlayer._maingameObject);
        _serverPlayers.Remove(serverPlayer);
    }

    void ServerPlayersListMovePlayers(NetPeer peer, float inputX,  float inputY)
    {
        float speed = 10f;
        ServerPlayer serverPlayer = _serverPlayers.Find(player => player._id == peer.Id);
        
        if (serverPlayer != null)
        {
            serverPlayer._maingameObject.transform.Translate(Vector3.forward * inputY * speed * FPS_TICK);
            serverPlayer._maingameObject.transform.Translate(Vector3.right * inputX * speed * FPS_TICK);
        }
    }


    public void OnPeerConnected(NetPeer peer)
    {
        Debug.LogFormat("A Peer is connected Id : {0}", peer.Id);
        ServerPlayersListAdd(peer);
        NetDataWriter joinedPacket = new NetDataWriter();
        joinedPacket.Put((int)PacketType.Join);
        joinedPacket.Put((int)peer.Id);
        _server.SendToAll(joinedPacket, DeliveryMethod.ReliableOrdered);

        SpawnPlayers();
        Ids++;
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        //Remove the player from the server
        ServerPlayersListRemove(peer);
        Ids--;
        //Send The Player Leave to Every one
        NetDataWriter leavePacket = new NetDataWriter();
        leavePacket.Put((int)PacketType.Leave);
        leavePacket.Put((int)peer.Id);
        _server.SendToAll(leavePacket, DeliveryMethod.ReliableOrdered);
        Debug.LogFormat("[SERVER] Peer : {0} disconnected : {0}", peer.Id, disconnectInfo.Reason);
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
                    float inputX = reader.GetFloat();
                    float inputY = reader.GetFloat();

                    ServerPlayersListMovePlayers(peer, inputX, inputY);
                    NetDataWriter mvtData = new NetDataWriter();
                    mvtData.Put((int)PacketType.Movement);
                    mvtData.Put(peerId);
                    mvtData.Put(inputX);
                    mvtData.Put(inputY);
                    _server.SendToAll(mvtData, DeliveryMethod.ReliableOrdered);
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
