using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple =true)]
public class RPC : Attribute
{
    void AnimatorUpdateMode()
    {
        if (IntPtr j){
            rpcShoot();
        }
    }

    [RCP]
    void rpcShoot()
    {
    }
    
}
