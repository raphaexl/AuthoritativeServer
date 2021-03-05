using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RPCTest : MonoBehaviour
{

    [RPC]
    public void openDoor()
    {
        Debug.Log("I want the door opened");
    }

    [RPC]
    public void dropLife(int amount)
    {
        Debug.LogFormat("I want to drop {0} lives", amount);
    }


    [RPC]
    public void rpcShoot()
    {
        Debug.Log("I want to kill the enemy");
    }

    public void notAnRpc()
    {
        Debug.Log("I'm niot an RPC");
    }


    PlayerViewClient PlayerViewClient;

    private void Awake()
    {
        PlayerViewClient = GetComponent<PlayerViewClient>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayerViewClient.RPC("openDoor", RPCTarget.ALL, null);
        }
    }
}
