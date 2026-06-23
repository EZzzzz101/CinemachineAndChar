# CLAUDE_01 — 计时器、急停转向、相机框架

## 1. 计时器系统（TimerManager + GameTimer）

### 为什么不用协程
协程挂在 MonoBehaviour 上，物体销毁就断，无法提前取消，调试难追踪。计时器统一池化管理，受 timeScale 控制更精确。

### 架构
```
TimerManager (Singleton)
  ├── 空闲池 Queue<GameTimer>     ← 用完回收，避免 GC
  ├── 工作中 List<GameTimer>      ← Update 里遍历 Tick
  └── GetTimer(duration, callback) → 返回引用，可 Cancel
```
- `GameTimer.Start(duration, callback)` → 启动倒计时
- `GameTimer.Tick()` → 每帧 `_remaining -= Time.unscaledDeltaTime`
- `GameTimer.Cancel()` → 提前终止，不触发回调
- `TimerManager.Cancel(timer)` → 外部取消接口

### 两种计时模式
| 模式 | 倒计时用 | 场景 |
|------|---------|------|
| 普通 `Time.deltaTime` | `UpdateTimer()` | Combo 窗口、输入缓冲 |
| 真实 `Time.unscaledDeltaTime` | `UpdateRealTimer()` | 子弹时间（timeScale=0.1 时也正常走） |

### 池化回收关键点
- 用 `for (int i = list.Count - 1; i >= 0; i--)` **倒着遍历**才能安全 Remove
- `foreach` 遍历时修改集合会抛异常（迭代器检测版本号变化）

## 2. 急停转向（Sprint → TurnBack）

### 检测方式：输入方向夹角
```csharp
Vector2.Angle(_lastMoveValue, cur) > 150f  // 接近反向 → 触发转向
```
`_lastMoveValue` 只在两个值都 > 0.1f 时才更新，避免 (0,0) 污染。

### 触发方式：SetBool 走 Animator 过渡线
```csharp
Owner.Animator.SetBool("TurnBack", true);
```
**不是 CrossFade**。Animator 过渡线由动画师调曲线，比代码固定值更顺滑。

### 松键缓冲：Timer 替代协程
```csharp
// 松键 → 等 0.15s → 切 Idle
OnMoveCanceled → _idleTimer = TimerManager.Instance.GetTimer(0.15f, GoIdle);
// 缓冲期又按了 → Cancel → 留在 Sprint
OnMoveStarted  → TimerManager.Instance.Cancel(_idleTimer);
```

### 转身速度保持
缓冲期内 `GetTargetSpeed()` 应维持 3f（不是立刻就掉 0），否则 SmoothDamp 秒归零。

### 转身状态锁定旋转
`TurnBackState` 覆写 `Update()`，停掉 `HandleRotation()`，让动画自己转，避免代码旋转与动画冲突导致跳帧。

## 3. 相机框架

### 分工
| 组件 | 职责 |
|------|------|
| `CinemachineVirtualCamera` + `CinemachinePOV` | 旋转 |
| `CameraZoom` | 滚轮缩放 `FramingTransposer.m_CameraDistance` |
| `CameraManager` | 单例，只算 `GetMoveDir()`（输入转世界方向） |

### 缩放冲突
`CinemachineInputProvider` 的 Z Axis 也读滚轮，和 `CameraZoom` 打架。清掉 Z Axis 绑定即可。

### 移动方向计算
```csharp
// camera.forward 投影到水平面
forward.y = 0; right.y = 0;
return (forward * input.y + right * input.x).normalized;
```

## 4. 单例基类

```csharp
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
```
- 懒加载：`Instance` getter 找不到就自动 `new GameObject + AddComponent`
- `Awake` 设 `DontDestroyOnLoad`，重复则 `Destroy`
- 线程安全：`lock(_lock)`

## 5. Animator.SetFloat 阻尼 vs Mathf.SmoothDamp

参考项目用 `Animator.SetFloat(name, value, dampTime, deltaTime)` 驱动速度，比代码 `Mathf.SmoothDamp` 更适配 root motion：
```csharp
Owner.Animator.SetFloat("Movement", targetSpeed, 0.35f, Time.deltaTime);
```
`SpeedSmoothTime` 调到 0.35 后松键速度自然衰减，不会秒停。

## 6. 关键教训

- **转向用 Animator 过渡线，不用 CrossFade**：过渡曲线在 Unity 里调，代码只设 bool
- **动画状态里锁旋转**：root motion 动画和代码旋转不能同时跑
- **输入取消用 Timer 不用协程**：可取消、不受生命周期影响
- **遍历删除倒着走**：`for (i = count-1; i >= 0; i--)`
