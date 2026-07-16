using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AI.BehaviourTree.Editor
{
    /// <summary>
    /// 节点类型元数据
    /// </summary>
    public class BTNodeTypeInfo
    {
        public Type Type;
        public string Name;            // 中文显示名
        public string Category;        // 分类路径，如 "Action/时间"
        public string Description;     // 描述文本
        public BTNodeCategory NodeCategory;
    }

    /// <summary>
    /// 反射扫描所有 [BTNode] 类型，只扫一次
    /// </summary>
    public static class BTNodeFactory
    {
        private static List<BTNodeTypeInfo> _cache;

        public static List<BTNodeTypeInfo> GetAllNodeTypes()
        {
            if (_cache != null) return _cache;

            _cache = new List<BTNodeTypeInfo>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException) { continue; }

                foreach (var type in types)
                {
                    if (!typeof(BTNode).IsAssignableFrom(type)) continue;
                    if (type.IsAbstract) continue;

                    var attr = type.GetCustomAttribute<BTNodeAttribute>();
                    if (attr == null) continue;

                    _cache.Add(new BTNodeTypeInfo
                    {
                        Type = type,
                        Name = attr.Name,
                        Category = attr.Category,
                        Description = attr.Description,
                        NodeCategory = GetNodeCategory(type)
                    });
                }
            }

            Debug.Log($"[BT Editor] 扫描到 {_cache.Count} 个节点类型");
            return _cache;
        }

        private static BTNodeCategory GetNodeCategory(Type type)
        {
            if (typeof(BTComposite).IsAssignableFrom(type))
                return BTNodeCategory.Composite;
            if (typeof(BTDecorator).IsAssignableFrom(type))
                return BTNodeCategory.Decorator;

            // 泛型基类 BTAction<T> / BTCondition<T>
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType)
                {
                    var def = baseType.GetGenericTypeDefinition();
                    if (def == typeof(BTAction<>)) return BTNodeCategory.Action;
                    if (def == typeof(BTCondition<>)) return BTNodeCategory.Condition;
                }
                baseType = baseType.BaseType;
            }

            return BTNodeCategory.Action;
        }
    }
}
