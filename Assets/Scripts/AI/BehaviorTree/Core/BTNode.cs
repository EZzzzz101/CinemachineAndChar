
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

        public BTResult Execute(Blackboard bb)
        {
            if(!_wasRunning)
                OnEnter(bb);   
            BTResult result =OnExecute(bb); //子类实现
            _wasRunning=(result==BTResult.Running);
            if(!_wasRunning)
                OnExit(bb);   //结束(成功或失败)

            return result;
        }

        protected abstract BTResult OnExecute(Blackboard bb);

        public virtual void OnEnter(Blackboard bb){}
        public virtual void OnExit(Blackboard bb){}

        public virtual void ResetNode(){ _wasRunning = false;}
    }

}
