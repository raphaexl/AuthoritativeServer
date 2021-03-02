using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//used to send input of client to to the client script
//used to receive the position and applying it
//it will handle the id of the player and bool of is it mine or not
//it will handle the sending of state of animation and receiving of state of animation
// It is connected to the gameobject of each player
//It should have the list of all rpc's related to this gameObject
//we can call Rpc using this class * we can receive RPC from client or server class to search in the list of RPC

public class PlayerViewClient : Player
{
    int id;
    bool isMine;
    List<MonoBehaviour> monobehaviours;
    private void Awake()
    {
        Client.instance.listofPlayerViews.Add(this);       
    }
    private void Start()
    {
        monobehaviours.Add(GetComponent<PlayerController>());
    }

    public void Move(Tools.NInput nInput, float fpsTick)
    {
      //  PlayerController playerController = monobehaviours.Find(script => script.GetType() == PlayerController);
       // PlayerController playerController = monobehaviours[0] as PlayerControllers;
        PlayerController playerController = (PlayerController) monobehaviours[0];
        playerController.ApplyInput(nInput, fpsTick);
    }


    public void RPC(string function,int Target,params object[] param)
    {
        //It will be called by a script inside this gameobject to execute any function inside any component inside this gameObject

        param[0].GetType();
    }

    public void ReceiveRPC(string function, params object[] param)
    {
        //This function is called by client if we receive an rpc with a target == to id
        //and searchs for th same function name on the list of RPC functions in this gameObject and executes it 

    }

}
