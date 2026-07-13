using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    private Animator ani;

    void Start()
    {
        ani=GetComponent<Animator>();
    }
    void OnAnimatorMove()
    {
        transform.position += ani.deltaPosition;
    }
}
