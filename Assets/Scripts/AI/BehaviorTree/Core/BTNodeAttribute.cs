using System;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 标记行为树节点，编辑器通过 TypeCache 自动发现
    /// </summary>
    public class BTNodeAttribute:Attribute
    {
        public string Name;       // 显示在搜索窗口里的名字，如 "等待"
        public string Category;   // 分类路径，如 "Action/时间"
        public string Description;// 可选描述，鼠标悬停时显示

        public BTNodeAttribute(string name, string category, string description = "")
        {
            Name = name;
            Category = category;
            Description = description;
        }
    }
}

