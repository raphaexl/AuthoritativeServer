using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public enum PacketType
{
    Join,
    Spawn,
    Leave,
    CameraSetup,
    Movement,
    ServerState,
    RPC,
}

public struct InputPacket : INetSerializable
{
    public  int Id;
    public  float mouseX;
    public  float mouseY;
    public  float inputX;
    public  float inputY;
    public  bool jump;

    public void Deserialize(NetDataReader reader)
    {
        Id = reader.GetInt();
        mouseX = reader.GetFloat();
        mouseY = reader.GetFloat();
        inputX = reader.GetFloat();
        inputY = reader.GetFloat();
        
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Id);
        writer.Put(mouseX);
        writer.Put(mouseY);
        writer.Put(inputX);
        writer.Put(inputY);
        writer.Put(jump);
    }
}


public struct PlayerStatePacket : INetSerializable
{
    //PacketType type;
    public int Id;
    public int lastProcessedInput;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 camPosition;
    public Quaternion camRotation;
    public float AnimSpeed;


    public void Deserialize(NetDataReader reader)
    {
        Id = reader.GetInt();
        lastProcessedInput = reader.GetInt();
        Position.x = reader.GetFloat();
        Position.y = reader.GetFloat();
        Position.z = reader.GetFloat();
        Rotation.x = reader.GetFloat();
        Rotation.y = reader.GetFloat();
        Rotation.z = reader.GetFloat();
        Rotation.w = reader.GetFloat();
        AnimSpeed = reader.GetFloat();
        camPosition.x = reader.GetFloat();
        camPosition.y = reader.GetFloat();
        camPosition.z = reader.GetFloat();
        camRotation.x = reader.GetFloat();
        camRotation.y = reader.GetFloat();
        camRotation.z = reader.GetFloat();
        camRotation.w = reader.GetFloat();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Id);
        writer.Put(lastProcessedInput);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
        writer.Put(AnimSpeed);
        writer.Put(camPosition.x);
        writer.Put(camPosition.y);
        writer.Put(camPosition.z);
        writer.Put(camRotation.x);
        writer.Put(camRotation.y);
        writer.Put(camRotation.z);
        writer.Put(camRotation.w);
    }
}

public class JoinPacket
{
    public string UserName { get; set; }
}

public class JoinAcceptPacket
{
    public int  Id { get; set; }
    public int  ServerTick { get; set; }
}

public class PlayerJoinedPacket
{
    public string UserName { get; set; }
    public bool NewPlayer { get; set; }
    public byte Health { get; set; }
    public ushort ServerTick { get; set; }
    public PlayerStatePacket InitialPlayerState { get; set; }
}

public class PlayerLeavedPacket
{
    public byte Id { get; set; }
}

public struct CameraSetupPacket : INetSerializable
{
    public int Id;
    public Vector3 Position;
    public Quaternion Rotation;

    public void Deserialize(NetDataReader reader)
    {
        Id = reader.GetInt();
        Position.x = reader.GetFloat();
        Position.y = reader.GetFloat();
        Position.z = reader.GetFloat();
        Rotation.x = reader.GetFloat();
        Rotation.y = reader.GetFloat();
        Rotation.z = reader.GetFloat();
        Rotation.w = reader.GetFloat();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Id);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
    }
}

//Manual serializable packets
public struct SpawnPacket : INetSerializable
{
    public int PlayerId;
    public Vector3 Position;
    public Quaternion Rotation;
    public Color Albedo;

    public Vector3 CameraPosition;
    public Quaternion CameraRotation;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(PlayerId);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation.x);
        writer.Put(Rotation.y);
        writer.Put(Rotation.z);
        writer.Put(Rotation.w);
        writer.Put(Albedo.r);
        writer.Put(Albedo.g);
        writer.Put(Albedo.b);
        writer.Put(CameraPosition.x);
        writer.Put(CameraPosition.y);
        writer.Put(CameraPosition.z);
        writer.Put(CameraRotation.x);
        writer.Put(CameraRotation.y);
        writer.Put(CameraRotation.z);
        writer.Put(CameraRotation.w);
    }

    public void Deserialize(NetDataReader reader)
    {
        PlayerId = reader.GetInt();
        Position.x = reader.GetFloat();
        Position.y = reader.GetFloat();
        Position.z = reader.GetFloat();
        Rotation.x = reader.GetFloat();
        Rotation.y = reader.GetFloat();
        Rotation.z = reader.GetFloat();
        Rotation.w = reader.GetFloat();
        Albedo.r = reader.GetFloat();
        Albedo.g = reader.GetFloat();
        Albedo.b = reader.GetFloat();
        CameraPosition.x = reader.GetFloat();
        CameraPosition.y = reader.GetFloat();
        CameraPosition.z = reader.GetFloat();
        CameraRotation.x = reader.GetFloat();
        CameraRotation.y = reader.GetFloat();
        CameraRotation.z = reader.GetFloat();
        CameraRotation.w = reader.GetFloat();
    }
}

