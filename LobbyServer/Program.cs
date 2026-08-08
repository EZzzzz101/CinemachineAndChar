using System;
using System.Threading;

/// <summary>
/// 大厅服务器独立进程 — 真终端跑法：cd Server && dotnet run
/// 用法：dotnet run [--port 7777] [--selftest]
/// 服务器不依赖 Unity，就是纯 C# 控制台程序，和客户端共用 Assets/Scripts/Net 下的代码。
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        int port = 7777;
        bool selftest = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length) int.TryParse(args[++i], out port);
            if (args[i] == "--selftest") selftest = true;
        }
        if (selftest) return SelfTest(port);

        var server = new LobbyServer();
        if (!server.Start(port))
        {
            Console.WriteLine($"大厅服务器启动失败 :{port}");
            return 1;
        }
        Console.WriteLine($"大厅服务器已启动 :{port}（Ctrl+C 退出）");

        while (true)
        {
            server.Poll();
            Thread.Sleep(16);
        }
    }

    static int SelfTest(int port)
    {
        var server = new LobbyServer();
        if (!server.Start(port))
        {
            Console.WriteLine("[SELFTEST] 启动失败");
            return 1;
        }
        for (int i = 0; i < 60; i++)
        {
            server.Poll();
            Thread.Sleep(16);
        }
        server.Stop();
        Console.WriteLine("[SELFTEST] OK：大厅服务器可正常启动并轮询");
        return 0;
    }
}
