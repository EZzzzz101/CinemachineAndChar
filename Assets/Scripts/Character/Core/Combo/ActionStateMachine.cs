using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ActionStateMachine : StateMachine
{
    public int ComboIndex;// 当前第几招
    public PlayerController Owner { get; }

    public ActionNullState ActionNullState{get;}
    public ComboState ComboState {get;}
    public ActionStateMachine(PlayerController owner)
    {
        Owner = owner;
        ComboState = new ComboState(this);
        ActionNullState=new ActionNullState(this);
        // MovementNullState = new MovementNullState(this);
    }


}
