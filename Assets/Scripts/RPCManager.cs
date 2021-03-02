using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using UnityEngine;

public class RPCManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void printRPCS()
    {
        var test =
        from t in Assembly.GetExecutingAssembly().GetTypes()
        where t.GetCustomAttributes(false).Any(a => a is RPC)
        select t;

       foreach (Type t in test)
        {
            Debug.Log(t.Name);
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
