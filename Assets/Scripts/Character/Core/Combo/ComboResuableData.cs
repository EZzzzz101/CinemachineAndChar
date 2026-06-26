/// <summary>
/// Combo 运行时共享数据 — ActionStateMachine 持有，所有 Combo 状态共享
/// </summary>
public class ComboResuableData
{
    // ——— 运行时状态 ———
    public int comboIndex;           // 当前第几段
    public bool hasBufferedInput;    // 是否在 combo 窗口内按了攻击

    public int currentATKIndex;       //单个动画的ATK索引 

    public ComboConfigSO comboConfig; //Combo数据

    //快捷方法
    public ComboStepData CurrentStep => comboConfig.steps[comboIndex];
    public string CurrentAnimationName => CurrentStep.animStateName;
    public float CurrentInputWindow => CurrentStep.inputWindowStart;

    // ——— combo 规则开关（由动画事件控制） ———
    public bool canInput = true;         // EnablePreInput() 打开 —— 允许"记录"玩家按下
    public bool canATK = true;           // CancelAttackColdTime() 打开 —— 允许"执行"缓冲指令
    public bool canLinkCombo = true;     // DisableLinkCombo() 关闭
    public bool canMoveInterrupt;        // EnableMoveInterrupt() 打开
}
