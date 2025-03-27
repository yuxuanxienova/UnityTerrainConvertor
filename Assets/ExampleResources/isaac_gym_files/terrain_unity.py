import os
import numpy as np
import trimesh
from trimesh.transformations import rotation_matrix
from isaacgym import gymapi
from nav_gym.nav_legged_gym.utils.warp_utils import convert_to_wp_mesh
import torch
from nav_gym import NAV_GYM_ROOT_DIR
import re
import os
import random
from typing import TYPE_CHECKING, Union,List,Tuple
if TYPE_CHECKING:
    from nav_gym.nav_legged_gym.envs.config_locomotion_env import LocomotionEnvCfg
    from nav_gym.nav_legged_gym.envs.config_locomotion_fld_env import LocomotionFLDEnvCfg
    from nav_gym.nav_legged_gym.envs.config_local_nav_pae_latent_residual_command_env import LocalNavPAEEnvCfg
    from nav_gym.nav_legged_gym.envs.local_nav_pae_latent_residual_command_env import LocalNavPAEEnv
    ANY_ENV_CFG = Union[LocomotionEnvCfg, LocomotionFLDEnvCfg, LocalNavPAEEnvCfg]

class TerrainUnity:
    def __init__(self,gym,sim,device,num_envs,terrain_unity_cfg:"ANY_ENV_CFG.terrain_unity"):
        
        self.gym = gym
        self.sim = sim
        self.device = device
        self.num_envs = num_envs

        #Pattern
        self.pattern = terrain_unity_cfg.env_origin_pattern
        
        # Load terrain mesh here
        asset_root = os.path.join(NAV_GYM_ROOT_DIR,"resources")
        terrain_file = terrain_unity_cfg.terrain_file
        if(os.path.exists(asset_root + terrain_file)):
            print("[INFO]Terrain file found")
        else:
            print("[ERROR]Terrain file not found")
            exit(1)

        # Load the terrain mesh from a .obj file
        self.terrain_mesh = trimesh.load(asset_root + terrain_file)  # Replace 'terrain.obj' with your mesh file path

        # Extract vertices and triangle indices from the mesh
        self.vertices = np.array(self.terrain_mesh.vertices, dtype=np.float32)
        self.triangles = np.array(self.terrain_mesh.faces, dtype=np.uint32)

        # Convert to wp mesh
        self.wp_meshes:dict = {}
        self.wp_meshes["terrain"] = convert_to_wp_mesh(self.terrain_mesh.vertices, self.terrain_mesh.faces, self.device)

        # Create triangle mesh parameters
        self.tm_params = gymapi.TriangleMeshParams()
        self.tm_params.nb_vertices = self.vertices.shape[0]
        self.tm_params.nb_triangles = self.triangles.shape[0]

        # Initialize the transform without any rotation
        self.tm_params.transform = gymapi.Transform()
        self.tm_params.transform.p = gymapi.Vec3(0.0, 0.0, 0.0)  # DON'T CHANGE THIS, WILL CAUSE DIFFERNECE IN WP MESH

        # Set friction and restitution
        self.tm_params.static_friction = 1.0
        self.tm_params.dynamic_friction = 1.0
        self.tm_params.restitution = 0.0

        # terrain levels
        #self.terrain_levels_types[0][i]: levels of env i
        #self.terrain_levels_types[1][i]: types of env i
        self.terrain_levels_types:Tuple[torch.tensor,torch.tensor] = (torch.ones(self.num_envs, dtype=torch.int32, device=self.device, requires_grad=False),torch.zeros(self.num_envs, dtype=torch.int32, device=self.device, requires_grad=False))

        # Get the environment origins
        if self.pattern == "point":
            self.x_origin = terrain_unity_cfg.point_pattern.env_origins[0][0]
            self.y_origin = terrain_unity_cfg.point_pattern.env_origins[0][1]
            self.curriculum = False
            self._calcu_env_origins_point()
        elif self.pattern == "grid":
            self.x_offset = terrain_unity_cfg.grid_pattern.x_offset
            self.y_offset = terrain_unity_cfg.grid_pattern.y_offset
            self.env_spacing = terrain_unity_cfg.grid_pattern.env_spacing
            self.curriculum = False
            self._calcu_env_origins_grid()
        elif self.pattern == "pointpairs_file":
            resources_root = os.path.join(NAV_GYM_ROOT_DIR, "resources")
            file_root = terrain_unity_cfg.pointpairs_file_pattern.file_root
            dir_path = os.path.join(resources_root, file_root)
            self.env_origins = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)
            self.env_targets = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)
            self.curriculum = False
            self._calcu_env_origins_target_pairs_from_dir_path(dir_path)
        elif self.pattern == "pointpairs_with_level_file":
            self.weights = terrain_unity_cfg.pointpairs_with_level_file_pattern.terrain_weights
            self.level_to_point_set_dictlist_dict = {}
            resources_root = os.path.join(NAV_GYM_ROOT_DIR, "resources")
            file_root = terrain_unity_cfg.pointpairs_with_level_file_pattern.file_root
            dir_path = os.path.join(resources_root, file_root)
            self.env_origins = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)
            self.env_targets = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)
            self.max_terrain_level = terrain_unity_cfg.pointpairs_with_level_file_pattern.max_terrain_level
            self._calcu_env_origins_target_with_grouped_level_types_from_dir_path(dir_path)
            self.curriculum = terrain_unity_cfg.pointpairs_with_level_file_pattern.curriculum
            self.buffer_length=1000
            self.num_runs_count_buffer = torch.zeros(self.buffer_length,len(self.weights.keys())+1,self.max_terrain_level+1,device=self.device, requires_grad=False)#Dim(buffer_length,num_levels,num_types)
            self.num_success_count_buffer = torch.zeros(self.buffer_length,len(self.weights.keys())+1,self.max_terrain_level+1,device=self.device, requires_grad=False)#Dim(buffer_length,num_levels,num_types)
            self.success_rate_buffer = torch.zeros(len(self.weights.keys())+1,self.max_terrain_level+1,device=self.device, requires_grad=False)#Dim(num_levels,num_types)
            self.buffer_head_pointer = 0
        else:
            print("[ERROR]Invalid pattern")
            exit(1)
    def add_to_sim(self):
        # Add the terrain mesh to the simulation
        self.gym.add_triangle_mesh(self.sim, self.vertices.flatten(), self.triangles.flatten(), self.tm_params)
    def on_env_reset_idx(self,env,env_ids,extras_dict_epi):
        if self.pattern == "pointpairs_with_level_file":
            # self._update_success_rate_each_types_levels(env,env_ids)
            self.update_env_origins_using_cur_types_levels()
        self._log_info(env,extras_dict_epi)

