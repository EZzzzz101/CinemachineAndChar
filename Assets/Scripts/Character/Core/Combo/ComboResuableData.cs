using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComboResuableData
{
    // ——— 运行时状态 ———
    public int comboIndex;           // 当前第几段
    public bool hasBufferedInput;    // 是否在 combo 窗口内按了攻击

    // ——— 动画列表 ———（后续可改成 ScriptableObject）
    public string[] comboAnims = { "Anbi_Normal_1", "Anbi_Normal_2", "Anbi_Normal_3" };

    // ——— combo 规则开关 ———+
    public bool canLinkCombo = true;     // 是否允许连下一段
    public bool canMoveInterrupt;        // 是否允许移动打断
}

