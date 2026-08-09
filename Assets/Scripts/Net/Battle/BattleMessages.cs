using System.Collections.Generic;

/// <summary>
/// 战斗消息编解码 — 沿用大厅协议的手写二进制风格（NetIO），
/// 字段顺序就是协议契约，前后端必须一致；只往后加字段，不要改已有字段顺序。
/// </summary>

/// <summary>客户端→房主：请求加入战斗</summary>
public class MsgBattleJoin
{
    public string Name;

    public static byte[] Encode(string name)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, name);
        return buf.ToArray();
    }

    public static MsgBattleJoin Decode(byte[] body)
    {
        int offset = 0;
        return new MsgBattleJoin { Name = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>房主→客户端：加入结果 + 成员表（客户端据此知道"世界里有谁"）</summary>
public class MsgBattleJoinAck
{
    public bool Success;
    public string Reason;
    public int MySlot;
    public List<string> Names = new();
    public float SpawnX;
    public float SpawnY;
    public float SpawnZ;

    public static byte[] Encode(bool success, string reason, int mySlot, List<string> names,
                                float spawnX, float spawnY, float spawnZ)
    {
        var buf = new List<byte>();
        NetIO.WriteBool(buf, success);
        NetIO.WriteString(buf, reason);
        NetIO.WriteInt(buf, mySlot);
        NetIO.WriteInt(buf, names.Count);
        foreach (var n in names) NetIO.WriteString(buf, n);
        NetIO.WriteFloat(buf, spawnX);
        NetIO.WriteFloat(buf, spawnY);
        NetIO.WriteFloat(buf, spawnZ);
        return buf.ToArray();
    }

    public static MsgBattleJoinAck Decode(byte[] body)
    {
        int offset = 0;
        var msg = new MsgBattleJoinAck
        {
            Success = NetIO.ReadBool(body, ref offset),
            Reason = NetIO.ReadString(body, ref offset),
            MySlot = NetIO.ReadInt(body, ref offset),
        };
        int count = NetIO.ReadInt(body, ref offset);
        for (int i = 0; i < count; i++)
            msg.Names.Add(NetIO.ReadString(body, ref offset));
        msg.SpawnX = NetIO.ReadFloat(body, ref offset);
        msg.SpawnY = NetIO.ReadFloat(body, ref offset);
        msg.SpawnZ = NetIO.ReadFloat(body, ref offset);
        return msg;
    }
}

/// <summary>房主→已加入的客户端：新玩家加入（让客户端可以"晚到就补个幽灵"）</summary>
public class MsgBattleJoinNotify
{
    public string Name;

    public static byte[] Encode(string name)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, name);
        return buf.ToArray();
    }

    public static MsgBattleJoinNotify Decode(byte[] body)
    {
        int offset = 0;
        return new MsgBattleJoinNotify { Name = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>房主→其余客户端：有人离开</summary>
public class MsgBattleLeaveNotify
{
    public string Name;

    public static byte[] Encode(string name)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, name);
        return buf.ToArray();
    }

    public static MsgBattleLeaveNotify Decode(byte[] body)
    {
        int offset = 0;
        return new MsgBattleLeaveNotify { Name = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>客户端→房主：输入上报（seq 供调试/后续换 UDP 乱序检测；TCP 下理论有序）</summary>
public class MsgBattleInput
{
    public int Seq;
    public float MoveX;
    public float MoveZ;
    public BattleInputFlags Flags;
    public float PosX;
    public float PosY;
    public float PosZ;

    public static byte[] Encode(int seq, float moveX, float moveZ, BattleInputFlags flags,
                                float posX, float posY, float posZ)
    {
        var buf = new List<byte>();
        NetIO.WriteInt(buf, seq);
        NetIO.WriteFloat(buf, moveX);
        NetIO.WriteFloat(buf, moveZ);
        NetIO.WriteByte(buf, (byte)flags);
        NetIO.WriteFloat(buf, posX);
        NetIO.WriteFloat(buf, posY);
        NetIO.WriteFloat(buf, posZ);
        return buf.ToArray();
    }

    public static MsgBattleInput Decode(byte[] body)
    {
        int offset = 0;
        return new MsgBattleInput
        {
            Seq = NetIO.ReadInt(body, ref offset),
            MoveX = NetIO.ReadFloat(body, ref offset),
            MoveZ = NetIO.ReadFloat(body, ref offset),
            Flags = (BattleInputFlags)NetIO.ReadByte(body, ref offset),
            PosX = NetIO.ReadFloat(body, ref offset),
            PosY = NetIO.ReadFloat(body, ref offset),
            PosZ = NetIO.ReadFloat(body, ref offset),
        };
    }
}

/// <summary>房主→客户端：主机权威状态快照（定频下发）</summary>
public class MsgBattleSnapshot
{
    public int Tick;
    public List<BattleSnapshotItem> Items = new();

    public static byte[] Encode(int tick, List<BattleSnapshotItem> items)
    {
        var buf = new List<byte>();
        NetIO.WriteInt(buf, tick);
        NetIO.WriteInt(buf, items.Count);
        foreach (var it in items)
        {
            NetIO.WriteString(buf, it.Name);
            NetIO.WriteFloat(buf, it.PosX);
            NetIO.WriteFloat(buf, it.PosY);
            NetIO.WriteFloat(buf, it.PosZ);
            NetIO.WriteFloat(buf, it.RotY);
            NetIO.WriteFloat(buf, it.MoveSpeed);
            NetIO.WriteByte(buf, (byte)it.Anim);
            NetIO.WriteFloat(buf, it.HP);
            NetIO.WriteFloat(buf, it.MaxHP);
            NetIO.WriteBool(buf, it.Placeholder);
        }
        return buf.ToArray();
    }

    public static MsgBattleSnapshot Decode(byte[] body)
    {
        int offset = 0;
        var msg = new MsgBattleSnapshot { Tick = NetIO.ReadInt(body, ref offset) };
        int count = NetIO.ReadInt(body, ref offset);
        for (int i = 0; i < count; i++)
        {
            msg.Items.Add(new BattleSnapshotItem
            {
                Name = NetIO.ReadString(body, ref offset),
                PosX = NetIO.ReadFloat(body, ref offset),
                PosY = NetIO.ReadFloat(body, ref offset),
                PosZ = NetIO.ReadFloat(body, ref offset),
                RotY = NetIO.ReadFloat(body, ref offset),
                MoveSpeed = NetIO.ReadFloat(body, ref offset),
                Anim = (BattleAnimState)NetIO.ReadByte(body, ref offset),
                HP = NetIO.ReadFloat(body, ref offset),
                MaxHP = NetIO.ReadFloat(body, ref offset),
                Placeholder = NetIO.ReadBool(body, ref offset),
            });
        }
        return msg;
    }
}

/// <summary>房主→客户端：一次性战斗事件（伤害/死亡）</summary>
public class MsgBattleEvent
{
    public BattleEventType Type;
    public string From;
    public string To;
    public float V1;
    public float V2;

    public static byte[] Encode(BattleEventType type, string from, string to, float v1, float v2)
    {
        var buf = new List<byte>();
        NetIO.WriteByte(buf, (byte)type);
        NetIO.WriteString(buf, from);
        NetIO.WriteString(buf, to);
        NetIO.WriteFloat(buf, v1);
        NetIO.WriteFloat(buf, v2);
        return buf.ToArray();
    }

    public static MsgBattleEvent Decode(byte[] body)
    {
        int offset = 0;
        return new MsgBattleEvent
        {
            Type = (BattleEventType)NetIO.ReadByte(body, ref offset),
            From = NetIO.ReadString(body, ref offset),
            To = NetIO.ReadString(body, ref offset),
            V1 = NetIO.ReadFloat(body, ref offset),
            V2 = NetIO.ReadFloat(body, ref offset),
        };
    }
}
