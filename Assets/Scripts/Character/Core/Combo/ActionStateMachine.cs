using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActionStateMachine : StateMachine
{
    public PlayerController Owner { get; }

    public ActionNullState ActionNullState{get;}
    public ATKingState ComboState {get;}
    public HitState HitState {get;}
    public ComboResuableData ResuableData{get;}
    public ActionStateMachine(PlayerController owner,ComboConfigSO config)
    {
        Owner = owner;
        ResuableData=new ComboResuableData{comboConfig=config};
        ComboState = new ATKingState(this);
        ActionNullState=new ActionNullState(this);
        HitState = new HitState(this);
    }
}
