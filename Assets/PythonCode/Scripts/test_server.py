import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
from NetworkServer.net.ClientState import ClientState
from NetworkServer.proto.Messages import MsgPing, MsgPong, MsgTest, TransitionMsg, SampleActionRequestMsg, SampleActionResponseMsg
from NetworkServer.net.MsgBase import MsgBase
from NetworkServer.net.NetManager import NetManagerServer
import numpy as np
import torch
import PIL

import concurrent.futures
from torch.utils.tensorboard import SummaryWriter
import shutil
import time


#----------------------Node--------------------------

class Node:
    def __init__(self) -> None:

        SERVER_PORT = 65432;
        self.server = NetManagerServer()
        def MotionStateMsgHandler(client_state:ClientState , msg_base:MsgBase):
            print("[Msg Received][client:{0}]MotionStateMsg".format(client_state.client_address) + str(msg_base.dof_pos_RH))
        # def TransitionMsgHandler(client_state:ClientState , msg_base:MsgBase):
        #     print("[Msg Received][client:{0}]TransitionMsg".format(client_state.client_address) + str(msg_base.state) + str(msg_base.action) + str(msg_base.reward) + str(msg_base.next_state) + str(msg_base.trancated_flag))
        #     state = np.array(msg_base.state)
        #     action = np.array(msg_base.action)
        #     reward = np.array(msg_base.reward)
        #     next_state = np.array(msg_base.next_state)
        #     trancated_flag = msg_base.trancated_flag
        #     transition = (state,action,reward,next_state)
        #     if(state.shape[0]==self.state_dim and action.shape[0]==self.action_dim and next_state.shape[0]==self.state_dim):
        #         self.agent.memory.put(transition)
        # def SampleActionRequestMsgHandler(client:ClientState, msg_base):
        #     print("[Msg Received][client:{0}][agent_id:{1}]SampleActionMsg".format(client.client_address,msg_base.agent_id) + str(msg_base.state))
        #     agent_id = msg_base.agent_id
        #     state = np.array(msg_base.state)
            
        #     action = self.agent.get_action(state,train=False)
        #     response = SampleActionResponseMsg()
        #     response.agent_id = agent_id
        #     response.action = action.tolist()
        #     print("[Msg Send][client:{0}][agent_id:{1}]SampleActionResponseMsg".format(client.client_address,agent_id) + str(response.action))
        #     self.server.send(client, response)   
         
        self.server.add_msg_handler("MotionStateMsg",MotionStateMsgHandler)
        # self.server.add_msg_handler("TransitionMsg",TransitionMsgHandler)
        # self.server.add_msg_handler("SampleActionRequestMsg",SampleActionRequestMsgHandler)
        self.server.initialize(SERVER_PORT)
        
    def run(self):
        while True:
            current_time = time.time()
            #update client
            self.server.update()
            time.sleep(0.001)  # Sleep briefly to prevent high CPU usage
                    
if __name__ == "__main__":
    #initialize 
    node = Node()
    #Start 
    node.run()