##################################################Calculate Environment Origins##################################################

    def _calcu_env_origins_target_with_grouped_level_types_from_dir_path(self, dir_path: str):
        # Loop through files in the directory
        #point_set_dict_list: list of all point set in one level
        #point_set_dict_list[i][:]: list of all point set in one level, in type i
        #point_set: set of all point in one subterrain in one level, is a dictionary
        self.level_to_point_set_dictlist_dict = {}
        for file_name in os.listdir(dir_path):
            if file_name.startswith("level"):
                pattern = re.compile(r'^level(\d+)')
                match = pattern.search(file_name)
                if match:
                    # Extract the level number (as a string) and convert it to an integer.
                    level_number = int(match.group(1))
                    level_path = os.path.join(dir_path, file_name)
                    print(f"##############Folder: {file_name}, Level: {level_number}################")
                    point_set_dict_list:dict[List[dict]] = {}
                    for file_name in os.listdir(level_path):
                        if file_name.startswith("point_") and file_name.endswith(".txt"):
                            print(f"\nLoading file: {file_name}")
                            pattern = re.compile(r'^point_set_(\d+)_')
                            match = pattern.search(file_name)
                            if match:
                                type_number = int(match.group(1))
                                print("file of type: ", type_number)
                                point_sets = load_points_set_ros(level_path, file_name)
                                if point_set_dict_list.__contains__(type_number):
                                    point_set_dict_list[type_number].append(point_sets)
                                else:
                                    point_set_dict_list[type_number] = [point_sets]
                    self.level_to_point_set_dictlist_dict[level_number] = point_set_dict_list

        if not self.level_to_point_set_dictlist_dict:
            raise ValueError(f"No point pair files found in directory: {level_path}")

        print(f"Level to Point Set List Directory: {self.level_to_point_set_dictlist_dict}")


        # Prepare tensors for origins and targets
        self.env_origins = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False) 
        self.env_targets = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)

        
        # Randomly assign a point pair to each environment
        for i in range(self.num_envs):
            point_set_dict_list_i = self.level_to_point_set_dictlist_dict[1]
            # Pick one type from level 1
            keys = list(point_set_dict_list_i.keys())
            weight_list = [self.weights[k] for k in keys]
            selected_key = random.choices(keys, weights=weight_list, k=1)[0]
            # point_set_list_type_j = random.choice(point_set_dict_list_i)
            selected_type_list = point_set_dict_list_i[selected_key]
            #Pick one point set from selected type
            point_set_dict = random.choice(selected_type_list)
            
            # Convert dictionary values to a list
            point_values = list(point_set_dict.values())
            
            # Ensure there are at least 2 distinct points to choose from
            if len(point_values) < 2:
                raise ValueError(f"Not enough distinct points in the point set: {point_set_dict}")
            
            # Use random.sample to select two distinct points
            start_tuple, end_tuple = random.sample(point_values, 2)
            
            #Assign these to environment:
            #self.terrain_levels_types[0][i]: levels of env i
            #self.terrain_levels_types[1][i]: types of env i
            self.terrain_levels_types[1][i] = selected_key
            self.env_origins[i] = torch.tensor(start_tuple, device=self.device)
            self.env_targets[i] = torch.tensor(end_tuple, device=self.device)
            #print("[INFO][calcu_env_origins_target]env_id:{0},level:{1},type:{2},env_origins:{3},env_targets:{4}".format(i,1,selected_key,self.env_origins,self.env_targets))
    def _calcu_env_origins_target_pairs_from_dir_path(self, dir_path: str):
        # Loop through files in the directory
        pointpair_list = []
        for file_name in os.listdir(dir_path):
            # Check if file name starts with 'point_pair_' and ends with '.txt'
            if file_name.startswith("point_") and file_name.endswith(".txt"):
                print(f"\nLoading file: {file_name}")
                pointpair_positions = load_points_set_ros(dir_path, file_name)
                pointpair_list.append(pointpair_positions)

        if not pointpair_list:
            raise ValueError(f"No point pair files found in directory: {dir_path}")

        # Prepare tensors for origins and targets
        self.env_origins = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False) 
        self.env_targets = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)

        # Randomly assign a point pair to each environment
        for i in range(self.num_envs):
            # Pick one dictionary from pointpair_list at random
            pair_dict = random.choice(pointpair_list)

            # **Option 1: Direct Access (Preferred if 'start' and 'target' keys exist)**
            if "start" in pair_dict and "target" in pair_dict:
                start_tuple = pair_dict["start"]
                target_tuple = pair_dict["target"]
            else:
                # **Option 2: Random Sampling (Use if multiple points and no specific keys)**
                values = list(pair_dict.values())
                if len(values) < 2:
                    raise ValueError(f"Not enough points in pair_dict to assign origin and target: {pair_dict}")
                start_tuple, target_tuple = random.sample(values, 2)

            # Convert them to torch tensors and assign
            self.env_origins[i] = torch.tensor(start_tuple, device=self.device)# + torch.tensor(-self.terrain_translation_standard, device=self.device, requires_grad=False).float()
            self.env_targets[i] = torch.tensor(target_tuple, device=self.device)# + torch.tensor(-self.terrain_translation_standard, device=self.device, requires_grad=False).float()
    def _calcu_env_origins_point(self):
        self.env_origins = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)  # Dim:(num_envs, 3)
        # set origin and goal positions
        # self.x_goal = 1.0
        # self.y_goal =-4.0
        self.x_origin=self.x_origin
        self.y_origin=self.y_origin
        self.env_origins[:, 0] = torch.tensor([self.x_origin] * self.num_envs, device=self.device)
        self.env_origins[:, 1] = torch.tensor([self.y_origin] * self.num_envs, device=self.device)
        # Remove the hardcoded z value
        # self.env_origins[:, 2] = -1.0

        # Prepare ray origins and directions
        z_max = 60.0  # Set a height above the highest possible terrain point
        ray_origins = np.zeros((self.num_envs, 3))
        ray_origins[:, 0] = self.env_origins[:, 0].cpu().numpy()
        ray_origins[:, 1] = self.env_origins[:, 1].cpu().numpy()
        ray_origins[:, 2] = z_max  # Start raycasting from above

        ray_directions = np.zeros((self.num_envs, 3))
        ray_directions[:, 2] = -1.0  # Pointing downwards along the z-axis

        # Perform raycasting
        ray_intersections, index_ray, index_tri = self.terrain_mesh.ray.intersects_location(
            ray_origins=ray_origins, ray_directions=ray_directions
        )

        num_rays = ray_origins.shape[0]
        highest_intersections = np.zeros((num_rays, 3))

        # Loop through each ray to find the intersection with the largest z-value
        for ray_idx in range(num_rays):
            # Get the indices of intersections for the current ray
            indices = np.where(index_ray == ray_idx)[0]
            
            if len(indices) == 0:
                # Handle rays that did not intersect the mesh
                print(f"[WARNING] Ray {ray_idx} did not hit the terrain")
                # You can set a default z-value or handle it as needed
                highest_intersections[ray_idx] = [ray_origins[ray_idx, 0], ray_origins[ray_idx, 1], 0.0]  # Default z=0.0
                continue
            
            # Get all intersections for this ray
            intersections = ray_intersections[indices]
            
            # Find the intersection with the maximum z-value
            max_z_idx = np.argmax(intersections[:, 2])
            highest_intersections[ray_idx] = intersections[max_z_idx]

        # Update env_origins z-coordinate with the terrain height
        self.env_origins[:, 2] = torch.tensor(highest_intersections[:, 2], device=self.device)
    def _calcu_env_origins_grid(self):
        x_offset = self.x_offset
        y_offset = self.y_offset
        self.env_origins = torch.zeros(self.num_envs, 3, device=self.device, requires_grad=False)  # Dim:(num_envs, 3)
        # Create a grid of robots
        num_cols = np.floor(np.sqrt(self.num_envs))
        num_rows = np.ceil(self.num_envs / num_cols)
        xx, yy = torch.meshgrid(torch.arange(num_rows), torch.arange(num_cols))
        spacing = self.env_spacing
        self.env_origins[:, 0] = spacing * xx.flatten()[:self.num_envs] + x_offset
        self.env_origins[:, 1] = spacing * yy.flatten()[:self.num_envs] + y_offset
        # Remove the hardcoded z value
        # self.env_origins[:, 2] = -1.0

        # Prepare ray origins and directions
        z_max = 60.0  # Set a height above the highest possible terrain point
        ray_origins = np.zeros((self.num_envs, 3))
        ray_origins[:, 0] = self.env_origins[:, 0].cpu().numpy()
        ray_origins[:, 1] = self.env_origins[:, 1].cpu().numpy()
        ray_origins[:, 2] = z_max  # Start raycasting from above

        ray_directions = np.zeros((self.num_envs, 3))
        ray_directions[:, 2] = -1.0  # Pointing downwards along the z-axis

        # Perform raycasting
        ray_intersections, index_ray, index_tri = self.terrain_mesh.ray.intersects_location(
            ray_origins=ray_origins, ray_directions=ray_directions
        )

        num_rays = ray_origins.shape[0]
        highest_intersections = np.zeros((num_rays, 3))

        # Loop through each ray to find the intersection with the largest z-value
        z_offset=0.0
        for ray_idx in range(num_rays):
            # Get the indices of intersections for the current ray
            indices = np.where(index_ray == ray_idx)[0]
            
            if len(indices) == 0:
                # Handle rays that did not intersect the mesh
                print(f"[WARNING] Ray {ray_idx} did not hit the terrain")
                # You can set a default z-value or handle it as needed
                highest_intersections[ray_idx] = [ray_origins[ray_idx, 0], ray_origins[ray_idx, 1], 0.0]  # Default z=0.0
                continue
            
            # Get all intersections for this ray
            intersections = ray_intersections[indices]
            
            # Find the intersection with the maximum z-value
            max_z_idx = np.argmax(intersections[:, 2])
            highest_intersections[ray_idx] = intersections[max_z_idx]

            #offset
            # z_offset = torch.tensor([0.2]).repeat(self.env_origins.shape[0]).to(self.device)

        # Update env_origins z-coordinate with the terrain height
        self.env_origins[:, 2] = torch.tensor(highest_intersections[:, 2], device=self.device) #+ z_offset
    def sample_new_init_poses(self,env_ids):
        # Sample new initial poses for the environments
        return self.env_origins[env_ids] 
    
