using System;
using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 运行时键值存储 — 节点之间共享数据
    /// 构造时根据 BTBlackboardSO 的 schema 初始化，并自动绑定 _owner / _transform / _animator
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> _values = new();

        /// <summary>根据黑板定义 + owner GameObject 构造</summary>
        public Blackboard(BTBlackboardSO definition, GameObject owner)
        {
            // 自动绑定内置键
            _values["_owner"] = owner;
            _values["_transform"] = owner.transform;
            _values["_animator"] = owner.GetComponent<Animator>();
        }

        /// <summary>写值</summary>
        public void Set<T>(string key, T value)
        {
            _values[key] = value;
        }

        /// <summary>读值（键不存在返回 default）</summary>
        public T Get<T>(string key)
        {
            if (_values.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return default;
        }

        /// <summary>键是否存在</summary>
        public bool Has(string key) => _values.ContainsKey(key);

        /// <summary>移除键</summary>
        public void Remove(string key) => _values.Remove(key);

        /// <summary>清空所有值</summary>
        public void Clear() => _values.Clear();
    }
}
