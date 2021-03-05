/*
 ** 
 * Is going to contain all required structs *
 * 
 */

public enum RPCTarget
{
    ALL,
    OTHERS
}

public struct RPCParametersTypes
{
    public const char FLOAT     = (char)0;
    public const char DOUBLE    = (char)1;
    public const char LONG      = (char)2;
    public const char ULONG     = (char)3;
    public const char INT       = (char)4;
    public const char UINT      = (char)5;
    public const char CHAR      = (char)6;
    public const char USHORT    = (char)7;
    public const char SHORT     = (char)8;
    public const char SBYTE     = (char)9;
    public const char BYTE      = (char)10;
    public const char BOOL      = (char)11;
    public const char STRING    = (char)12;
}

public struct RPCPacket
{
    public int              senderPeerId;
    public RPCTarget        rpcTarget;
    public string           methodName;
    public int              parameterLength;
    public string           parametersOrder;
    public object[]         parameters;
};

public class Tools
{
    public struct NInput
    {
        public float        mouseX;
        public float        mouseY;
        public float        inputX;
        public float        inputY;
        public bool         jump;
    }
}

