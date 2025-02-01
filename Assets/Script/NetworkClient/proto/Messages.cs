using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//---------------System Message----------------
public class MsgPing : MsgBase
{
    public MsgPing() { protoName = "MsgPing"; }
}

public class MsgPong : MsgBase
{
    public MsgPong() { protoName = "MsgPong"; }
}
//---------------Agent Messages-----------------
public class TransitionMsg : MsgBase 
{
    public TransitionMsg() { protoName = "TransitionMsg"; }
    public float[] state;
    public float[] action;
    public float[] reward;
    public float[] next_state;
    public bool trancated_flag;
}

public class SampleActionRequestMsg : MsgBase
{
    public SampleActionRequestMsg() { protoName = "SampleActionRequestMsg"; }
    public int agent_id;
    public float[] state;
}

public class SampleActionResponseMsg : MsgBase
{
    public SampleActionResponseMsg() { protoName = "SampleActionResponseMsg"; }
    public int agent_id;
    public float[] action;
}

public class EpisodicRewardMsg : MsgBase
{
    public EpisodicRewardMsg() { protoName = "EpisodicRewardMsg"; }
    public float reward;
}

//-----------------Motion Capture Message---------------------
public class MotionStateMsg : MsgBase
{
    public MotionStateMsg() {  protoName = "MotionStateMsg"; }
    public int step;
    public float[] dof_pos_RH;
}
//--------------Test Message----------
public class MsgTest : MsgBase
{
    public MsgTest() { protoName = "MsgTest"; }
    public string str;
    public int num;
}
