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
    protected NetDataWriter writer;
    protected InputPacket inputPacket;
    protected NetPeer netPeer;


    // Start is called before the first frame update
    protected void Start()
    {
        _client = new NetManager(this);
        writer = new NetDataWriter(); //Will be used for GC optimizations !!!!!!!!!!!!!!!!!!!!!!
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
    protected void SendInputToServer(PendingInput pendingInput)
    {
        if (netPeer == null)
        { return;}
        // NetDataWriter netData = inputPacket.
        NetDataWriter inputData = new NetDataWriter();
        inputData.Put((int)PacketType.Movement);
        inputData.Put(netPeer.Id);
        inputData.Put(pendingInput.sequenceNumber);
        inputData.Put(pendingInput.nTime);
        inputData.Put(pendingInput.nInput.inputX);
        inputData.Put(pendingInput.nInput.inputY);
        inputData.Put(pendingInput.nInput.jump);
        inputData.Put(pendingInput.nInput.mouseX);
        inputData.Put(pendingInput.nInput.mouseY);
        netPeer.Send(inputData, DeliveryMethod.ReliableOrdered);
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        throw new NotImplementedException();
    }
}
