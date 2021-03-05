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


/*class RPCAttribute : Attribute { };
class RPCMethodAttribute : Attribute { }*/

class PlayerShoot
{
    public void ShootMySelf()
    {
        Console.WriteLine("The Player Shooting him self");
    }

    [RPC]
    public void ShootWithKalash()
    {
        Console.WriteLine(" Player Shoot with Kalash");
    }
    [RPC]
    public void ShootWithGun()
    {
        Console.WriteLine(" Player Shoot with Gun");
    }
}



public class PlayerViewClient : MonoBehaviour
{
    int _id;
    bool _isMine;
    List<MonoBehaviour> _rpcMonoBehaviours;

    public int Id {
        get { return _id; }
        set { _id = value; }
    }

    private void Start()
    {
        //ReceiveRPC("openDoor");
        //ReceiveRPC("dropLife", 5);
        _rpcMonoBehaviours = new List<MonoBehaviour>();
        RefreshMonoBehaviours();
        Client.Instance.AddPlayerView(this);
        // printMonoRPCS();
    }


    void getRPCMonobehaviours()
    {

        MonoBehaviour []monos = this.GetComponents<MonoBehaviour>();
        Type t;
        foreach (MonoBehaviour mono in monos)
        {
            t = mono.GetType();
            /*var meth = from m in t.GetMethods()
                       where m.GetCustomAttributes<RPC>().Any(a => a is RPCMethodAttribute)
                       select m;
            */
            IEnumerable < MethodInfo > m = t.GetMethods().Where(methodInfo => methodInfo.GetCustomAttributes<RPC>().Any());
            bool hasRpc = false;
            foreach (MethodInfo meth in m)
            {  hasRpc = true;}
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

    public void Move(Tools.NInput nInput, float fpsTick)
    {
      //  PlayerController playerController = monobehaviours.Find(script => script.GetType() == PlayerController);
       // PlayerController playerController = monobehaviours[0] as PlayerControllers;
      //  PlayerController playerController = (PlayerController) monobehaviours[0];
      //  playerController.ApplyInput(nInput, fpsTick);
    }


    public void RPC(string methodName, RPCTarget rpcTarget,params object[] param)
    {
        //It will be called by a script inside this gameobject to execute any function inside any component inside this gameObject
        Client.Instance.RequestRPC(Id, methodName, rpcTarget, param);
    }

    public void ReceiveRPC(string functionName, params object[] param)
    {
        //This function is called by client if we receive an rpc with a target == to id
        //and searchs for th same function name on the list of RPC functions in this gameObject and executes it 

        // object rpcClassInstance = Activator.CreateInstance(this);
        //MethodInfo method = FindMethodFromCache(.......);
        //foreach (MethodInfo methodInfo in _rpcMethodInfos)
        //{
        //    if (methodInfo.Name == functionName)
        //    {
        //        Debug.Log("Method : " + methodInfo.Name);
        ////        methodInfo.Invoke(this, parameters);
        //       // methodInfo.Invoke(rpcClassInstance, parameters);
        //    }
        //}

        for (int i = 0; i < _rpcMonoBehaviours.Count; i++)
        {
            MonoBehaviour mono = _rpcMonoBehaviours[i];
            if (mono == null) {
                Debug.LogError("Error Missing Monobehaviour on a GameObject");
                return;
            }
            MethodInfo methodInfo;
            methodInfo = FindMethodFromCache(functionName, param);
            if (methodInfo == null) { continue; } //But should never happen
            methodInfo?.Invoke(mono, param);
        }
    }

    void RefreshMonoBehaviours()
    {
        //Function that will have the list of monobehaviour with attribute type RPC
        getRPCMonobehaviours();
    }

    MethodInfo  FindMethodFromCache(string functionName, params object[] parm)
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
        //Will go through the monobehaviour we have , check for the name , number of parameters and type and return it
        return (method);
    }

    bool CheckParametersMatch(ParameterInfo[] parameterInfo,  params object[] param)
    {
        bool match = false;

       // for (int i = 0; i < parameterInfo.Length; i++)
         for (int i = 0; i < param.Length; i++)
         {
            //Debug.Log($"Parameter {i} type {parameterInfo[i].GetType()}");
            //Debug.Log($"param {param[i]} tyype {param[i].GetType()}");
            //Type realType = parameterInfo[i].ParameterType.GetElementType();
            //Debug.Log($"Parameter real type : {realType}");
            Type type = Type.GetType("System." + parameterInfo[i].ParameterType.Name);
            if (type == param[i].GetType())
            {
             //    Debug.Log("Type : " + param[i].GetType());
                match = true;
            }
            else
            {
                Debug.LogError($"Unrecognized parameter type : Check if the parameters match with the RPC Constructor {parameterInfo[i].Name}");
                match = false;
                break;
            }
        }
        return (match);
    }
}
