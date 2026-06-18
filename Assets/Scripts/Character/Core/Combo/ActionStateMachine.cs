using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ActionStateMachine : StateMachine
{
    public PlayerController Owner { get; }

    public ActionNullState ActionNullState{get;}
    public ATKingState ComboState {get;}
    public ComboResuableData ResuableData{get;}
    public ActionStateMachine(PlayerController owner)
    {
        Owner = owner;
        ResuableData=new ComboResuableData();
        ComboState = new ATKingState(this);
        ActionNullState=new ActionNullState(this);
    }
}
