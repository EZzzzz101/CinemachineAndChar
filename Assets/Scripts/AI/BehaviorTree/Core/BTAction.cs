using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 叶子-动作基类 (泛型 T)
    /// </summary>
    public abstract class BTAction<T>:BTNode where T:new()
    {
        public T Data =new T();

        //序列化
        public string SerializeData()=>
    JsonUtility.ToJson(Data);
    
        //反序列化
        public void DeserializeData(string json)
        {
            if(!string.IsNullOrEmpty(json))
                Data=JsonUtility.FromJson<T>(json)??new T();
        }
    }
}

