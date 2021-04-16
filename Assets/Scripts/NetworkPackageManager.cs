using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class NetworkPackageManager<T> where T : class
{
    public event System.Action<byte[]> OnRequiredPackageTransmit;

    private float m_sendSpeed;
    public float SendSpeed
    {
        get
        {
            if (m_sendSpeed < 0.1f)
            {
                m_sendSpeed = 0.1f;
            }
            return m_sendSpeed;
        }
        set
        {
            m_sendSpeed = value;
        }
    }

    private float nextTick = 0f;
    List<T> m_packages;
    public List<T> Packages
    {
        get
        {
            if (m_packages == null)
            {
                m_packages = new List<T>();
            }
            return m_packages;
        }
    }

    public void AddPackage(T package)
    {
        Packages.Add(package);
    }

    public Queue<T> receivedPackages;

    public void ReceivePackages(byte[] bytes)
    {
        if (receivedPackages == null)
        {
            receivedPackages = new Queue<T>();
        }
        T[] packages = ReadBytes(bytes).ToArray();

        for (int i = 0; i < packages.Length; i++)
        {
            receivedPackages.Enqueue(packages[i]);
        }

    }

    public void Tick()
    {
        nextTick += (1.0f / (this.SendSpeed * Time.fixedDeltaTime));
        if (nextTick > 1.0f && Packages.Count > 0)
        {
            nextTick = 0f;
            if (OnRequiredPackageTransmit != null)
            {
                byte[] bytes = CreateBytes();
                Packages.Clear();
                OnRequiredPackageTransmit(bytes);
            }
        }
    }

    public T GetNextDataReceived()
    {
        if (receivedPackages == null || receivedPackages.Count == 0)
            return default(T);
        return receivedPackages.Dequeue();
    }

    byte[] CreateBytes()
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        using (MemoryStream ms = new MemoryStream())
        {
            binaryFormatter.Serialize(ms, this.Packages);
            return ms.ToArray();
        }
    }

    List<T> ReadBytes(byte[] bytes)
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        using (MemoryStream ms = new MemoryStream())
        {
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            return (List<T>)binaryFormatter.Deserialize(ms);
        }
    }

}
