using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveInput : MonoBehaviour
{
    [Header("动画与手感")]
    [SerializeField] private float speedSmoothTime = 0.2f;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private Animator ani;
    private Vector2 moveInput;
    private float currentSpeed;  // 实际传给 Animator 的平滑值
    private float speed;

    private float _speedVelocity;

    void Start()
    {
        playerInput=GetComponent<PlayerInput>();
        ani=GetComponent<Animator>();
        moveAction=playerInput.actions.FindAction("Player/Move");

    }

    void Update()
    {
        //读取输入
        moveInput = moveAction.ReadValue<Vector2>();
        CalculateSpeed();
        UpdateRotation();
    }

    void OnAnimatorMove()
    {
        transform.position += ani.deltaPosition;
    }

    /// <summary>
    /// 速度计算
    /// </summary>
    void CalculateSpeed()
    {

        speed =moveInput.magnitude;
        float targetSpeed = speed > 0.1f ? 2f : 0f;
        bool isSprint = Keyboard.current.shiftKey.isPressed;

        if(isSprint) targetSpeed=3f;

        //平滑追赶
        currentSpeed=Mathf.SmoothDamp(currentSpeed,targetSpeed,ref _speedVelocity,speedSmoothTime);
        //设置Animator参数
        ani.SetFloat("Movement",currentSpeed);
        ani.SetBool("HasInput",speed>0.1f);
    }

    /// <summary>
    /// 更新转向
    /// </summary>
    void UpdateRotation()
    {
        ///取相机方向为正方向
        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward; forward.y = 0;
        Vector3 right   = cam.right;   right.y = 0;
        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

        if (speed > 0.1f)
        {            
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
