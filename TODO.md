# TODO — 待完成任务

## Combo 系统

- [ ] **PlayerComboState 基类：加 HandInput()**
  - 拆分输入读取（HandInput）和逻辑更新（Update）
  - `HandInput()` 虚方法，子类覆写做输入逻辑
- [ ] **ATKingState：HandInput + 动画事件接口**
  - 覆写 `HandInput()`：检查窗口 + `canInput` + buffer
  - `OnFireStarted` 简化：只设 `hasBufferedInput = true`
  - 动画事件接口：`EnablePreInput()` / `DisableLinkCombo()` / `EnableMoveInterrupt()` / `CancelAttackColdTime()`
  - 覆写 `Update()`：检查 canLinkCombo / canMoveInterrupt
- [ ] **PlayerController：移走 `_inputWindowStart`**（已在 ComboStepData 里）

## 移动系统

- [ ] **Run / Sprint 拆分**
  - 提取 `Move.canceled` 到 MovingState 基类
  - RunState 加 `Run.started → Sprint`
  - SprintState 补 `Move.canceled → Idle` + `Run.canceled → Run`
- [ ] **Dash 状态**（已完成基础框架，待跑通）
  - Dash 输入绑定：Shift 触发
  - `Run → Dash 动画 → Sprint`（动画驱动，已有 AnimationEnterBehaviour）
  - LocomotionStateMachine 注册 DashingState
- [ ] **Dash 后退状态**（DashBack 动画单独处理）

## 音效系统

- [x] CharacterAudio 单例
- [x] 闪避音效（前冲/后退）
- [x] 攻击音效（ComboNext + Animation Event）
- [x] 脚步声（前/后）
- [x] 收刀/入鞘音效
- [ ] **Inspector 配置**：拖 AudioClip 到对应字段
- [ ] **动画关键帧配 Event**：在攻击 clip 上加 PlayWhooshSound / PlayWeaponBackSound / PlayWeaponEndSound
- [ ] **对象池**（后续优化，Instantiate → Pool）

## 特效系统

- [x] CharacterVFX 单例
- [x] PlayVFX(string) 查表生成
- [x] VFXEntry 支持每特效独立的旋转/位置偏移
- [x] 跟随角色（父子化到 SpawnPoint）
- [ ] **Inspector 配置**：拖 VFX 预制体到映射表，设偏移
- [ ] **动画关键帧配 Event**：在攻击 clip 上加 PlayVFX("Slash_X")
- [ ] **对象池**（后续优化）

## 怪物 / 敌人系统

- [ ] 敌人基础类（Health / 受击 / 死亡）
- [ ] 攻击判定框 + 碰撞检测
- [ ] 受击反馈（VFX + 音效 + 硬直）

## EventBus

- [ ] UI 连接时使用（当前不需要）

## 对象池

- [ ] SFX_PoolManager（音效池）
- [ ] VFX_PoolManager（特效池）

## 其他

- [ ] comboConfigSO Inspector 可见性（`{get;private set;}` → `public` field）
- [ ] AnimationEnterBehaviour / AnimationExitBehaviour 在各动画 clip 上配置完整
