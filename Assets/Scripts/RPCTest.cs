using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RPCTest : MonoBehaviour
{
    /*class RPCAttribute : Attribute { };
    class RPCMethodAttribute : Attribute { }*/

    class PlayerShoot
    {
        public void ShootMySelf()
        {
            System.Console.WriteLine("The Player Shooting him self");
        }

        [RPC]
        public void ShootWithKalash()
        {
            System.Console.WriteLine(" Player Shoot with Kalash");
        }
        [RPC]
        public void ShootWithGun()
        {
            System.Console.WriteLine(" Player Shoot with Gun");
        }
    }

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
}
