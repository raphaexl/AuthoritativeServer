using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    protected string _userName;
    protected int _id;
    protected bool _isLocalPlayer;

    public string  UserName {
        get { return _userName; }
        set { _userName = value; }
    }

    public int ID
    {
        get { return _id; }
        set {
            _id = value;
        }
    }

    public bool IsLocalPlayer
    {
        get { return _isLocalPlayer; }
        set { _isLocalPlayer = value; }
    }

    public string playerType;

    private void Update()
    {
        playerType = _isLocalPlayer ? "Player" : "Enemy";
    }
}
