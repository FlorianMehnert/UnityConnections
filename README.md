# UnityConnections

The goal of this project is to understand complicated UnityScenes by visualizing the underlying structure using various references.

## Setup
Since I am only getting started with Unity, this section has still had a lot of steps to take by hand
1. `git clone https://github.com/FlorianMehnert/UnityConnections.git`
2. in the Assets folder remove the 3DConnections file e.g. `rm Assets/3DConnections`
3. `git clone https://github.com/FlorianMehnert/3DConnections.git Assets/3DConnection`
4. open UnityConnections as a Unity project
5. download the unity package for the standalone file browser and import into unity Assets → Import Package → Custom Package
6. install NuGet
7. in NuGet search for Microsoft.CodeAnalysis and Microsoft.CodeAnalysis.CSharp and install these packages (a version in this project is 4.12.0 for both. on linux, you might also need to install SQLitePCLRaw.bundle_green if workspaces fail to compile 


## Progress

- This project is only the test environment for all the contained modules, e.g. [SceneConnections](https://github.com/FlorianMehnert/SceneConnections) and its successor [3DConnections](https://github.com/FlorianMehnert/3DConnections/)
- SceneConnections focussed more on the structure of classes and inner dependencies, visualizing them using in separate Editor Windows using GraphViewApi which predefines relevant concepts such as nodes and their connections
- 3DConnections acts as an overlay to existing scenes meant to be dragged into existing Scenes to visualize at Runtime

### Scene Connections: [Rectangle Graph](https://github.com/FlorianMehnert/SceneConnections/blob/main/Editor/RectangleOverview.cs) 
- prototype of ComponentGraphView using rectangles instead of nodes which allowed for more extensibility
- generates a graph of class instances and connects them as they have references to each other
- selecting a rectangle in the editor pings the related game object which allows changing the related game object at runtime using the inspector
- performs layout using basic grids with fixed spacing since dimensions of nodes [can change at runtime](https://discussions.unity.com/t/how-can-i-properly-space-dynamically-loaded-nodes-in-graphview/875298) which forces the following implementations to register to the OnGeometryChanged event

![Rectangle Graph](images/rectangle_graph.png)

### Scene Connections: [Component Graph](https://github.com/FlorianMehnert/SceneConnections/blob/main/Editor/ComponentGraphView.cs)
- reimplementation of class parsing/grid layout done in Rectangle Graph
- has additional features such as force directed layout, parsing of specific root folder
- uses the [experimental graph view api](https://docs.unity3d.com/ScriptReference/Experimental.GraphView.GraphView.html)
- minimap
  ![Graph View Api](images/graph_view_api.png)
- supports different modes: 
  - nodes are scripts (each node represents a file contained in the root folder specified in the given path)
  - nodes are components (nodes are contained in group objects that represent the game object while each node shows its current values)
  - nodes are gameobjects (each node represent )
  ![nodes are gameobjects](images/nodes_are_gameobjects.png)

### Evaluation of GraphViewApi
- most of the important ui update logic can only be performed on the main thread resulting in a slow UI 
- Node graphs with ~10,000 nodes take 40 seconds+ to be created with an average performance of 10 fps which is unpleasant to work with
- colored node connections (Line renderer instance) are not possible
- provides components such as minimap/searchbar out of the box
- node sizes are not available during creation
- integrates nicely into other editors
- still experimental
- perfect for smaller visualizations where 
- simple and easy to use allowing to focus on important tasks such as how to visualize node connections

### [3DConnections](https://github.com/FlorianMehnert/3DConnections/)
- at runtime scene overlay using main prefabs to display 3D nodes
- can be toggled
- works with prefabs
- integrates better into the rendering pipeline resulting in a better performance
- required to rewrite nodes, connections, update logic for nodes, camera movement, dragging behavior
- text cannot easily be contained in nodes since this would require the node sizes to be recalculated
![grid layout implementation](images/overlay_implementation_grid.gif)

## Current goals
- implement better layout algorithms in 3DConnections showing off parent-child connections, optimize for none overlapping connections
- [lag googles](https://www.curseforge.com/minecraft/mc-mods/laggoggles) like functionality to show time consumed by game objects (profiler for UnityScenes) 
- add a ping mechanism to highlight/change objects on select
- improve selection behavior (select node whose center is closest to the cursor)
