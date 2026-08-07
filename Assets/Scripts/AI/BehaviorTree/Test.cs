using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [Tooltip("贴地吸附力度：每帧向下按压，防止怪物跳跃根运动把碰撞体抬到玩家头上")]
    [SerializeField] private float _groundSnapSpeed = 2f;

    private Animator ani;
    private CharacterController _cc;

    void Start()
    {
        ani = GetComponent<Animator>();
        _cc = GetComponent<CharacterController>();
    }
    void OnAnimatorMove()
    {
        // 走 CharacterController 施加根运动 + 贴地吸附（和玩家一致）：
        // 撞到玩家/障碍被挡住不穿模；跳跃动画的向上分量被往下按成低跳，不会跳到玩家头上
        if (_cc != null)
            _cc.Move(ani.deltaPosition + Vector3.down * _groundSnapSpeed * Time.deltaTime);
        else
            transform.position += ani.deltaPosition;
    }
}
