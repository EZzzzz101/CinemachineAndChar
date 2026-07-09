using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AI.BehaviourTree
{
     /// <summary>
    /// 叶子-条件基类 (泛型 T) — 只判断条件，永远不返回 Running
    /// </summary>
    public abstract class BTCondition<T> : BTNode where T : new()
    {
        public T Data = new T();

        // 序列化 / 反序列化
        public string SerializeData() =>
            JsonUtility.ToJson(Data);

        public void DeserializeData(string json)
        {
            if (!string.IsNullOrEmpty(json))
                Data = JsonUtility.FromJson<T>(json) ?? new T();
        }
    }
}

