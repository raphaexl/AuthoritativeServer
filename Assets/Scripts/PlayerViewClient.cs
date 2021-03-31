using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Reflection;
using System.Linq;

//used to send input of client to to the client script
//used to receive the position and applying it
//it will handle the id of the player and bool of is it mine or not
//it will handle the sending of state of animation and receiving of state of animation
// It is connected to the gameobject of each player
//It should have the list of all rpc's related to this gameObject
//we can call Rpc using this class * we can receive RPC from client or server class to search in the list of RPC

public class StateBuffer
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Timestamp;

    public override string ToString()
    {
        return $"Position({Position.x}, {Position.y}, {Position.z}) Rotation({Rotation.x}, {Rotation.y}, {Rotation.z}, {Rotation.w}) Timestamp {Timestamp}";
    }
}
public class PlayerViewClient : MonoBehaviour
{
    int                             _id;
    Tools.NInput                    _playerInput;
    List<MonoBehaviour>             _rpcMonoBehaviours;
    public List<StateBuffer>        StateBuffers;

    Vector3 _Position;
    Quaternion _Rotation;

    public bool isReady { get; set; }

    public bool isMine { get; set; }
    public int Id {
        get { return _id; }
        set { _id = value; }
    }

    private void SpecialChecks(bool _isLocalPlayer)
    {
        isMine = _isLocalPlayer;
        GetComponent<Player>().IsLocalPlayer = _isLocalPlayer;

        if (!_isLocalPlayer)
        {
            //Disable the camera of the enemy
            transform.GetChild(0).gameObject.GetComponent<Camera>().gameObject.SetActive(false);
            StateBuffers = new List<StateBuffer>();
        }
      
    }

    public void Spawn(bool isLocalPlayer, int id, Vector3 pos, Quaternion rot, Color color, Transform camTrans)
    {
        Id = id;
        GetComponent<PlayerController>().SetState(pos, rot, 0f);
        if (isLocalPlayer)
        {
            Vector3 pos_ = GetComponent<PlayerController>().cameraTrans.position;
            Quaternion rot_ = GetComponent<PlayerController>().cameraTrans.rotation;
            if (pos_ == camTrans.position && rot_ == camTrans.rotation)
            {
                Debug.Log("Everything is Fine");
            }
            else
            {
                Debug.Log($"Position : ({pos_.x}, {pos_.y}, {pos_.z}) vs Server Position : ({camTrans.position.x}, {camTrans.position.y}, {camTrans.position.z}) ");
                Debug.Log($"Rotation : ({rot_.x}, {rot_.y}, {rot_.z}) vs Server Rotation : ({camTrans.rotation.x}, {camTrans.rotation.y}, {camTrans.rotation.z}, {camTrans.rotation.w}) ");
               // GetComponent<PlayerController>().cameraTrans = camTrans;
            }
        }
       // transform.GetChild(1).GetComponent<MeshRenderer>().material.color = color;
        SpecialChecks(isLocalPlayer);
        Client.Instance.AddPlayerView(this);
    }

    private void Start()
    {
        //ReceiveRPC("openDoor");
        //ReceiveRPC("dropLife", 5);
        _playerInput = new Tools.NInput();
        _rpcMonoBehaviours = new List<MonoBehaviour>();
        RefreshMonoBehaviours();
        // printMonoRPCS();
    }

    public void UpdateInput(Tools.NInput nInput)
    {
        Client.Instance.setPlayerInputs(nInput);
    }

    public void ApplyServerState(Vector3 pos, Quaternion quaternion, float _animSpeed)
    {
        if (!isMine) { quaternion.eulerAngles = new Vector3(quaternion.eulerAngles.x, -quaternion.eulerAngles.y, quaternion.eulerAngles.z); }
        GetComponent<PlayerController>().SetState(pos, quaternion, _animSpeed);
    }

    public void Move(Tools.NInput nInput, float fpsTick)
    {
       HandleInput(nInput, fpsTick);
    }

    void processInterpolations()
    {
    }

    private void Update()
    {
       if (!isMine) {
            if (!Client.Instance.interpolation) { return; }
            processInterpolations();
            return;
        }
        UpdatePlayerInput();
        if (Input.GetKeyDown(KeyCode.R))
        {
            RPC("openDoor", RPCTarget.ALL, null);
        }
    }

    #region INPUT
    void UpdatePlayerInput()
    {
        /* _playerInput.InputX = Input.GetAxis("Horizontal");
         _playerInput.InputY = Input.GetAxis("Vertical");
         _playerInput.Jump = Input.GetButtonDown("Jump");
         _playerInput.MouseX = Input.GetAxis("Mouse X");
         _playerInput.MouseY = Input.GetAxis("Mouse Y");*/
        _playerInput.InputX = Input.GetAxisRaw("Horizontal");
        _playerInput.InputY = Input.GetAxisRaw("Vertical");
        _playerInput.Jump = Input.GetKey(KeyCode.Space);
        _playerInput.Run = Input.GetKey(KeyCode.LeftShift);
        _playerInput.MouseX = Input.GetAxis("Mouse X");
        _playerInput.MouseY = Input.GetAxis("Mouse Y");

        UpdateInput(_playerInput);
    }

    public void HandleInput(Tools.NInput nInput, float fpsTick)
    {
        GetComponent<PlayerController>().ApplyInput(nInput, fpsTick);
    }

    #endregion
    #region ANIMATION
    #endregion
    #region RPC

    public void RPC(string methodName, RPCTarget rpcTarget, params object[] param)
    {
        //It will be called by a script inside this gameobject to execute any function inside any component inside this gameObject
        Client.Instance.RequestRPC(Id, methodName, rpcTarget, param);
    }

