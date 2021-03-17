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

public class Tools
{

    public struct NInput: System.IEquatable<NInput>
    {
        //Read /Write autoimplemented properties
        public float MouseX { get; set; }
        public float MouseY { get; set; }
        public float InputX { get; set; }
        public float InputY { get; set; }
        public bool Jump { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is NInput)
            {
                return true;
            }
            return false;
        }

        public bool Equals(NInput nInput) =>
            (this.InputX == nInput.InputX
                && this.InputY == nInput.InputY
                && this.Jump == nInput.Jump
                && this.MouseX == nInput.MouseX
                && this.MouseY == nInput.MouseY);

        public static bool operator ==(NInput lhs, NInput rhs) => lhs.Equals(rhs);

        public static bool operator !=(NInput lhs, NInput rhs) => !lhs.Equals(rhs);

        //TO BE IMLEMENTED
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

