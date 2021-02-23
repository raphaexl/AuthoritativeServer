using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClientUIController : MonoBehaviour
{
    public static  ClientUIController Instance;
  
    public  Text onClientConnected;

    public  Text onClientReceiveFromServer;



    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }
    }
}
