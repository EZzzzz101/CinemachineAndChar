using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class CameraManager : MonoBehaviour
{
    public CinemachineFreeLook freeLook;
    [SerializeField] private float sensitivity = 0.2f;
    public static CameraManager Instance{get;private set;}


    void Awake()
    {
        Instance=this;
    }

    void Update()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        freeLook.m_XAxis.Value += mouseDelta.x * sensitivity * 0.8f;

        freeLook.m_YAxis.Value += mouseDelta.y * sensitivity * 0.006f;
        freeLook.m_YAxis.Value = Mathf.Clamp01(freeLook.m_YAxis.Value);
    }

    public Vector3 GetMoveDir(Vector2 input)
    {
        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward;  forward.y = 0;
        Vector3 right   = cam.right;    right.y   = 0;
        return (forward * input.y + right * input.x).normalized;
    }


}
