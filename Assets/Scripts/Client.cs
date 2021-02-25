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
        go.GetComponent<Player>().ID = id;
        go.GetComponent<Player>().Id = id;
        //Disable the camera of the enemy
        go.transform.GetChild(0).gameObject.GetComponent<Camera>().gameObject.SetActive(false);
        go.GetComponent<Player>().IsLocalPlayer = false;
    }
}

public class Client : NetworkManagerClient
{
    const string CONNECTION_KEY = "KEYOFCONNCETION";

    [SerializeField]
    GameObject playerPrefab;

    List<RemotePlayer> _remotePlayers;
    GameObject _localPlayer;

    private int Id;
    private Tools.NInput nInput;


    const string SERVER_URL = "localhost";
    const int PORT = 9050;
    const int MAX_LENGTH = 500;
    const int CLIENT_SLEEP_TIME = 15;
    const float FPS_TICK = 0.02f;


    bool istantiated = false;
    bool isConnected = false;
    // Start is called before the first frame update
   new void Start()
    {
        Id = -1;
        base.Start();
        StartClient();
        _localPlayer = null;
        _remotePlayers = new List<RemotePlayer>();
        isConnected = false;
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

    void MoveLocalplayer()
    {
        if (!_localPlayer) { return; }
        //float speed = 10f;
        ////client Side Prediction ?
        //_localPlayer.transform.Translate(Vector3.forward * inputY * speed * FPS_TICK);
        //_localPlayer.transform.Translate(Vector3.right * inputX * speed * FPS_TICK);

        /*PlayerController controller = _localPlayer.GetComponent<PlayerController>();
        if (!controller)
        {
            Debug.Log("_Local Player is not being moved ?? No ??? ");
        }
        controller.ApplyInput(inputX, inputY, jump, FPS_TICK);*/
        //Debug.Log($"Mouse X : {mouseX} Y : {mouseY}");
        _localPlayer.GetComponent<PlayerController>().ApplyInput(nInput, FPS_TICK);
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
                    int peerId = reader.GetInt();
                    Vector3 position;
                    Quaternion rotation;

                    position.x = reader.GetFloat();
                    position.y = reader.GetFloat();
                    position.z = reader.GetFloat();

                    rotation.x = reader.GetFloat();
                    rotation.y = reader.GetFloat();
                    rotation.z = reader.GetFloat();
                    rotation.w = reader.GetFloat();
                    if (peerId == Id) //Server Reconciliation
                    {
                        //Debug.Log("Should Reconciliate with the server");
                        ServerReconciliation();
                    }
                    else
                    {
                       // Debug.Log("Updation the other players position but nope");
                        RemotePlayersMove(peerId, position, rotation);
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

    void ServerReconciliation()
    {
        
    }

    void RemotePlayersMove(int id, Vector3 position, Quaternion rotation)
    {

        RemotePlayer foundPlayer = _remotePlayers.Find(player => player.Id == id);
        if (foundPlayer != null)
        {
            foundPlayer.go.transform.position = position;
            foundPlayer.go.transform.rotation = rotation;
            Debug.LogFormat("Player : [{0}] : {1}", id, rotation);
        }                   

        else
            Debug.Log("Didn't Find Player");
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
        nInput.inputX = Input.GetAxis("Horizontal");
        nInput.inputY = Input.GetAxis("Vertical");
        nInput.jump = Input.GetButtonDown("Jump");
        nInput.mouseX = Input.GetAxis("Mouse X");
        nInput.mouseY = Input.GetAxis("Mouse Y");
        //if (nInput.inputX != 0 || nInput.inputY != 0 || nInput.jump)
        {
            MoveLocalplayer();
            UpdateServerInput();
        }

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
        inputData.Put(nInput.inputX);
        inputData.Put(nInput.inputY);
        inputData.Put(nInput.jump);
        inputData.Put(nInput.mouseX);
        inputData.Put(nInput.mouseY);
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
