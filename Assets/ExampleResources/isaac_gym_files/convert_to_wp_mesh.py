import numpy as np
import torch

import warp as wp
from warp.torch import to_torch

wp.init()
def convert_to_wp_mesh(vertices, triangles, device):
    return wp.Mesh(
        points=wp.array(vertices.astype(np.float32), dtype=wp.vec3, device=device),
        indices=wp.array(triangles.astype(np.int32).flatten(), dtype=int, device=device),
    )