public struct RPCPacket
{
    public int senderPeerId;
    public RPCTarget rpcTarget;
    public string methodName;
    public int parameterLength;
    public string parametersOrder;
    public object[] parameters;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(senderPeerId);
        writer.Put((int)rpcTarget);
        writer.Put(methodName);
        if (parameters == null)
        { writer.Put((int)0); }
        else
        {
            writer.Put(parameters.Length);
            writer.Put(parametersOrder);
            System.Array.ForEach<object>(parameters, parameter =>
            {
                System.Type type = parameter.GetType();
                if (type.Equals(typeof(float)))
                {
                    writer.Put((float)parameter);
                }
                else if (type.Equals(typeof(double)))
                {
                    writer.Put((double)parameter);
                }
                else if (type.Equals(typeof(long)))
                {
                    writer.Put((long)parameter);
                }
                else if (type.Equals(typeof(ulong)))
                {
                    writer.Put((ulong)parameter);
                }
                else if (type.Equals(typeof(int)))
                {
                    writer.Put((int)parameter);
                }
                else if (type.Equals(typeof(uint)))
                {
                    writer.Put((uint)parameter);
                }
                else if (type.Equals(typeof(char)))
                {
                    writer.Put((char)parameter);
                }
                else if (type.Equals(typeof(ushort)))
                {
                    writer.Put((ushort)parameter);
                }
                else if (type.Equals(typeof(short)))
                {
                    writer.Put((short)parameter);
                }
                else if (type.Equals(typeof(sbyte)))
                {
                    writer.Put((sbyte)parameter);
                }
                else if (type.Equals(typeof(byte)))
                {
                    writer.Put((byte)parameter);
                }
                else if (type.Equals(typeof(bool)))
                {
                    writer.Put((bool)parameter);
                }
                else if (type.Equals(typeof(string)))
                {
                    writer.Put((string)parameter);
                }
            });
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        senderPeerId = reader.GetInt();
        rpcTarget = (RPCTarget)reader.GetInt();
        methodName = reader.GetString();
        parameterLength = reader.GetInt();
        if (parameterLength > 0)
        {
            parametersOrder = reader.GetString();
            parameters = new object[parameterLength];
            int index = 0;
            var self = this;
            System.Array.ForEach<char>(parametersOrder.ToCharArray(), (typename) =>
            {
                switch (typename)
                {
                    case RPCParametersTypes.FLOAT:
                        self.parameters[index] = reader.GetFloat();
                        break;
                    case RPCParametersTypes.DOUBLE:
                        self.parameters[index] = reader.GetDouble();
                        break;
                    case RPCParametersTypes.LONG:
                        self.parameters[index] = reader.GetLong();
                        break;
                    case RPCParametersTypes.ULONG:
                        self.parameters[index] = reader.GetULong();
                        break;
                    case RPCParametersTypes.INT:
                        self.parameters[index] = reader.GetInt();
                        break;
                    case RPCParametersTypes.UINT:
                        self.parameters[index] = reader.GetUInt();
                        break;
                    case RPCParametersTypes.CHAR:
                        self.parameters[index] = reader.GetChar();
                        break;
                    case RPCParametersTypes.USHORT:
                        self.parameters[index] = reader.GetChar();
                        break;
                    case RPCParametersTypes.SHORT:
                        self.parameters[index] = reader.GetShort();
                        break;
                    case RPCParametersTypes.SBYTE:
                        self.parameters[index] = reader.GetSByte();
                        break;
                    case RPCParametersTypes.BYTE:
                        self.parameters[index] = reader.GetByte();
                        break;
                    case RPCParametersTypes.BOOL:
                        self.parameters[index] = reader.GetBool();
                        break;
                    case RPCParametersTypes.STRING:
                        self.parameters[index] = reader.GetString();
                        break;
                    default: break;
                }
                index++;
            });
        }
    }
};


public class Packets : MonoBehaviour
{
}
