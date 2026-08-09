using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// 战斗会话自测（不依赖 Unity）— 验证 BattleServer ↔ BattleClient 全链路：
/// 加入 → 输入 → 快照 → 事件 → 离开。
/// 跑法：cd BattleServerTest && dotnet run
/// 为什么这样写：网络层是纯 C#，先脱离引擎用日志验证每条消息到达，
/// 再进 Unity 接画面——到那时网络层已经是可信的，调试范围只剩画面。
/// </summary>
class Program
{
    static int Main()
    {
        const int port = 7798;   // 避开 7777（大厅）/ 7778（战斗正式端口）
        var server = new BattleServer();
        server.SetHostName("Host");
        if (!server.Start(port))
        {
            Console.WriteLine("[SELFTEST] 服务器启动失败");
            return 1;
        }

        // ===== 阶段标记：每个阶段到达后置位，驱动主循环一步步推进 =====
        bool joined = false;
        bool inputSeen = false;
        string inputFrom = null;
        bool snapshotSent = false;
        bool snapshotSeen = false;
        bool eventSent = false;
        bool eventSeen = false;
        bool leftSeen = false;

        // 服务器侧观察点：输入到了吗？谁发的？有人离开吗？
        server.OnInput += (name, input) =>
        {
            inputSeen = true;
            inputFrom = name;
            Console.WriteLine($"[SELFTEST] 服务器收到输入：{name} move=({input.MoveX:F1},{input.MoveZ:F1}) flags={input.Flags}");
        };
        server.OnPlayerLeft += name =>
        {
            if (name == "Guest") leftSeen = true;
            Console.WriteLine($"[SELFTEST] 服务器感知离开：{name}");
        };

        // 客户端侧观察点：加入回执 / 快照 / 伤害事件
        var client = new BattleClient();
        client.OnJoined += info =>
        {
            joined = info.Success;
            Console.WriteLine($"[SELFTEST] 客户端加入：Success={info.Success} 槽位={info.MySlot} 成员=[{string.Join(",", info.Names)}]");
        };
        client.OnSnapshot += snap =>
        {
            snapshotSeen = true;
            foreach (var it in snap.Items)
                Console.WriteLine($"[SELFTEST] 客户端收到快照 tick={snap.Tick} {it.Name} pos=({it.PosX:F1},{it.PosZ:F1}) anim={it.Anim} hp={it.HP}");
        };
        client.OnBattleEvent += e =>
        {
            eventSeen = true;
            Console.WriteLine($"[SELFTEST] 客户端收到事件 {e.Type} from={e.From} to={e.To} 伤害={e.V1} 新HP={e.V2}");
        };

        client.Connect("127.0.0.1", port, "Guest");

        // ===== 主循环：每帧 Poll 两端（模拟 Unity 的 Update），按阶段推进 =====
        int snapshotTick = 0;
        var deadline = DateTime.Now.AddSeconds(8);
        while (DateTime.Now < deadline)
        {
            server.Poll();
            client.Poll();

            if (!joined)
            {
                // 阶段 1：等加入回执（什么也不做，只管 Poll）
            }
            else if (!inputSeen)
            {
                // 阶段 2：客户端上报输入（摇杆 + 闪避/攻击边沿）
                client.SendInput(1f, 0f, BattleInputFlags.Dash | BattleInputFlags.Attack | BattleInputFlags.Sprint);
            }
            else if (!snapshotSent)
            {
                // 阶段 3：服务器广播一帧快照（模拟 Unity 运行时采集了两名玩家的状态）
                var items = new List<BattleSnapshotItem>
                {
                    new BattleSnapshotItem { Name = "Host", PosX = 0f, PosY = 0f, PosZ = 0f, RotY = 0f, MoveSpeed = 0f, Anim = BattleAnimState.Idle, HP = 100f, MaxHP = 100f },
                    new BattleSnapshotItem { Name = "Guest", PosX = 1f, PosY = 0f, PosZ = 0f, RotY = 90f, MoveSpeed = 2f, Anim = BattleAnimState.Run, HP = 100f, MaxHP = 100f },
                };
                server.Tick(++snapshotTick, items);
                snapshotSent = true;
            }
            else if (!eventSent)
            {
                // 阶段 4：Boss 打中 Guest → 广播伤害事件（伤害值 + 新 HP）
                server.BroadcastEvent(BattleEventType.Damage, "Boss", "Guest", 10f, 90f);
                eventSent = true;
            }
            else if (!leftSeen)
            {
                // 阶段 5：客户端断开 → 服务器应感知并广播离开
                client.Disconnect();
            }
            else
            {
                break;   // 全链路走完
            }

            Thread.Sleep(10);
        }

        bool ok = joined && inputSeen && snapshotSeen && eventSeen && leftSeen && inputFrom == "Guest";
        if (!ok)
            Console.WriteLine($"[SELFTEST] FAIL: joined={joined} input={inputSeen}({inputFrom}) snapshot={snapshotSeen} event={eventSeen} left={leftSeen}");
        else
            Console.WriteLine("[SELFTEST] OK：加入 → 输入 → 快照 → 事件 → 离开 全链路通过");

        server.Stop();
        client.Disconnect();
        return ok ? 0 : 1;
    }
}
