using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Client : MonoBehaviour
{
    string SERVER_HOST = "127.0.0.1";
    int SERVER_PORT = 65432;

    void Start()
    {
        NetManager.AddEventListener(NetManager.NetEvent.ConnectSucc, OnConnectSucc);
        NetManager.AddEventListener(NetManager.NetEvent.ConnectFail, OnConnectFail);
        NetManager.AddEventListener(NetManager.NetEvent.Close, OnConnectClose);

        //----test----
        NetManager.AddMsgListener("MsgTest", OnMsgTest);
        //NetManager.AddMsgListener("SampleActionResponseMsg", OnSampleActionResponseMsg);
        //Connect
        NetManager.Connect(SERVER_HOST, SERVER_PORT);

    }

    //---------------------------连接服务端----------------------------------

    //连接成功回调
    public void OnConnectSucc(string err)
    {
        Debug.Log("OnConnectSucc");
    }

    //连接失败回调
    public void OnConnectFail(string err)
    {
        Debug.Log("OnConnectFail" + err);

    }

    //关闭连接
    public void OnConnectClose(string err)
    {
        Debug.Log("OnConnectClose");

    }

    //点击连接按钮
    public void OnConnectClick()
    {
        NetManager.Connect(SERVER_HOST, SERVER_PORT);
        //TODO:开始转圈，提示“连接中”
    }

    //主动关闭
    public void OnCloseCLick()
    {
        NetManager.Close();
    }


    public void Update()
    {
        NetManager.Update();


        if (Input.GetKeyDown(KeyCode.T))
        {

            TestMain();

        }
    }

    //-----------------------------------Publishers--------------------------------
    public void CallPublishMotionState(float[] dof_pos_RH) 
    {
        MotionStateMsg msg = new MotionStateMsg
        {
            dof_pos_RH = dof_pos_RH
        };
        NetManager.Send(msg);

    }


    //public void CallPublishEpisodicReward(float reward)
    //{
    //    EpisodicRewardMsg msg = new EpisodicRewardMsg
    //    {
    //        reward = reward
    //    };
    //    // Await the PublishAsync method to complete asynchronously
    //    NetManager.Send(msg);

    //}
    //public void CallPublishEventSampleAction(float[] obs_array, int id_agent)
    //{
    //    SampleActionRequestMsg msg = new SampleActionRequestMsg
    //    {
    //        agent_id = id_agent,
    //        state = obs_array
    //    };
    //    // Await the PublishAsync method to complete asynchronously
    //    NetManager.Send(msg);
    //}
    //public void CallPublishTransition(float[] state_tminus1, float[] action_tminus1, float[] reward_tminus1, float[] state_t, bool trancated_flag)
    //{
    //    TransitionMsg msg = new TransitionMsg
    //    {
    //        state = state_tminus1,
    //        action = action_tminus1,
    //        reward = reward_tminus1,
    //        next_state = state_t,
    //        trancated_flag = trancated_flag,
    //    };
    //    // Await the PublishAsync method to complete asynchronously
    //    NetManager.Send(msg);
    //}
    //----------------------------------Subscribers----------------------------------
    //public void OnSampleActionResponseMsg(MsgBase msgBase)
    //{
    //    SampleActionResponseMsg msg = (SampleActionResponseMsg)msgBase;

    //    int agent_id = msg.agent_id;
    //    List<float> floatList = new List<float>();
    //    foreach (var value in msg.action)
    //    {
    //        floatList.Add(value);
    //    }
    //    if (AgentManager.Instance.idToAgentDict.ContainsKey(agent_id))
    //    {
    //        AgentManager.Instance.idToAgentDict[agent_id].SetExecuteAction(floatList);
    //    }
    //    else
    //    {
    //        Debug.LogError("[ERROR][Client.cs] AgentManager.Instance.idToAgentDict does not contain" + agent_id);
    //    }



    //}
    //---------------------------------------Test------------------------------------ 
    //public void OnTestSendTrainsitionMsgClicked() 
    //{
    //    float[] state = { 1, 1, 1 };
    //    float[] action = { 1, 1, };
    //    float[] reward = { 1, };
    //    float[] next_state = { 1, 1, };
    //    bool trancated_flag = false;
    //    CallPublishTransition(state,action,reward,next_state,trancated_flag);
    //}
    public void TestMain()
    {

        MsgTest msg = new MsgTest();
        msg.str = "Hello";
        msg.num = 100;
        NetManager.Send(msg);

    }

    public void OnMsgTest(MsgBase msgBase)
    {
        MsgTest msg = (MsgTest)msgBase;
        Debug.Log("OnMsgTest: str=" + msg.str + " num=" + msg.num);
    }

}
