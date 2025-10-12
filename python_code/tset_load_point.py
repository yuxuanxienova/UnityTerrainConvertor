import re
import os
# Update this path to match the exported file location
asset_path = os.path.join(os.path.dirname(__file__), 'Assets')
file_path = 'CombinedMesh/locomotion_map_v2/InitialPositions.txt'

path = os.path.join(asset_path, file_path)
#check path exists
if(os.path.exists(path)):
    print("File exists")
else:
    print("File does not exist")
# Dictionary to stosre data: {object_name: (x, y, z)}
object_positions = {}

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
    # We'll use a regular expression to parse that format.
    match = re.search(r'^Object:\s*(.*?),\s*Position:\s*\((.*?),\s*(.*?),\s*(.*?)\)$', line)
    if match:
        obj_name = match.group(1)
        x = float(match.group(2))
        y = float(match.group(3))
        z = float(match.group(4))

        object_positions[obj_name] = (x, y, z)

# Print out what we parsed
print("Parsed Object Positions:")
for name, (x, y, z) in object_positions.items():
    print(f"{name}: ({x}, {y}, {z})")