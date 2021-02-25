using System.Collections;
using System;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;

public class NetworkManagerClient : MonoBehaviour, INetEventListener
{
    protected NetManager _client;
    const string SERVER_URL = "localhost";
    const int PORT = 9050;
    const float TICK_INTERVAL = 0.02f;

    protected NetDataWriter writer;
    protected InputPacket inputPacket;
    protected NetPeer netPeer;


    float period;

    // Start is called before the first frame update
    protected void Start()
    {
        period = 0f;
        _client = new NetManager(this);
        writer = new NetDataWriter();
        netPeer = null;
    }

    protected void StartClient()
    {
        _client.Start();
    }

    protected void Connect(string connectionKey)
    {
        if (netPeer != null && netPeer.ConnectionState == ConnectionState.Connected)
        {
            return;
        }
        _client.Connect(SERVER_URL, PORT, connectionKey);
    }

    private void OnDestroy()
    {
        if (_client != null) { _client.Stop(); }
    }

    protected void Update()
    {
        if (_client != null) { _client.PollEvents(); }
        if (period >= TICK_INTERVAL)
        {
            period = 0f;
            SendToServer();
        }
        period += Time.deltaTime;
    }

    public virtual void OnPeerConnected(NetPeer peer)
    { }

    public virtual void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {}

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        throw new NotImplementedException();
    }

    public virtual void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {}

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        throw new NotImplementedException();
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

    //protected void SendInputToServer(InputPacket packet)
    protected void SendInputToServer()
    {
        if (netPeer == null)
        { return;}
       // NetDataWriter netData = inputPacket.
        netPeer.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    protected void SendToServer()
    {
        SendInputToServer();
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        throw new NotImplementedException();
    }
}
