using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 单机模拟机器人（玩家 2）— 纯网络模拟，不加载场景、没有画面。
///
/// 为什么需要它：不打包也能快速验证"大厅 → 战斗"全链路。
/// 它扮演"被邀请的第二个玩家"：
///   连大厅注册 → 收到邀请自动接受 → 收到开始战斗 → 连房主战斗服务器
///   → 上报无输入、收快照，把出生点/位置日志写到独立文件（SyncBotLog.txt），
///     与玩家 1 的 Console 日志分开（用户要求的"偏差日志输出到其他通道"）。
/// </summary>
public class SimBattleBot : MonoBehaviour
{
    [Header("大厅")]
    [SerializeField] private string lobbyIp = "127.0.0.1";
    [SerializeField] private int lobbyPort = 7777;
    [Tooltip("Bot 注册名（玩家 1 邀请时搜索这个名字）")]
    [SerializeField] private string botName = "2";

    [Header("战斗")]
    [SerializeField] private int battlePort = 7778;
    [Tooltip("Bot 位置/出生点日志文件（persistentDataPath 下）")]
    [SerializeField] private string logFile = "SyncBotLog.txt";

    private LobbyClient _lobby;
    private BattleClient _battle;
    private string _hostIp;
    private float _inputTimer;
    private float _retryTimer = 1f;
    private float _logTimer;         // 偏差日志节流
    private Vector3 _localPos;       // Bot 的"本地位置"（服务器出生点，Bot 不动）
    private bool _localPosSet;
    private StreamWriter _log;

    /// <summary>
    /// 任意场景启动即创建 Bot（登录/六分街就要在线上，等玩家 1 邀请），常驻跨场景。
    /// ⚠️ 双端（打包）测试前必须注释掉：否则每个 exe 都会自动创建 Bot 连大厅注册同名，
    /// 会造成重名冲突/假玩家。需要单机模拟时取消注释即可。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        // 双端测试：注释掉单机模拟 Bot（需要单机模拟时取消注释）
        // var go = new GameObject("SimBattleBot");
        // DontDestroyOnLoad(go);
        // go.AddComponent<SimBattleBot>();
    }

    private void Start()
    {
        _log = new StreamWriter(Path.Combine(Application.persistentDataPath, logFile), false, Encoding.UTF8);
        Log($"[Bot] 启动：连接大厅 {lobbyIp}:{lobbyPort}，注册名 {botName}");

        _lobby = new LobbyClient();
        _lobby.OnRegisterResult += r => Log($"[Bot] 大厅注册：{r}");
        _lobby.OnInvited += name =>
        {
            Log($"[Bot] 收到 {name} 的邀请 → 自动接受");
            _lobby.ReplyInvite(true);
        };
        _lobby.OnJoinedRoom += (hostName, guestName, ip, port, roomId) =>
        {
            _hostIp = ip;
            Log($"[Bot] 加入房间 {roomId}：房主 {hostName} @ {ip}:{port}");
        };
        _lobby.OnRoomStart += roomId =>
        {
            Log($"[Bot] 收到开始战斗（房间 {roomId}）→ 连接房主战斗服务器");
            ConnectBattle();
        };
        _lobby.Connect(lobbyIp, lobbyPort, botName);
    }

    private void ConnectBattle()
    {
        _battle = new BattleClient();
        _battle.OnJoined += info =>
        {
            _localPos = new Vector3(info.SpawnX, info.SpawnY, info.SpawnZ);   // 服务器权威出生点 = 本地位置
            _localPosSet = true;
            Log($"[Bot] 战斗加入：槽位 {info.MySlot}，成员 [{string.Join(",", info.Names)}]");
            Log($"[Bot] 服务器出生点 → {_localPos}");
        };
        _battle.OnSnapshot += snap => LogSnapshot(snap);
        _battle.Connect(_hostIp, battlePort, botName);
    }

    private void Update()
    {
        _lobby?.Poll();
        _battle?.Poll();

        // 竞态防御：房主还在加载 Main（BattleServer 未启动）时连 7778 会被拒，
        // 每 1s 重试，直到连上（房主服务器起来后自然成功）。
        if (_battle != null && !_battle.Connected)
        {
            _retryTimer -= Time.unscaledDeltaTime;
            if (_retryTimer <= 0f)
            {
                _retryTimer = 1f;
                Log($"[Bot] 战斗服务器未就绪，重试 {_hostIp}:{battlePort}");
                _battle.Connect(_hostIp, battlePort, botName);
            }
        }
        else if (_battle != null && _battle.Connected)
        {
            // Bot 无操作：定频上报空输入，主机正常模拟它（站着不动）
            _inputTimer -= Time.unscaledDeltaTime;
            if (_inputTimer <= 0f)
            {
                _inputTimer = 0.05f;
                // 带上自己的位置（服务器出生点），否则主机视角的"客户端上报位置"会是 (0,0,0)
                _battle.SendInput(0f, 0f, BattleInputFlags.None, _localPos.x, _localPos.y, _localPos.z);
            }
        }
    }

    /// <summary>
    /// [SyncDebug] Bot 偏差：自己的本地位置（服务器出生点，Bot 不动）vs 快照位置（主机模拟结果）。
    /// 每 0.5s 打一次，验证"服务器分配出生点 + 主机模拟"是否让两端一致。
    /// </summary>
    private void LogSnapshot(BattleSnapshot snap)
    {
        foreach (var item in snap.Items)
        {
            if (item.Name != botName) continue;
            _logTimer -= Time.unscaledDeltaTime;
            if (_logTimer > 0f) return;
            _logTimer = 0.5f;

            var snapPos = new Vector3(item.PosX, item.PosY, item.PosZ);
            float drift = _localPosSet ? Vector3.Distance(_localPos, snapPos) : -1f;
            Log($"[SyncDebug] Bot偏差 {drift:F2}m | 本地 {_localPos} vs 快照 {snapPos} | HP={item.HP}");
        }
    }

    private void Log(string msg)
    {
        Debug.Log(msg);
        if (_log != null)
        {
            _log.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
            _log.Flush();
        }
    }

    private void OnDestroy()
    {
        _lobby?.Disconnect();
        _battle?.Disconnect();
        _log?.Close();
    }
}
