using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SingletonTool;
using System;

/// <summary>
/// 计时器管理器
/// </summary>
public class TimerManager : Singleton<TimerManager>
{
    //计时器队列
    private int TimerCount =10;
    private Queue<GameTimer> FreeTimers = new Queue<GameTimer>();
    private List<GameTimer> WorkingTimers= new List<GameTimer>();
    private void CreateTimer()
    {
        GameTimer timer =new GameTimer();
        FreeTimers.Enqueue(timer);
    }
    //初始化
    void Start()
    {
        for(int i=0;i<TimerCount;i++)
        {
            GameTimer timer =new GameTimer();
            FreeTimers.Enqueue(timer);
        }
    }

    void Update()
    {
        RunRealTimer();
    }

    /// <summary>
    /// 无返回值|拿一个不受timescale影响的计时器
    /// </summary>
    public void GetRealTimer(float duration, Action callback)
    {
        if(FreeTimers.Count==0) CreateTimer();
        GameTimer realTimer=FreeTimers.Dequeue();
        realTimer.Start(duration,callback);
        WorkingTimers.Add(realTimer);
    }

    /// <summary>
    /// 有返回值|拿一个不受timescale影响的计时器
    /// </summary>
    public GameTimer GetTimer(float duration, Action callback) 
    {
        if (FreeTimers.Count == 0) CreateTimer();
        var timer = FreeTimers.Dequeue();
        timer.Start(duration, callback);
        WorkingTimers.Add(timer);
        return timer;
    }

    public void Cancel(GameTimer timer)   // 提前取消
    {
        if (timer == null || !timer.IsRunning) return;
        timer.Cancel();
    }


    /// <summary>
    /// 运行计时器组
    /// </summary>
    public void RunRealTimer()
    {
        //遍历正在工作的计时器
         for (int i = WorkingTimers.Count - 1; i >= 0; i--)  // 倒着遍历才能删
        {
            WorkingTimers[i].Tick();
            if (!WorkingTimers[i].IsRunning)
            {
                FreeTimers.Enqueue(WorkingTimers[i]);
                WorkingTimers.RemoveAt(i);
            }
        }
    }
}
