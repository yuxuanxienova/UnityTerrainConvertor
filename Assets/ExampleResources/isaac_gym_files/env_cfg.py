class terrain_unity:
    terrain_file:str = "/terrain/navigation_map_v2/NavigationMap_v2.obj"
    translation: Tuple = (-30.0, -30.0, 0.0)
    env_origin_pattern:str = "pointpairs_file" # "point" or "grid" or "pointpairs_file" or "pointpairs_with_level_file"
    class grid_pattern:
        env_spacing:float = 2.0
        x_offset:float = -80.0
        y_offset:float = -80.0
    class point_pattern:
        env_origins:List = [(0.0, 0.0, 0.0)]
    class pointpairs_file_pattern:
        file_root="terrain/plane_map" # do not start with "/"
    class pointpairs_with_level_file_pattern:
        file_root="terrain/navigation_map_v2"# do not start with "/"
        terrain_weights = {1: 0.0, 2: 0.0, 3: 0.0, 4: 0.6, 5:1.2, 6:0.1, 7:0.1, 8:0.1, 9:0.1, 10:0.1, 11:0.0,12:0.0,13:0.0,14:0.0 }
        max_terrain_level = 3
        curriculum = True