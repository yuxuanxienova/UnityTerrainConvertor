import os
import sys
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))
from NetworkServer.net.ClientState import ClientState
from NetworkServer.proto.Messages import MsgPing, MsgPong, MsgTest, TransitionMsg, SampleActionRequestMsg, SampleActionResponseMsg
from NetworkServer.net.MsgBase import MsgBase
from NetworkServer.net.NetManager import NetManagerServer
import numpy as np
import time

#navgym
from nav_gym.nav_legged_gym.envs.config_locomotion_env import LocomotionEnvCfg
from nav_gym.nav_legged_gym.envs.locomotion_env import LocomotionEnv
from nav_gym.learning.runners.on_policy_runner import OnPolicyRunner
from nav_gym.nav_legged_gym.train.config_train_locomotion import TrainConfig
from nav_gym.nav_legged_gym.utils.conversion_utils import class_to_dict
from nav_gym import NAV_GYM_ROOT_DIR
from nav_gym.learning.datasets.motion_loader import MotionLoader
# isaac-gym
from isaacgym import gymtorch
from isaacgym.torch_utils import (
    quat_rotate,
    quat_rotate_inverse,
    get_euler_xyz,
    quat_from_euler_xyz,
)
from isaacgym import gymtorch, gymapi, gymutil
#python
import torch
import os
import time

#----------------------Node--------------------------

class Node:
    def __init__(self) -> None:
        #------------server----------------
        SERVER_PORT = 65432
        self.server = NetManagerServer()
        def MotionStateMsgHandler(client_state:ClientState , msg_base:MsgBase):
            print("[Msg Received][client:{0}]MotionStateMsg[Step:{1}]".format(client_state.client_address,msg_base.step)  + str(msg_base.dof_pos_RH)) 
            self.dof_pos_RH = torch.tensor(msg_base.dof_pos_RH,device="cuda") 
        self.server.add_msg_handler("MotionStateMsg",MotionStateMsgHandler)
        self.server.initialize(SERVER_PORT)

        #------------navgym----------------
        log_dir = None
        train_cfg = TrainConfig
        train_cfg_dict = class_to_dict(train_cfg)

        #Override the default config
        env_cfg = LocomotionEnvCfg()
        env_cfg.env.num_envs = min(env_cfg.env.num_envs, 1)
        env_cfg.robot.randomization.randomize_friction = False
        env_cfg.randomization.push_robots = False
        # env_cfg.terrain_unity.terrain_file = "/terrain/Plane1.obj"
        # env_cfg.terrain_unity.translation = [0.0, 0.0, -0.2]
        env_cfg.terrain_unity.env_origin_pattern = "point"

        env_cfg.gym.viewer.eye = (3.0, 3.0, 3.0)

        env = LocomotionEnv(env_cfg)
        env.set_flag_enable_reset(False)
        env.set_flag_enable_resample(False)
        runner = OnPolicyRunner(env, train_cfg_dict, log_dir=log_dir, device="cuda:0")
        policy = runner.get_inference_policy()
        obs, extras = env.reset()
        
        #Load the Motion Data
        datasets_root = os.path.join(NAV_GYM_ROOT_DIR + "/resources/fld/motion_data/")
        motion_names = ["motion_data_pace1.0.pt","motion_data_walk01_0.5.pt","motion_data_walk03_0.5.pt","motion_data_canter02_1.5.pt"]

        motion_loader = MotionLoader(
            device="cuda",
            file_names=motion_names,
            file_root=datasets_root,
            corruption_level=0.0,
            reference_observation_horizon=2,
            test_mode=False,
            test_observation_dim=None
        )
        motion_idx = 3
        num_motion_clips, num_steps, motion_features_dim = motion_loader.data_list[motion_idx].size()
        #-------------Store----------------
        self.num_updates = 0
        self.env = env
        self.policy = policy
        self.motion_loader = motion_loader
        self.motion_idx = motion_idx
        self.obs = obs
        self.dof_pos_RH = torch.zeros(1, 3, device="cuda")
        
    def run(self):
        while True:
            self.num_updates += 1
            current_time = time.time()
            #update client
            self.update_nav_gym()
            if self.num_updates % 50 == 0:
                self.server.update()
    def update_nav_gym(self):

        action = self.policy(self.obs)
        self.obs, _, _, extras = self.env.step(action)

        self.env.robot.dof_pos[:] = torch.zeros_like(self.env.robot.dof_pos)
        self.env.robot.dof_vel[:] = torch.zeros_like(self.env.robot.dof_vel)
        self.env.robot.dof_pos[:,self.motion_loader.leg_idx_dict_rel["dof_pos_leg_hr"]] = self.dof_pos_RH 


        self.env.robot.root_pos_w[:] = torch.zeros_like(self.env.robot.root_pos_w) + torch.tensor([0, 0, 1], device="cuda")
        self.env.robot.root_quat_w[:] = torch.zeros_like(self.env.robot.root_quat_w) + torch.tensor([0, 0, 0, 1], device="cuda")
        self.env.robot.root_lin_vel_w[:] = torch.zeros_like(self.env.robot.root_lin_vel_w)
        self.env.robot.root_ang_vel_w[:] = torch.zeros_like(self.env.robot.root_ang_vel_w)



        env_ids_int32 = torch.arange(self.env.num_envs, device=self.env.device).to(dtype=torch.int32)        
        self.env.gym.set_dof_state_tensor_indexed(self.env.sim,
                                            gymtorch.unwrap_tensor(self.env.gym_iface.dof_state),
                                            gymtorch.unwrap_tensor(env_ids_int32), len(env_ids_int32))

        self.env.gym.set_actor_root_state_tensor_indexed(self.env.sim,
                                                    gymtorch.unwrap_tensor(self.env.robot.root_states),
                                                    gymtorch.unwrap_tensor(env_ids_int32), len(env_ids_int32))
        self.env.gym.refresh_rigid_body_state_tensor(self.env.sim)
if __name__ == "__main__":
    #initialize 
    node = Node()
    #Start 
    node.run()