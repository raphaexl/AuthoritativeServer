using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

public enum PacketType
{
    Join,
    Spawn,
    Leave,
    Movement,
    ServerState,
    RPC,
}

public struct InputPacket : INetSerializable
{
    int id;
    float analogueOne;
    float analogueTwo;

    public void Deserialize(NetDataReader reader)
    {
        id = reader.GetInt();
        analogueOne = reader.GetFloat();
        analogueTwo = reader.GetFloat();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(id);
        writer.Put(analogueOne);
        writer.Put(analogueTwo);
    }
}


public struct PlayerState : INetSerializable
{
    PacketType type;
    public int Id;
    public Vector3 Position;
    public float Rotation;

    public void Deserialize(NetDataReader reader)
    {
        Id = reader.GetInt();
        Position.x = reader.GetFloat();
        Position.y = reader.GetFloat();
        Position.z = reader.GetFloat();
        //Position = reader.GetVector3();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Id);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
        writer.Put(Rotation);
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
    public PlayerState InitialPlayerState { get; set; }
}

public class PlayerLeavedPacket
{
    public byte Id { get; set; }
}

//Manual serializable packets
public struct SpawnPacket : INetSerializable
{
    public long PlayerId;
    public Vector3 Position;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(PlayerId);
        writer.Put(Position.x);
        writer.Put(Position.y);
        writer.Put(Position.z);
    }

    public void Deserialize(NetDataReader reader)
    {
        PlayerId = reader.GetLong();
        Position.x = reader.GetFloat();
        Position.y = reader.GetFloat();
        Position.z = reader.GetFloat();
    }
}


public class Packets : MonoBehaviour
{
}
