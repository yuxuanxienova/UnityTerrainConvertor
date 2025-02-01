import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
from net.MsgBase import MsgBase
#----------------System Message----------------
class MsgPing(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "MsgPing"

class MsgPong(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "MsgPong"
#---------------Motion Capture Message----------------
class MotionStateMsg(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "MotionStateMsg"
        self.dof_pos_RH = []  
#---------------Agent Message----------------
class TransitionMsg(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "TransitionMsg"
        self.state = []
        self.action = []
        self.reward = []
        self.next_state = []
        self.trancated_flag = False 
class SampleActionRequestMsg(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "SampleActionRequestMsg"
        self.agent_id = -1
        self.state = []
class SampleActionResponseMsg(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "SampleActionResponseMsg"
        self.agent_id = -1
        self.action = []
class EpisodicRewardMsg(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "EpisodicRewardMsg"
        self.reward = 0
#---------------Test Message----------------
class MyMessage(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "MyMessage"
        self.content = ""
class MsgTest(MsgBase):
    def __init__(self):
        super().__init__()
        self.protoName = "MsgTest"
        self.str = ""
        self.num = 0