########################################################Update Terrain Levels#####################################################

    def update_terrain_levels(self, env_ids, move_up, move_down):
        #self.terrain_levels_types[0][i]: levels of env i
        #self.terrain_levels_types[1][i]: types of env i
        if self.curriculum:
            self.terrain_levels_types[0][env_ids] += 1 * move_up - 1 * move_down

            for i in env_ids:

                if self.terrain_levels_types[0][i] > self.max_terrain_level:
                    point_set_dict_list_i = self.level_to_point_set_dictlist_dict[1]
                    # Randomly choose a type with weight 
                    keys = list(point_set_dict_list_i.keys())
                    # filtered_keys = [key for key in keys if key != self.terrain_levels_types[1][i].item()]
                    filtered_keys = [key for key in keys]
                    weight_list = [self.weights[k] for k in filtered_keys]
                    selected_key = random.choices(filtered_keys, weights=weight_list, k=1)[0]
                    self.terrain_levels_types[1][i] = selected_key
                    #Reset the level
                    self.terrain_levels_types[0][i] = 1

                if self.terrain_levels_types[0][i] < 1:
                    self.terrain_levels_types[0][i] = 1

                level_i = self.terrain_levels_types[0][i].item()
                type_i = self.terrain_levels_types[1][i].item()

                point_set_dict_list_i = self.level_to_point_set_dictlist_dict[level_i]
                point_set_list_i = point_set_dict_list_i[type_i]
                # Pick one point set from level 
                point_set_dict = random.choice(point_set_list_i)
                
                # Convert dictionary values to a list
                point_values = list(point_set_dict.values())
                
                # Ensure there are at least 2 distinct points to choose from
                if len(point_values) < 2:
                    raise ValueError(f"Not enough distinct points in the point set: {point_set_dict}")
                
                # Use random.sample to select two distinct points
                start_tuple, end_tuple = random.sample(point_values, 2)
                
                #Assign these to environments
                self.env_origins[i] = torch.tensor(start_tuple, device=self.device)
                self.env_targets[i] = torch.tensor(end_tuple, device=self.device)
                #print("[INFO][Update Terrain Levels]env_id:{0},level:{1},type:{2},env_origins:{3},env_targets:{4}".format(i,level_i,type_i,self.env_origins,self.env_targets))
    def update_env_origins_using_cur_types_levels(self):
            if self.pattern != "pointpairs_with_level_file":
                return

            env_ids = torch.arange(self.num_envs, device=self.device)
            for i in env_ids:
                level_i = self.terrain_levels_types[0][i].item()
                type_i = self.terrain_levels_types[1][i].item()

                point_set_dict_list_i = self.level_to_point_set_dictlist_dict[level_i]
                point_set_list_i = point_set_dict_list_i[type_i]
                # Pick one point set from level 
                point_set_dict = random.choice(point_set_list_i)
                
                # Convert dictionary values to a list
                point_values = list(point_set_dict.values())
                
                # Ensure there are at least 2 distinct points to choose from
                if len(point_values) < 2:
                    raise ValueError(f"Not enough distinct points in the point set: {point_set_dict}")
                
                # Use random.sample to select two distinct points
                start_tuple, end_tuple = random.sample(point_values, 2)
                
                # (Optional) Assign these to your environments, for example:
                self.env_origins[i] = torch.tensor(start_tuple, device=self.device)
                self.env_targets[i] = torch.tensor(end_tuple, device=self.device)
                #print("[INFO][update_env_origins_using_cur_types_levels]env_id:{0},level:{1},type:{2},env_origins:{3},env_targets:{4}".format(i,level_i,type_i,self.env_origins,self.env_targets))
    ###################################Update Success Rate in each types####################################
    def _update_success_rate_each_types_levels(self,env, env_ids):
        if self.pattern == "pointpairs_with_level_file":
            if(self.buffer_head_pointer>self.buffer_length-2):
                self.buffer_head_pointer=0
            else:
                self.buffer_head_pointer +=1
                self.num_runs_count_buffer[self.buffer_head_pointer,:,:] =0
                self.num_success_count_buffer[self.buffer_head_pointer,:,:]=0 

            for i in env_ids:
                level_i = self.terrain_levels_types[0][i].item()
                type_i = self.terrain_levels_types[1][i].item()
                self.num_runs_count_buffer[self.buffer_head_pointer,type_i,level_i] += env.termination_manager.terminated_buf[i]
                self.num_success_count_buffer[self.buffer_head_pointer,type_i,level_i] += env.termination_manager.reach_goal_buf[i]
                self.success_rate_buffer[type_i,level_i] = torch.sum(self.num_success_count_buffer[:,type_i,level_i])/(torch.sum(self.num_runs_count_buffer[:,type_i,level_i])+10)
    #############################################################################################################
    def _log_info(self, env, extras_dict):
        """ Fill env extras with episode sum of each reward """
        if self.pattern == "pointpairs_with_level_file":
            for type_i in range(self.success_rate_buffer.shape[0]):
                for level_i in range(self.success_rate_buffer.shape[1]):
                    extras_dict["[success_rate][type:{0}][level:{1}]".format(type_i,level_i)] = self.success_rate_buffer[type_i,level_i]
                    # print(self.success_rate_buffer)
