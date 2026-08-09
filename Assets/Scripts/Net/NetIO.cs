using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 手写二进制编解码小工具（教学版）：
/// int = 4字节大端；bool = 1字节(0/1)；string = [4字节长度][UTF8字节]。
/// 所有消息 body 都用这套规则拼/拆，前后端必须一致。
/// </summary>
public static class NetIO
{
    public static void WriteInt(List<byte> buf, int value)
    {
        buf.Add((byte)(value >> 24));
        buf.Add((byte)(value >> 16));
        buf.Add((byte)(value >> 8));
        buf.Add((byte)value);
    }

    public static void WriteBool(List<byte> buf, bool value)
    {
        buf.Add(value ? (byte)1 : (byte)0);
    }

    public static void WriteString(List<byte> buf, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        WriteInt(buf, bytes.Length);
        buf.AddRange(bytes);
    }

    public static void WriteByte(List<byte> buf, byte value)
    {
        buf.Add(value);
    }

    /// <summary>单精度浮点 → 4 字节大端（IEEE754 位模式翻转）</summary>
    public static void WriteFloat(List<byte> buf, float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        WriteInt(buf, bits);
    }

    public static int ReadInt(byte[] buf, ref int offset)
    {
        int value = (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];
        offset += 4;
        return value;
    }

    public static bool ReadBool(byte[] buf, ref int offset)
    {
        bool value = buf[offset] != 0;
        offset += 1;
        return value;
    }

    public static string ReadString(byte[] buf, ref int offset)
    {
        int len = ReadInt(buf, ref offset);
        string value = Encoding.UTF8.GetString(buf, offset, len);
        offset += len;
        return value;
    }

    public static byte ReadByte(byte[] buf, ref int offset)
    {
        byte value = buf[offset];
        offset += 1;
        return value;
    }

    /// <summary>4 字节大端 → 单精度浮点</summary>
    public static float ReadFloat(byte[] buf, ref int offset)
    {
        int bits = ReadInt(buf, ref offset);
        return BitConverter.Int32BitsToSingle(bits);
    }
}

/// <summary>注册：客户端把自己的用户名发给服务器</summary>
public class MsgRegister
{
    public string UserName;

    public static byte[] Encode(string userName)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, userName);
        return buf.ToArray();
    }

    public static MsgRegister Decode(byte[] body)
    {
        int offset = 0;
        return new MsgRegister { UserName = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>注册回执：成功/失败 + 原因</summary>
public class MsgRegisterAck
{
    public bool Success;
    public string Reason;

    public static byte[] Encode(bool success, string reason)
    {
        var buf = new List<byte>();
        NetIO.WriteBool(buf, success);
        NetIO.WriteString(buf, reason);
        return buf.ToArray();
    }

    public static MsgRegisterAck Decode(byte[] body)
    {
        int offset = 0;
        return new MsgRegisterAck
        {
            Success = NetIO.ReadBool(body, ref offset),
            Reason = NetIO.ReadString(body, ref offset),
        };
    }
}

/// <summary>搜索玩家（只搜在线玩家）</summary>
public class MsgSearch
{
    public string Keyword;

    public static byte[] Encode(string keyword)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, keyword);
        return buf.ToArray();
    }

    public static MsgSearch Decode(byte[] body)
    {
        int offset = 0;
        return new MsgSearch { Keyword = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>搜索结果：找到（在线）或未找到</summary>
public class MsgSearchAck
{
    public bool Found;
    public string Name;

    public static byte[] Encode(bool found, string name)
    {
        var buf = new List<byte>();
        NetIO.WriteBool(buf, found);
        NetIO.WriteString(buf, name);
        return buf.ToArray();
    }

    public static MsgSearchAck Decode(byte[] body)
    {
        int offset = 0;
        return new MsgSearchAck
        {
            Found = NetIO.ReadBool(body, ref offset),
            Name = NetIO.ReadString(body, ref offset),
        };
    }
}

/// <summary>请求邀请某个玩家</summary>
public class MsgInvite
{
    public string TargetName;

    public static byte[] Encode(string targetName)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, targetName);
        return buf.ToArray();
    }

    public static MsgInvite Decode(byte[] body)
    {
        int offset = 0;
        return new MsgInvite { TargetName = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>服务器转发给被邀请者：谁邀请了你</summary>
public class MsgInviteNotify
{
    public string InviterName;

    public static byte[] Encode(string inviterName)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, inviterName);
        return buf.ToArray();
    }

    public static MsgInviteNotify Decode(byte[] body)
    {
        int offset = 0;
        return new MsgInviteNotify { InviterName = NetIO.ReadString(body, ref offset) };
    }
}

/// <summary>被邀请者的回复：接受/拒绝</summary>
public class MsgInviteAck
{
    public bool Accept;

    public static byte[] Encode(bool accept)
    {
        var buf = new List<byte>();
        NetIO.WriteBool(buf, accept);
        return buf.ToArray();
    }

    public static MsgInviteAck Decode(byte[] body)
    {
        int offset = 0;
        return new MsgInviteAck { Accept = NetIO.ReadBool(body, ref offset) };
    }
}

/// <summary>服务器通知双方进房：带房主的战斗地址</summary>
public class MsgJoinRoom
{
    public string HostName;
    public string GuestName;
    public string HostIp;
    public int HostPort;
    public int RoomId;

    public static byte[] Encode(string hostName, string guestName, string hostIp, int hostPort, int roomId)
    {
        var buf = new List<byte>();
        NetIO.WriteString(buf, hostName);
        NetIO.WriteString(buf, guestName);
        NetIO.WriteString(buf, hostIp);
        NetIO.WriteInt(buf, hostPort);
        NetIO.WriteInt(buf, roomId);
        return buf.ToArray();
    }

    public static MsgJoinRoom Decode(byte[] body)
    {
        int offset = 0;
        return new MsgJoinRoom
        {
            HostName = NetIO.ReadString(body, ref offset),
            GuestName = NetIO.ReadString(body, ref offset),
            HostIp = NetIO.ReadString(body, ref offset),
            HostPort = NetIO.ReadInt(body, ref offset),
            RoomId = NetIO.ReadInt(body, ref offset),
        };
    }
}

/// <summary>邀请结果：服务器通知邀请人（对方接受/拒绝/不在线）</summary>
public class MsgInviteResult
{
    public bool Accepted;
    public string Reason;

    public static byte[] Encode(bool accepted, string reason)
    {
        var buf = new List<byte>();
        NetIO.WriteBool(buf, accepted);
        NetIO.WriteString(buf, reason);
        return buf.ToArray();
    }

    public static MsgInviteResult Decode(byte[] body)
    {
        int offset = 0;
        return new MsgInviteResult
        {
            Accepted = NetIO.ReadBool(body, ref offset),
            Reason = NetIO.ReadString(body, ref offset),
        };
    }
}

/// <summary>开始战斗：房主请求 → 大厅转发给房间其他成员，大家同时进战斗场景</summary>
public class MsgRoomStart
{
    public int RoomId;

    public static byte[] Encode(int roomId)
    {
        var buf = new List<byte>();
        NetIO.WriteInt(buf, roomId);
        return buf.ToArray();
    }

    public static MsgRoomStart Decode(byte[] body)
    {
        int offset = 0;
        return new MsgRoomStart { RoomId = NetIO.ReadInt(body, ref offset) };
    }
}
