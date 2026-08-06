using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 装饰节点基类 — 只有一个子节点，修改其行为或返回值
    /// </summary>
    public abstract class BTDecorator : BTNode
    {
        public BTNode Child;

        public void SetChild(BTNode child)
        {
            Child = child;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            Child?.ResetNode();
        }

        public override void Abort(Blackboard bb)
        {
            if (IsRunning)
                Child?.Abort(bb);
            if (IsRunning)
                OnExit(bb);
            ResetNode();
        }
    }

    /// <summary>
    /// 泛型装饰节点基类 — 有配置参数的装饰节点继承这个
    /// 带 T Data + 自动序列化/反序列化
    /// </summary>
    public abstract class BTDecorator<T> : BTDecorator where T : new()
    {
        public T Data = new T();

        public string SerializeData() =>
            JsonUtility.ToJson(Data);

        public void DeserializeData(string json)
        {
            if (!string.IsNullOrEmpty(json))
                Data = JsonUtility.FromJson<T>(json) ?? new T();
        }
    }
}