    public void ReceiveRPC(string functionName, params object[] param)
    {
        for (int i = 0; i < _rpcMonoBehaviours.Count; i++)
        {
            MonoBehaviour mono = _rpcMonoBehaviours[i];
            if (mono == null)
            {
                Debug.LogError("Error Missing Monobehaviour on a GameObject");
                return;
            }
            MethodInfo methodInfo;
            methodInfo = FindMethodFromCache(functionName, param);
            if (methodInfo == null) { continue; } //But should never happen
            methodInfo?.Invoke(mono, param);
        }
    }
    void getRPCMonobehaviours()
    {

        MonoBehaviour[] monos = this.GetComponents<MonoBehaviour>();
        Type t;
        foreach (MonoBehaviour mono in monos)
        {
            t = mono.GetType();
            /*var meth = from m in t.GetMethods()
                       where m.GetCustomAttributes<RPC>().Any(a => a is RPCMethodAttribute)
                       select m;
            */
            IEnumerable<MethodInfo> m = t.GetMethods().Where(methodInfo => methodInfo.GetCustomAttributes<RPC>().Any());
            bool hasRpc = false;
            foreach (MethodInfo meth in m)
            { hasRpc = true; }
            if (hasRpc)
            { _rpcMonoBehaviours.Add(mono); }
        }
    }

    void printMonoRPCS()
    {
        foreach (MonoBehaviour mono in _rpcMonoBehaviours)
        {
            Debug.Log(mono.name);
        }
    }

    void RefreshMonoBehaviours()
    {
        getRPCMonobehaviours();
    }

    MethodInfo FindMethodFromCache(string functionName, params object[] parm)
    {
        MethodInfo method;

        method = null;
        _rpcMonoBehaviours.ForEach(mono =>
        {
            var rpcMethods = from m in mono.GetType().GetMethods()
                             where m.GetCustomAttributes<RPC>().Any()
                             select m;
            foreach (MethodInfo methodInfo in rpcMethods)
            {
                if (String.Equals(methodInfo.Name, functionName) == true) //Using String.Equals over == because String.Equals("a", "ab".substring(1)) vs "a" == "ab".substring()
                {
                    //Check Parameters
                    ParameterInfo[] parameterInfo = methodInfo.GetParameters();
                    if (parameterInfo.Length == parm.Length)
                    {
                        if (parameterInfo.Length == 0)
                        {
                            method = methodInfo;
                            break;
                        }
                        if (CheckParametersMatch(parameterInfo, parm))
                        {
                            method = methodInfo;
                            break;
                        }
                    }
                }
            }
        });
        return (method);
    }

    bool CheckParametersMatch(ParameterInfo[] parameterInfo, params object[] param)
    {
        bool match = false;

        // for (int i = 0; i < parameterInfo.Length; i++)
        for (int i = 0; i < param.Length; i++)
        {
            Type type = Type.GetType("System." + parameterInfo[i].ParameterType.Name);
            if (type == param[i].GetType())
            { match = true; }
            else
            {
                Debug.LogError($"Unrecognized parameter type : Check if the parameters match with the RPC Constructor {parameterInfo[i].Name}");
                match = false;
                break;
            }
        }
        return (match);
    }
    #endregion


    #region Other
    public static Quaternion SmoothDamp(Quaternion rot, Quaternion target, ref Quaternion deriv, float time)
    {
        if (Time.deltaTime < Mathf.Epsilon) return rot;
        // account for double-cover
        var Dot = Quaternion.Dot(rot, target);
        var Multi = Dot > 0f ? 1f : -1f;
        target.x *= Multi;
        target.y *= Multi;
        target.z *= Multi;
        target.w *= Multi;
        // smooth damp (nlerp approx)
        var Result = new Vector4(
            Mathf.SmoothDamp(rot.x, target.x, ref deriv.x, time),
            Mathf.SmoothDamp(rot.y, target.y, ref deriv.y, time),
            Mathf.SmoothDamp(rot.z, target.z, ref deriv.z, time),
            Mathf.SmoothDamp(rot.w, target.w, ref deriv.w, time)
        ).normalized;

        // ensure deriv is tangent
        var derivError = Vector4.Project(new Vector4(deriv.x, deriv.y, deriv.z, deriv.w), Result);
        deriv.x -= derivError.x;
        deriv.y -= derivError.y;
        deriv.z -= derivError.z;
        deriv.w -= derivError.w;

        return new Quaternion(Result.x, Result.y, Result.z, Result.w);
    }
    #endregion

    /*
    void UpdateEntitiesInterpolationState()
    {
        _remotePlayers.ForEach(remotePlayer =>
        {
            List<StateBuffer> stateBuffersToRemove = new List<StateBuffer>();
            for (int i = 0; i < remotePlayer.StateBuffers.Count - 2; i++)
            {
                if (remotePlayer.StateBuffers[i].InterpolationCompleted)
                {
                    stateBuffersToRemove.Add(remotePlayer.StateBuffers[i]);
                    Debug.Log($"Interpolation completed For : stabteBuffer {i}");
                }
                else
                {
                    //       Debug.Log($"Interpolation Not completed For : stabteBuffer {i}");
                }
            }
            stateBuffersToRemove.ForEach(stateBuffer =>
            {
                remotePlayer.StateBuffers.Remove(stateBuffer);
                Debug.Log("Something removed");

            });
            Debug.Log($"State Buffer Size Before : {remotePlayer.StateBuffers.Count} : To Remove : {stateBuffersToRemove.Count}");
            stateBuffersToRemove.Clear();
            Debug.Log($"State Buffer Size After : {remotePlayer.StateBuffers.Count} : To Remove : {stateBuffersToRemove.Count}");
        });
    }*/
}
