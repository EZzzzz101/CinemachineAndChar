using UnityEngine;

/// <summary>
/// 角色连招配置 ScriptableObject
/// 右键 → Create → Combo → ComboConfig 创建
/// </summary>
[CreateAssetMenu(fileName = "ComboConfig", menuName = "Combo/ComboConfig")]
public class ComboConfigSO : ScriptableObject
{
    [Header("基本信息")]
    public string comboName;                  
    [Header("连招列表")]
    public ComboStepData[] steps;               // 每段数据
}
