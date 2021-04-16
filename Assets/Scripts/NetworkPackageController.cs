using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkPackageController : MonoBehaviour
{
    bool isLocalPlayer = false;
    bool isServer = false;

    [System.Serializable]
    public class SendPackage
    {
        public float Horizontal;
        public float Vertical;
        public float Timestamp;
    }
    [System.Serializable]
    public class ReceivedPackage
    {
        public Vector3 Position;
        public float Timestamp;
    }

    public NetworkPackageManager<SendPackage> m_packageManager;
    public NetworkPackageManager<SendPackage> PackageManager
    {
        get
        {
            if (m_packageManager == null)
            {
                m_packageManager = new NetworkPackageManager<SendPackage>();
                if (isLocalPlayer)
                {
                    m_packageManager.OnRequiredPackageTransmit += TransmitPackageToServer;
                }
            }
            return m_packageManager;
        }
    }

    public NetworkPackageManager<ReceivedPackage> m_serverPackageManager;
    public NetworkPackageManager<ReceivedPackage> ServerPackageManager
    {
        get
        {
            if (m_serverPackageManager == null)
            {
                m_serverPackageManager = new NetworkPackageManager<ReceivedPackage>();
                if (isServer)
                {
                    m_serverPackageManager.OnRequiredPackageTransmit += TransmitPackageToClients;
                }
            }
            return m_serverPackageManager;
        }
    }

    private void TransmitPackageToServer(byte[] bytes)
    {
        CmdTransmitPackages(bytes);
    }

    private void TransmitPackageToClients(byte[] bytes)
    {
        RpcReceivedDataOnClient(bytes);
    }

    //[Command]
    private void CmdTransmitPackages(byte[] bytes)
    {
        PackageManager.ReceivePackages(bytes);
    }

    //[ClientRpc]
    private void RpcReceivedDataOnClient(byte[] data)
    {
        ServerPackageManager.ReceivePackages(data);
    }

    public virtual void FixedUpdate()
    {
        PackageManager.Tick();
        ServerPackageManager.Tick();
    }
}