# UnityTerrainConvertor
This project provides scripts for:
- 1 Converting unity terrain object into mesh, export to .obj file
- 2 Combine multiple meshes
- 3 Export game object to .obj file

## Step By Step Guide
### Step1 Converting unity terrain object into mesh
- 1.0 Create a terrain by rightclick -> "3D Object" -> "Terrain"
- 1.1 Shape your terrain
- 1.2 In Menu bar click "Terrain" -> "Export to Obj..."
- 1.3 Then a .obj file will create for the terrain , but without additional object on it.

### Step2 Combine Terrain mesh with other object
- 2.0 Import the terrain mesh by drag and drop the .obj file in first step
- 2.1 Create a empty gameObject named as for example "Combined mesh"
- 2.2 Put all game objects and terrain mesh as the children of "Combined mesh" 
- 2.3 Select "Combined mesh" object in hierarchy window
- 2.4 In Menu bar click "Tools" -> "Combine Meshes"
- 2.5 A combined mesh will then generated

### Step3 Export The Combined Mesh
- 3.0 Select the combined mesh generated
- 3.1 In Menu bar click "Tools" -> "Export Selected to OBJ"
