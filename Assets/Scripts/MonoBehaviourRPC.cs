using UnityEngine;
using System.Collections;


public class MonoBehaviourRPC : MonoBehaviour
{
    public PlayerViewClient PlayerViewClient;
    // Use this for initialization
    void Awake()
    {
        PlayerViewClient = gameObject.GetComponent<PlayerViewClient>();
    }

    // Update is called once per frame
    void Update()
    {

    }


}
