using AI.BehaviourTree;
using UnityEngine;

/// <summary>
/// Boss 执行层（Motor）——行为树只设置状态标志，这里每帧读取并平滑执行。
/// 解决"行为树 tick（默认 0.1s）和游戏帧生成不一致"导致的旋转停顿：
///   行为树：bb["_faceTarget"] = true / false（用 BTSetFaceTarget 节点）
///   Motor  ：每帧 if(_faceTarget) 平滑旋转面向黑板中的 target
/// </summary>
public class BossMotor : MonoBehaviour
{
    [Header("面向")]
    [Tooltip("旋转速度（度/秒），默认 720")]
    public float RotateSpeed = 720f;

    [Tooltip("角度差低于此值视为已面向（度），默认 5")]
    public float AngleThreshold = 5f;

    [Tooltip("黑板上控制是否面向的标志键名")]
    public string FaceTargetKey = "_faceTarget";

    private BehaviorTreeRunner _bt;
    private Transform _self;

    void Awake()
    {
        _bt = GetComponent<BehaviorTreeRunner>();
        _self = transform;
    }

    void Update()
    {
        if (_bt?.Blackboard == null) return;

        var bb = _bt.Blackboard;
        if (!bb.Get<bool>(FaceTargetKey))
            return;

        RotateToTarget(bb);
    }

    /// <summary>每帧平滑旋转面向黑板中的 target（只转水平面，Y 不变）</summary>
    private void RotateToTarget(Blackboard bb)
    {
        Transform target = bb.Get<Transform>("target");
        if (target == null) return;

        Vector3 direction = target.position - _self.position;
        direction.y = 0f;
        if (direction.magnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float speed = RotateSpeed > 0f ? RotateSpeed : 720f;
        float angle = Quaternion.Angle(_self.rotation, targetRotation);
        float threshold = AngleThreshold > 0.5f ? AngleThreshold : 5f;
        if (angle <= threshold) return;

        // 距离衰减：越近转越慢，避免玩家贴身时鬼畜
        float distFactor = Mathf.Min(direction.magnitude / 2f, 1f);
        float t = Mathf.Min(speed * Time.deltaTime / 30f * distFactor, 1f);
        _self.rotation = Quaternion.Slerp(_self.rotation, targetRotation, t);
    }
}