############################################Others######################################################################
def load_points_set_ros(dir_path:str,file_name:str):
    # Update this path to match the exported file location
    path = os.path.join(dir_path, file_name)
    #check if the file exists
    if not os.path.exists(path):
        print(f"File not found: {path}")
        exit(1)
    # Dictionary to store data: {object_name: (x, y, z)}
    pointpair_positions_ros = {}

    with open(path, 'r', encoding='utf-8') as f:
        # Read all lines from the file
        lines = f.readlines()

    # We expect the first two lines to be:
    #   Object Positions Export
    #   =======================
    # so we start parsing from line 3 onward
    for line in lines[2:]:
        line = line.strip()
        if not line:
            continue  # skip any empty lines
        
        # Each line should look like:
        #   Object: Cube, Position: (1.0, 2.0, -3.0)
        #use a regular expression to parse that format.
        match = re.search(r'^Object:\s*(.*?),\s*Position:\s*\((.*?),\s*(.*?),\s*(.*?)\)$', line)
        if match:
            obj_name = match.group(1)
            x = float(match.group(2))
            y = float(match.group(3))
            z = float(match.group(4))

            pointpair_positions_ros[obj_name] = (x, y, z)

    # Print out what we parsed
    print("Parsed Object Positions:")
    for name, (x, y, z) in pointpair_positions_ros.items():
        print(f"{name}: ({x}, {y}, {z})")

    return pointpair_positions_ros

def vec_unity_to_ros(vec3_unity):
    x = vec3_unity[2]
    y = -vec3_unity[0]
    z = vec3_unity[1]
    return [x,y,z]
def vec_ros_to_unity(vec3_ros):
    x = -vec3_ros[1]
    y = vec3_ros[2]
    z = vec3_ros[0]
    return [x,y,z]

if __name__ == "__main__":
    # Locate directory containing the files "point_pair_*.txt"
    dir_path = os.path.join(NAV_GYM_ROOT_DIR, "resources", "terrain", "locomotion_map_v2")

    # Loop through files in the directory
    pointpair_list = []
    for file_name in os.listdir(dir_path):
        # Check if file name starts with 'point_pair_' and ends with '.txt'
        if file_name.startswith("point_pair_") and file_name.endswith(".txt"):
            print(f"\nLoading file: {file_name}")
            pointpair_positions = load_pointpair_unity(dir_path,file_name)
            pointpair_list.append(pointpair_positions)