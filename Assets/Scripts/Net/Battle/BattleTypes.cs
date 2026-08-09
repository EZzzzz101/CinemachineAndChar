using System.Collections.Generic;

/// <summary>
/// 战斗会话共享数据类型 — 纯 C#，不依赖 UnityEngine，
/// 因此 BattleServer/BattleClient 可以在独立控制台工程里做自测。
/// </summary>

/// <summary>输入边沿标志：只有"瞬间意图"走边沿，连续状态（摇杆）单独上报</summary>
[System.Flags]
public enum BattleInputFlags
{
    None   = 0,
    Dash   = 1,   // 本帧按下闪避
    Attack = 2,   // 本帧按下攻击
    Sprint = 4,   // 持续按住冲刺（放移动包里一起上报）
}

/// <summary>
/// 动画状态压缩枚举 — 快照里只传这个 + 移动参数，远端据此"演出"角色动画。
/// 相比传 Animator 状态名，1 字节即可，且与动画机解耦。
/// </summary>
public enum BattleAnimState : byte
{
    Idle     = 0,
    Run      = 1,
    Sprint   = 2,
    Dash     = 3,
    TurnBack = 4,
    Attack   = 5,
    Hit      = 6,
    Dead     = 7,
}

/// <summary>一次输入上报：摇杆连续值 + 边沿标志</summary>
public struct BattleInputState
{
    public float MoveX;
    public float MoveZ;
    public BattleInputFlags Flags;
    // 客户端本地位置：主机用它在 Remote 生成时对齐（初始同步以客户端位置为起点），
    // 之后仍由主机模拟（主机权威），位置只用于"生成时的起点对齐"。
    public float PosX;
    public float PosY;
    public float PosZ;
}

/// <summary>单名玩家的状态快照条目（位置用 3 个 float，旋转只传 Yaw 够用）</summary>
public struct BattleSnapshotItem
{
    public string Name;
    public float PosX;
    public float PosY;
    public float PosZ;
    public float RotY;
    public float MoveSpeed;        // Animator "Movement" 参数，远端驱动走/跑
    public BattleAnimState Anim;   // 动画状态枚举
    public float HP;
    public float MaxHP;
    public bool Placeholder;       // true = 该玩家 Remote 尚未生成，位置只是出生点占位（客户端不能据此解锁）
}

/// <summary>一帧快照：tick 用于调试/后续换 UDP 的乱序检测</summary>
public class BattleSnapshot
{
    public int Tick;
    public List<BattleSnapshotItem> Items = new();
}

/// <summary>一次性战斗事件类型</summary>
public enum BattleEventType : byte
{
    Damage = 0,   // from=攻击者(如 Boss), to=被击玩家, v1=伤害值, v2=被击者新 HP
    Death  = 1,   // to=死亡玩家, v1=0
    Kick   = 2,   // 服务器踢人（保留）
}

/// <summary>战斗事件消息</summary>
public class BattleEventMsg
{
    public BattleEventType Type;
    public string From;
    public string To;
    public float V1;
    public float V2;
}

/// <summary>加入战斗的回执数据</summary>
public class BattleJoinInfo
{
    public bool Success;
    public string Reason;
    public int MySlot;              // 自己在成员表里的槽位（主机本地玩家=0）
    public List<string> Names = new();   // 完整成员表（含主机）
    // 服务器分配的出生点（主机权威：客户端直接用，不做本地推算）
    public float SpawnX;
    public float SpawnY;
    public float SpawnZ;
}
