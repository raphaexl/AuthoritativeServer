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

    public RemotePlayer(int id, GameObject _go)
    {
        Id = id;
        go = _go;
    }
}

public class Client : NetworkManagerClient
{
    const string CONNECTION_KEY = "KEYOFCONNCETION";

    [SerializeField]
    GameObject playerPrefab;

    List<RemotePlayer> _remotePlayers;
    GameObject _localPlayer;

    int Id;
    float   inputX;
    float   inputY;

    const string SERVER_URL = "localhost";
    const int PORT = 9050;
    const int MAX_LENGTH = 500;
    const int CLIENT_SLEEP_TIME = 15;
    const float FPS_TICK = 0.02f;



    // Start is called before the first frame update
   new void Start()
    {
        Id = -1;
        base.Start();
        StartClient();
        _remotePlayers = new List<RemotePlayer>();
    }

    public void ConnectToServer()
    {
        ClientUIController.Instance.onClientConnected.text = "Connecting...";
        ClientUIController.Instance.onClientReceiveFromServer.text = "";
        base.Connect(CONNECTION_KEY);
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
        Debug.Log(" You are connected is it to the Server ?? "+ peer.Id);
    }


    public void PlayersInstanciation(int id, Vector3 position, Quaternion rotation, Color color, bool isLocalPlayer)
    {
        RemotePlayer foundPlayer = _remotePlayers.Find(player => player.Id == id);
        if (foundPlayer == null)
        {
            GameObject go = Instantiate(playerPrefab, position, rotation);
            go.GetComponent<MeshRenderer>().material.color = color;
            go.SetActive(true);
            if (isLocalPlayer)
            {
                _localPlayer = go;
                Debug.Log("Instanciation of Self");
            }
            else
            {
                _remotePlayers.Add(new RemotePlayer(id, go));
                Debug.Log("Instanciation of an enemy");
            }
        }
    }

    void MoveLocalplayer()
    {
        if (!_localPlayer) { return; }
        float speed = 10f;
        //client Side Prediction ?
        _localPlayer.transform.Translate(Vector3.forward * inputY * speed * FPS_TICK);
        _localPlayer.transform.Translate(Vector3.right * inputX * speed * FPS_TICK);
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
                    if (Id < 0){
                        netPeer = peer;
                        Id = peerId;
                        ClientUIController.Instance.onClientConnected.text = $"connected to the server with : {peerId}";
                    }
                    string msg = ClientUIController.Instance.onClientReceiveFromServer.text;
                    if (Id != peerId)
                    {
                        msg += $"[SERVER] :  A Player Joined with id : {peerId}";
                    }
                    ClientUIController.Instance.onClientReceiveFromServer.text = msg;
                }
                break;
            case PacketType.Spawn:
                {
                    int peerId = reader.GetInt();
                    {
                        Vector3 position;
                        Quaternion rotation;
                        Color color =  new Color();

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
            case PacketType.Movement:
                {
                    int peerId = reader.GetInt();
                    float sInputX = reader.GetFloat();
                    float sInputY = reader.GetFloat();

                    Debug.Log($" From server id : {peerId} : inputX : {sInputX} inputY : {sInputY}");
                    if (peerId == Id) //Server Reconciliation
                    {
                        Debug.Log("Sould Reconciliate with the server");
                        ServerReconciliation();
                    }
                    else
                    {
                        Debug.Log("Updation the other players position");
                        RemotePlayersMove(peerId, sInputX, sInputY);
                    }
                }
                break;
            case PacketType.ServerState:
                {

                }break;
            default:
                ClientUIController.Instance.onClientReceiveFromServer.text = "UNKNOWN PACKET";
                Debug.LogWarning("Received Unknown Packet From The Server");
                break;
        }
        reader.Recycle();
    }

    void ServerReconciliation()
    {

    }

    void RemotePlayersMove(int id, float inputX, float inputY)
    {
        float speed = 10f;
        RemotePlayer foundPlayer = _remotePlayers.Find(player => player.Id == id);
        if (foundPlayer != null)
        {
            foundPlayer.go.transform.Translate(Vector3.forward * inputY * speed * FPS_TICK);
            foundPlayer.go.transform.Translate(Vector3.right * inputX * speed * FPS_TICK);
        }
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
        inputX = Input.GetAxis("Horizontal");
        inputY = Input.GetAxis("Vertical");
        MoveLocalplayer();
        UpdateServerInput();
    }
    
    void OthersRemove(int id)
    {
        RemotePlayer toDelete = _remotePlayers.Find(player => player.Id == id);
        Debug.LogFormat("Found : {0} ", toDelete.Id);
        if (toDelete.go != null)
        {
            Debug.Log($"Remove Gamobject : {toDelete.go}");
        }
        Destroy(toDelete.go);
        _remotePlayers.Remove(toDelete);
        Debug.Log($"Removed {id}");
    }

    void UpdateServerInput()
    {
        NetDataWriter inputData = new NetDataWriter();
        inputData.Put((int)PacketType.Movement);
        inputData.Put(Id);
        inputData.Put(inputX);
        inputData.Put(inputY);
        base.writer = inputData;
    }

    void ReceiveInputFromServer()
    {
        //Debug.LogFormat(" id : {0} ", dataReader.GetInt());
        //Debug.LogFormat(" Analog 1 : {0} ", dataReader.GetFloat());
        //Debug.LogFormat(" Analog 2 : {0} ", dataReader.GetFloat());
        //dataReader.Recycle();
    }

  
}
