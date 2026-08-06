using UnityEngine;

namespace AI.BehaviourTree
{
    //返回状态枚举
    public enum BTResult
    {
        Running,
        Success,
        Failure
    }
    /// <summary>
    /// 抽象结点基类,每帧Tick入口 — 管理OnEnter/OnExit生命周期
    /// </summary>
    public abstract class BTNode
    {
        public string Guid {get;internal set;}
        private bool _wasRunning;

        /// <summary>当前是否处于 Running 状态（编辑器高亮用）</summary>
        public bool IsRunning { get; private set; }

        /// <summary>父节点引用（运行时由 WireChild 设置，编辑器高亮回溯路径用）</summary>
        public BTNode Parent { get; set; }

        public BTResult Execute(Blackboard bb)
        {
            if(!_wasRunning)
                OnEnter(bb);
            BTResult result = OnExecute(bb); //子类实现
            _wasRunning=(result==BTResult.Running);
            IsRunning = _wasRunning;  // 持续跟踪，供编辑器读取
            if(!_wasRunning)
                OnExit(bb);   //结束(成功或失败)

            return result;
        }

        protected abstract BTResult OnExecute(Blackboard bb);

        public virtual void OnEnter(Blackboard bb){}
        public virtual void OnExit(Blackboard bb){}

        public virtual void ResetNode(){ _wasRunning = false; IsRunning = false;}

        /// <summary>递归中止正在运行的子树：先沿 Running 路径自底向上调 OnExit(bb)，再 ResetNode() 清状态</summary>
        public virtual void Abort(Blackboard bb)
        {
            if (IsRunning)
                OnExit(bb);
            ResetNode();
        }
    }

}
