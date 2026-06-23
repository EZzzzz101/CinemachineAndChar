# CLAUDE_05 — 相机框架与 Animator 阻尼

## 1. 相机框架

### 分工
| 组件 | 职责 |
|------|------|
| `CinemachineVirtualCamera` + VCam 自带旋转 | 旋转 |
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

## 2. Animator.SetFloat 阻尼 vs Mathf.SmoothDamp

参考项目用 `Animator.SetFloat(name, value, dampTime, deltaTime)` 驱动速度：
```csharp
Owner.Animator.SetFloat("Movement", targetSpeed, 0.35f, Time.deltaTime);
```
比 `Mathf.SmoothDamp` 更适配 root motion，松键后速度自然衰减，不会秒停。
`SpeedSmoothTime` 调 0.35 左右效果接近参考项目。

## 3. 单例基类

```csharp
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
```
- 懒加载：`Instance` getter 找不到就自动 `new GameObject + AddComponent`
- `Awake` 设 `DontDestroyOnLoad`，重复则 `Destroy`
- 线程安全：`lock(_lock)`

## 4. 关键教训

- 相机旋转、缩放、方向计算分三个模块，互不干涉
- root motion 动画用 Animator 内置阻尼比代码平滑更好
