using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerViewClient : Player
{
    int id;
    bool isMine;
    List<MonoBehaviour> monobehaviours;

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
}
