    using System;
using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 黑板键类型枚举
    /// </summary>
    public enum BTBlackboardKeyType
    {
        Int,
        Float,
        Bool,
        String,
        Vector3,
        GameObject,
        Transform
    }

    /// <summary>
    /// 黑板架构定义 ScriptableObject — 定义有哪些键，编辑器和运行时共用
    /// </summary>
    [CreateAssetMenu(menuName = "AI/Blackboard Definition", fileName = "NewBlackboard")]
    public class BTBlackboardSO : ScriptableObject
    {
        [SerializeField] private List<BlackboardKeyEntry> _keys = new();

        public IReadOnlyList<BlackboardKeyEntry> Keys => _keys;

        public void AddKey(string name, BTBlackboardKeyType type)
        {
            _keys.Add(new BlackboardKeyEntry { Name = name, Type = type });
        }

        public void RemoveKey(string name)
        {
            _keys.RemoveAll(k => k.Name == name);
        }

        /// <summary>黑板键定义条目</summary>
        [Serializable]
        public class BlackboardKeyEntry
        {
            public string Name;
            public BTBlackboardKeyType Type;
        }
    }

    /// <summary>
    /// 黑板键类型的扩展方法
    /// </summary>
    public static class BTBlackboardKeyTypeExtensions
    {
        public static Type ToSystemType(this BTBlackboardKeyType type) => type switch
        {
            BTBlackboardKeyType.Int => typeof(int),
            BTBlackboardKeyType.Float => typeof(float),
            BTBlackboardKeyType.Bool => typeof(bool),
            BTBlackboardKeyType.String => typeof(string),
            BTBlackboardKeyType.Vector3 => typeof(UnityEngine.Vector3),
            BTBlackboardKeyType.GameObject => typeof(UnityEngine.GameObject),
            BTBlackboardKeyType.Transform => typeof(UnityEngine.Transform),
            _ => typeof(object)
        };
    }
}
