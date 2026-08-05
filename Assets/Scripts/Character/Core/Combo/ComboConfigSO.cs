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

    [Tooltip("敌人层(LayerMask)。不设则查所有层，靠 IDamageable 接口过滤")]
    public LayerMask enemyLayer;  
                 
    [Header("连招列表")]
    public ComboStepData[] steps;               // 每段数据
}
